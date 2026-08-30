using UnityEngine;

namespace KillingMahjong.UI
{
    /// <summary>
    /// PlayerInfoUI と EnemyInfoUI で中身が一字一句同じだったロジックだけを集めた
    /// 非 MonoBehaviour の静的ヘルパ（2026-08-29、重複メンバーの整理で追加）。
    ///
    /// **ここに置いてよいのは両クラスで完全に同じ処理だけ。**
    /// ズームの向き・Ready札の出す出さない・演出の有無など、自分側・相手側で
    /// 意図的に振る舞いが違う部分は各クラス側に残したまま触らないこと
    /// （理由は PlayerInfoUI / EnemyInfoUI 側のコメントを参照）。
    /// </summary>
    internal static class HpMeterMath
    {
        /// <summary>
        /// HPメーターの分母。開始HP（<paramref name="maxHp"/>）と、対局中に一度でも
        /// 到達した最高HP（<paramref name="hpPeak"/>）のうち大きい方を使う。
        /// ロンで血を奪って開始HPを超えたときに fillAmount が1で頭打ちにならないようにするため
        /// （PlayerInfoUI.MeterMax / EnemyInfoUI.MeterMax の元コメント参照。
        /// ダメージSEのピッチ判定など「絶対量」を見たい箇所は maxHp をそのまま使うので、
        /// ここには関与しない）。
        /// </summary>
        public static int MeterMax(int maxHp, int hpPeak) => Mathf.Max(1, Mathf.Max(maxHp, hpPeak));

        /// <summary>到達最高HPを引き下げずに引き上げる（SetMaxHP / ResetHpMeter / SetHP から呼ばれる）。</summary>
        public static int RaisePeak(int hpPeak, int candidate) => Mathf.Max(hpPeak, candidate);
    }

    internal static class CharacterVisualUtil
    {
        /// <summary>
        /// CharacterData から「通常時の体」「打牌時の体」「通常時の顔」を解決する。
        /// 元の PlayerInfoUI.ApplyCharacterData / EnemyInfoUI.ApplyCharacterData と一字一句同じロジック。
        ///
        /// bodySprites / faceSprites が空のときは <paramref name="normalFaceSprite"/> を
        /// 上書きしない（呼び出し前の値をそのまま残す）。これは元の実装の挙動をそのまま踏襲している。
        /// </summary>
        public static void ResolveDefaultSprites(CharacterData data, ref Sprite normalSprite, ref Sprite discardSprite, ref Sprite normalFaceSprite)
        {
            normalSprite = data.normalSprite;
            discardSprite = data.discardSprite;

            if (data.bodySprites != null && data.bodySprites.Count > 0)
            {
                var match = data.bodySprites.Find(x => x.id == data.defaultBodyId);
                normalSprite = match != null ? match.sprite : data.bodySprites[0].sprite;
            }

            if (data.faceSprites != null && data.faceSprites.Count > 0)
            {
                var match = data.faceSprites.Find(x => x.id == data.defaultFaceId);
                normalFaceSprite = match != null ? match.sprite : data.faceSprites[0].sprite;
            }
        }

        /// <summary>renderer / sprite が両方 non-null のときだけ差し替える。あちこちで繰り返されていた null チェックの共通化。</summary>
        public static void ApplyIfPresent(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer != null && sprite != null)
            {
                renderer.sprite = sprite;
            }
        }

        /// <summary>SetBodyPose の中身。characterData.bodySprites から poseId を探す。</summary>
        public static bool TryFindBodySprite(CharacterData data, string poseId, out Sprite sprite)
        {
            sprite = null;
            var match = data?.bodySprites?.Find(x => x.id == poseId);
            if (match == null || match.sprite == null) return false;
            sprite = match.sprite;
            return true;
        }

        /// <summary>SetFaceExpression の中身。characterData.faceSprites から expressionId を探す。</summary>
        public static bool TryFindFaceSprite(CharacterData data, string expressionId, out Sprite sprite)
        {
            sprite = null;
            var match = data?.faceSprites?.Find(x => x.id == expressionId);
            if (match == null || match.sprite == null) return false;
            sprite = match.sprite;
            return true;
        }

        /// <summary>
        /// SetDiscardingState の中身。打牌中かどうかで出す画像を選ぶ。
        /// characterData 側の画像を優先し、無ければ生成時に控えたフォールバックを使う
        /// （実行中に characterData が差し替わっても反映されるようにするため）。
        /// 戻り値が null のときは呼び出し側で何もしない（元の実装と同じ）。
        /// </summary>
        public static Sprite ResolveDiscardingSprite(CharacterData data, Sprite discardSpriteFallback, Sprite normalSpriteFallback, bool isDiscarding)
        {
            Sprite targetDiscardSprite = (data != null && data.discardSprite != null) ? data.discardSprite : discardSpriteFallback;
            Sprite targetNormalSprite = (data != null && data.normalSprite != null) ? data.normalSprite : normalSpriteFallback;
            return isDiscarding ? targetDiscardSprite : targetNormalSprite;
        }
    }

    internal static class ReadyBoxUtil
    {
        /// <summary>
        /// 「準備完了」の札を実行時に組み直して返す（無ければ作る）。
        /// EnsureReadyBadge の中身。呼び出し側で anchor / isSelf が違うだけで、遅延初期化の処理自体は同じ。
        /// </summary>
        public static ReadyBadge EnsureBadge(ref ReadyBadge badge, GameObject readyBoxContainer, GameObject readyCheckImage, RectTransform anchor, bool isSelf)
        {
            if (badge == null)
            {
                badge = ReadyBadge.Attach(readyBoxContainer, readyCheckImage, anchor, isSelf);
            }
            return badge;
        }

        /// <summary>ShowReadyBox の中身。札があれば隠し、無ければ旧UIを直接消す。</summary>
        public static void HideReadyBox(ReadyBadge badge, GameObject readyBoxContainer, GameObject readyCheckImage)
        {
            if (badge != null)
            {
                badge.SetVisible(false);
                return;
            }

            if (readyBoxContainer != null) readyBoxContainer.SetActive(false);
            if (readyCheckImage != null) readyCheckImage.SetActive(false);
        }
    }
}
