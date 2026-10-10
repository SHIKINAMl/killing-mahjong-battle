using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KillingMahjong.UI.Effects
{
    /// <summary>
    /// 演出の順番待ち（2026-10-11）。
    ///
    /// ユーザーの指示「演出中にほかの演出が入ると重なる。演出は全部キューに入れて流れるようにしてほしい。
    /// カットしてもいい演出は、ほかの演出が来た瞬間にカットしてほしい」。
    ///
    /// **重なっていた理由。** フェイズの演出（配牌・対局開始・ロンなど）は、前から
    /// 「演出中に届いたら、明けるまで保留する」作りになっている（GameUIManager.DeferUntilIdle）。
    /// ところがスキルの演出は届いた瞬間に始めていて、何が流れていても待たなかった。
    /// 役名表示（満貫など）は、その仕組みの外にあった。
    ///
    /// **ここがやること。**
    ///   1. 入れた順に、1つずつ流す（<see cref="Enqueue"/>）
    ///   2. 流し始める前に、ここを通らない演出（フェイズの演出）が終わるのを待つ（<see cref="ForeignBusy"/>）
    ///   3. カットしてよい演出は、次の演出が来た瞬間に切る
    ///
    /// フェイズの演出は、いままでどおりロック（GameUIManager.BeginTransition）と保留で順番を守る。
    /// ここの演出も流れているあいだはロックを取るので、フェイズの演出は明けるのを待つ。
    /// つまり2つの仕組みは、ロックを見合うことで1本の列になっている。
    ///
    /// **カットしてよい演出**（ほかの演出が来たら消える）
    ///   役名表示（満貫・跳満・倍満・役満）… <see cref="Enqueue"/> に cuttable で入れる
    ///   「手牌を選んでください」の案内    … 出す時刻を変えたくないので列には入れず、
    ///                                       「いま出ている」とだけ届け出る（<see cref="RegisterShowing"/>）
    /// **カットしない演出**（順番を待って、最後まで流す）
    ///   スキルの演出（カットインから戻りまで。自分のも相手のも）
    ///
    /// シーンごとに1つ。シーンが変わると一緒に消える。
    /// </summary>
    public sealed class EffectQueue : MonoBehaviour
    {
        /// <summary>列に入れた演出1つ。</summary>
        public sealed class Ticket
        {
            public string Name { get; internal set; }
            /// <summary>流し終えたか（切られた・捨てられた場合も true）。</summary>
            public bool Finished { get; internal set; }
            /// <summary>途中で切られた、または流さずに捨てられたか。</summary>
            public bool WasCut { get; internal set; }

            internal Func<IEnumerator> Body;
            internal bool Cuttable;
            internal Action OnCut;
            internal Func<bool> StillValid;
            internal Coroutine Running;
        }

        /// <summary>「いま出ている、カットしてよい演出」の届け出。</summary>
        public sealed class Showing
        {
            internal string Name;
            internal Action Cut;
            internal bool Active;
        }

        /// <summary>
        /// ここを通らない演出を待つ上限（秒）。
        /// ロックが外れないまま残ると、列が永久に止まる。上限を過ぎたら、重なってでも流す。
        /// </summary>
        private const float MaxForeignWaitSeconds = 20f;

        /// <summary>
        /// 1つの演出にかけてよい上限（秒）。演出の中で例外が出ると「終わった」が届かない。
        /// 上限を過ぎたら次へ進む。いちばん長いスキルの演出で10秒ほど。
        /// </summary>
        private const float MaxItemSeconds = 40f;

        private static EffectQueue _instance;

        private readonly List<Ticket> _pending = new List<Ticket>();
        private readonly List<Showing> _showing = new List<Showing>();
        private Ticket _current;
        private Coroutine _runner;
        private GameUIManager _ui;

        public static EffectQueue Instance
        {
            get
            {
                if (_instance == null && Application.isPlaying)
                {
                    var go = new GameObject("EffectQueue");
                    _instance = go.AddComponent<EffectQueue>();
                }
                return _instance;
            }
        }

        /// <summary>いま流している演出の名前。無ければ null。</summary>
        public string CurrentName { get { return _current != null ? _current.Name : null; } }

        /// <summary>順番を待っている演出の数。</summary>
        public int PendingCount { get { return _pending.Count; } }

        /// <summary>確認用。いまの列の様子。</summary>
        public string DebugState
        {
            get
            {
                var names = new List<string>();
                foreach (var t in _pending) names.Add(t.Name);
                return "current=" + (CurrentName ?? "-") + " pending=[" + string.Join(",", names.ToArray())
                    + "] showing=" + _showing.Count + " foreignBusy=" + ForeignBusy;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// ここを通らない演出（フェイズの演出）が流れているか。
        /// 牌を動かす短いアニメと、牌交換の返事待ちは数えない（GameUIManager.IsBusyForQueuedEffect）。
        /// </summary>
        private bool ForeignBusy
        {
            get
            {
                if (_ui == null) _ui = FindFirstObjectByType<GameUIManager>();
                return _ui != null && _ui.isActiveAndEnabled && _ui.IsBusyForQueuedEffect;
            }
        }

        /// <summary>
        /// 演出を列に入れる。前の演出が終わり、フェイズの演出も明けたら流れる。
        /// **入れた瞬間に、カットしてよい演出は切られる。**
        /// </summary>
        /// <param name="name">演出の名前。記録と確認に使う</param>
        /// <param name="body">演出の本体。終わるまで返らないこと</param>
        /// <param name="cuttable">ほかの演出が来たら切ってよいか</param>
        /// <param name="onCut">切られたときの後片付け（画面から消す）。流す前に捨てられたときは呼ばれない</param>
        /// <param name="stillValid">順番が来たときに、まだ流す意味があるか。false なら流さずに捨てる</param>
        public Ticket Enqueue(string name, Func<IEnumerator> body, bool cuttable = false,
            Action onCut = null, Func<bool> stillValid = null)
        {
            var ticket = new Ticket { Name = name, Body = body, Cuttable = cuttable, OnCut = onCut, StillValid = stillValid };
            if (body == null) { ticket.Finished = true; return ticket; }

            // 来た瞬間に切る。順番待ちの中のカットしてよい演出も、出た直後に切られるだけなので捨てる
            CutShowing(name);
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (!_pending[i].Cuttable) continue;
                _pending[i].Finished = true;
                _pending[i].WasCut = true;
                _pending.RemoveAt(i);
            }

            _pending.Add(ticket);
            if (_runner == null) _runner = StartCoroutine(Run());
            return ticket;
        }

        /// <summary>
        /// 列には入れないが「いま出ている、カットしてよい演出」として届け出る。
        /// ほかの演出が来たら <paramref name="cut"/> が呼ばれる。出し終えたら <see cref="Unregister"/>。
        /// </summary>
        public Showing RegisterShowing(string name, Action cut)
        {
            var showing = new Showing { Name = name, Cut = cut, Active = true };
            _showing.Add(showing);
            return showing;
        }

        public void Unregister(Showing showing)
        {
            if (showing == null) return;
            showing.Active = false;
            _showing.Remove(showing);
        }

        /// <summary>
        /// いま出ている、カットしてよい演出を切る。**順番待ちの中身には触らない。**
        /// 次の演出が列に入ったときと、フェイズの演出が始まったとき（GameUIManager.BeginTransition）に呼ばれる。
        /// </summary>
        public void CutShowing(string reason)
        {
            if (_current != null && _current.Cuttable && !_current.Finished)
            {
                var cut = _current;
                if (cut.Running != null) StopCoroutine(cut.Running);
                cut.Running = null;
                cut.WasCut = true;
                cut.Finished = true;
                InvokeSafely(cut.OnCut);
                Debug.Log("[EffectQueue] '" + cut.Name + "' を切りました（" + reason + " が来たため）");
            }

            if (_showing.Count == 0) return;
            var list = _showing.ToArray();
            _showing.Clear();
            foreach (var s in list)
            {
                if (!s.Active) continue;
                s.Active = false;
                InvokeSafely(s.Cut);
            }
        }

        /// <summary>
        /// 列を空にする。流している演出は、カットしてよいものだけ画面から消す。
        /// 対局の画面が閉じるときに呼ぶ（スキルの演出は、スキル側の中止処理が片付ける）。
        /// </summary>
        public void Clear()
        {
            foreach (var t in _pending) { t.Finished = true; t.WasCut = true; }
            _pending.Clear();
            CutShowing("clear");
            if (_current != null)
            {
                if (_current.Running != null) StopCoroutine(_current.Running);
                _current.Running = null;
                _current.Finished = true;
                _current.WasCut = true;
                _current = null;
            }
            if (_runner != null) { StopCoroutine(_runner); _runner = null; }
        }

        /// <summary>実体があれば切る。無ければ作らない。</summary>
        public static void CutShowingIfAny(string reason)
        {
            if (_instance != null) _instance.CutShowing(reason);
        }

        /// <summary>実体があれば空にする。無ければ作らない。</summary>
        public static void ClearIfAny()
        {
            if (_instance != null) _instance.Clear();
        }

        private IEnumerator Run()
        {
            while (_pending.Count > 0)
            {
                // ここを通らない演出（フェイズの演出）が明けるのを待つ
                float waited = 0f;
                while (ForeignBusy)
                {
                    waited += Time.unscaledDeltaTime;
                    if (waited >= MaxForeignWaitSeconds)
                    {
                        Debug.LogWarning("[EffectQueue] ほかの演出が " + MaxForeignWaitSeconds
                            + " 秒たっても明けません。待つのをやめて流します: " + _pending[0].Name);
                        break;
                    }
                    yield return null;
                    if (_pending.Count == 0) break;
                }
                if (_pending.Count == 0) break;

                Ticket ticket = _pending[0];
                _pending.RemoveAt(0);

                if (ticket.StillValid != null && !SafeBool(ticket.StillValid))
                {
                    ticket.Finished = true;
                    ticket.WasCut = true;
                    continue;
                }

                _current = ticket;
                ticket.Running = StartCoroutine(RunItem(ticket));

                float ran = 0f;
                while (!ticket.Finished)
                {
                    ran += Time.unscaledDeltaTime;
                    if (ran >= MaxItemSeconds)
                    {
                        Debug.LogWarning("[EffectQueue] '" + ticket.Name + "' が " + MaxItemSeconds
                            + " 秒たっても終わりません。次へ進みます");
                        ticket.Finished = true;
                        break;
                    }
                    yield return null;
                }
                if (_current == ticket) _current = null;
            }
            _runner = null;
        }

        private IEnumerator RunItem(Ticket ticket)
        {
            IEnumerator body = null;
            try { body = ticket.Body(); }
            catch (Exception e) { Debug.LogException(e); }

            while (body != null)
            {
                object step;
                try
                {
                    if (!body.MoveNext()) break;
                    step = body.Current;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    break;
                }
                yield return step;
            }

            ticket.Running = null;
            ticket.Finished = true;
        }

        private static void InvokeSafely(Action action)
        {
            if (action == null) return;
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        private static bool SafeBool(Func<bool> check)
        {
            try { return check(); }
            catch (Exception e) { Debug.LogException(e); return false; }
        }
    }
}
