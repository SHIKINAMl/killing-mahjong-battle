using System;
using System.Collections;
using KillingMahjong.EngineData;
using KillingMahjong.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KillingMahjong.UI
{
    public partial class TitleMultiMenuUI
    {
        private GameObject _cpuPanel;
        private readonly Button[] _cpuChoices = new Button[4];
        private Button _cpuBack;
        private TMP_Text _cpuStatus, _cpuError;
        private CpuDifficulty _cpuDifficulty = CpuDifficulty.Normal;
        private bool _cpuChoiceArmed;
        private CpuMatchJoinGate _cpuAttempt;
        private WebSocketGameClientSample _cpuJoinClient;
        private Action<GameStartedData> _cpuStartedHandler;
        private Action<string> _cpuErrorHandler, _cpuConnectionHandler;

        private void BuildCpuPanel()
        {
            _cpuPanel = SceneFirst.Child(_root.transform, "CpuPanel", out bool made, typeof(RectTransform));
            if (made) Center((RectTransform)_cpuPanel.transform, new Vector2(ItemWidth, 470));
            CreateHeading(_cpuPanel.transform, "CPUの強さ", 155);
            for (int i = 0; i < _cpuChoices.Length; i++)
            {
                var difficulty = (CpuDifficulty)i;
                var go = CreateMenuItem(_cpuPanel.transform, CpuDifficultyCatalog.Label(difficulty), 90 - i * 51,
                    () => OnCpuDifficultySelected(difficulty));
                _cpuChoices[i] = go.GetComponent<Button>();
            }
            CpuText("SpecialHint", "CPUに特典あり", -63, 14, new Color32(200, 188, 196, 255));
            // 旧ボタンは配置データに残すが、表示・入力には使わない。
            var previousStart = _cpuPanel.transform.Find("対戦する");
            if (previousStart != null)
            {
                var button = previousStart.GetComponent<Button>();
                if (button != null) { button.onClick.RemoveAllListeners(); button.interactable = false; }
                previousStart.gameObject.SetActive(false);
            }
            _cpuBack = CreateMenuItem(_cpuPanel.transform, "もどる", -140, OnCpuBackSelected).GetComponent<Button>();
            _cpuStatus = CpuText("Status", "", 123, 14, MenuText);
            _cpuError = CpuText("Error", "", -248, 14, ErrorText);
            SelectCpuDifficulty(CpuDifficulty.Normal);
            _cpuPanel.SetActive(false);
        }
        private TMP_Text CpuText(string name, string text, float y, float fontSize, Color color)
        {
            var go = SceneFirst.Child(_cpuPanel.transform, name, out bool made, typeof(RectTransform), typeof(TextMeshProUGUI));
            var label = go.GetComponent<TMP_Text>();
            if (made)
            {
                Center((RectTransform)go.transform, new Vector2(name == "Error" ? 420 : name == "SpecialHint" ? 140 : ItemWidth, name == "Error" ? 40 : 22));
                ((RectTransform)go.transform).anchoredPosition = new Vector2(name == "SpecialHint" ? 70 : 0, y);
                ApplyFont(label); label.text = text; label.fontSize = fontSize; label.color = color;
                label.alignment = name == "SpecialHint" ? TextAlignmentOptions.Left : TextAlignmentOptions.Center; label.raycastTarget = false;
            }
            return label;
        }
        private void ShowCpuPanel()
        {
            if (_isCheckingRoom || _hasDecided) return;
            _modePanel.SetActive(false); _friendPanel.SetActive(false); _passwordPanel.SetActive(false);
            _cpuPanel.SetActive(true); _cpuError.text = _cpuStatus.text = "";
            SelectCpuDifficulty(CpuDifficulty.Normal); SetCpuBusy(false);
        }
        private void OnCpuDifficultySelected(CpuDifficulty difficulty)
        {
            if (TryConfirmCpuDifficulty(difficulty)) OnCpuStartSelected();
        }
        private bool TryConfirmCpuDifficulty(CpuDifficulty difficulty)
        {
            if (_isCheckingRoom || _hasDecided || _cpuPanel == null || !_cpuPanel.activeInHierarchy) return false;
            CpuDifficultyCatalog.WireName(difficulty);
            if (_cpuChoiceArmed && _cpuDifficulty == difficulty)
            {
                _cpuChoiceArmed = false;
                return true;
            }
            SelectCpuDifficulty(difficulty);
            _cpuChoiceArmed = true;
            return false;
        }
        private void SelectCpuDifficulty(CpuDifficulty difficulty)
        {
            if (_isCheckingRoom) return;
            CpuDifficultyCatalog.WireName(difficulty); _cpuDifficulty = difficulty; _cpuChoiceArmed = false;
            for (int i = 0; i < _cpuChoices.Length; i++)
                if (_cpuChoices[i] != null) UIInteractionFeedback.Attach(_cpuChoices[i].gameObject).SetSelected(i == (int)difficulty);
        }
        private void SetCpuBusy(bool busy)
        {
            foreach (var choice in _cpuChoices) if (choice != null) choice.interactable = !busy;
            if (_cpuBack != null) _cpuBack.interactable = true;
        }
        private void OnCpuBackSelected()
        {
            CancelCpuJoin();
            if (_cpuStatus != null) _cpuStatus.text = "";
            if (_cpuError != null) _cpuError.text = "";
            ShowModePanel();
        }
        private void OnCpuStartSelected()
        {
            if (_isCheckingRoom || _hasDecided || _cpuPanel == null || !_cpuPanel.activeInHierarchy) return;
            _cpuChoiceArmed = false;
            MatchJoinRequest.RequestCpu(_cpuDifficulty);
            _isCheckingRoom = true;
            _cpuAttempt = new CpuMatchJoinGate(_cpuDifficulty, Time.realtimeSinceStartup);
            StartCoroutine(JoinCpuRoutine(_cpuAttempt));
        }
        private IEnumerator JoinCpuRoutine(CpuMatchJoinGate attempt)
        {
            _cpuError.text = ""; _cpuStatus.text = "CPU対戦を準備しています..."; SetCpuBusy(true);
            var client = EnsureClient(); _cpuJoinClient = client;
            if (client == null) attempt.Fail("通信クライアントを用意できませんでした");
            else
            {
                client.UseManualStartup();
                _cpuStartedHandler = attempt.Accept;
                _cpuErrorHandler = attempt.Fail; _cpuConnectionHandler = attempt.Fail;
                client.OnMatchStartedData += _cpuStartedHandler;
                client.OnServerError += _cpuErrorHandler; client.OnConnectionError += _cpuConnectionHandler;
                // 既存の接続は同じ入口でjoin。新規接続はconnectedの応答で1回だけ送られる。
                if (client.IsConnected) client.RequestRejoin();
                else { _ = client.ConnectAsync(); }
            }
            while (attempt.IsPending)
            {
                if (_cpuAttempt != attempt) yield break;
                attempt.Tick(Time.realtimeSinceStartup);
                yield return null;
            }
            if (_cpuAttempt != attempt) yield break;
            UnsubscribeCpuClient(); _cpuAttempt = null; _isCheckingRoom = false;
            _cpuStatus.text = ""; SetCpuBusy(false);
            if (attempt.State == CpuJoinState.Matched)
            {
                // 確定した接続と受信キューは対局シーンへそのまま持ち越す。
                Decide(MatchJoinMode.Cpu); yield break;
            }
            if (client != null) { _ = client.ResetConnectionAsync(); }
            MatchJoinRequest.RequestPublic();
            if (attempt.State == CpuJoinState.Failed) _cpuError.text = TranslateCpuJoinError(attempt.Error);
        }
        private static string TranslateCpuJoinError(string message)
        {
            if (string.IsNullOrEmpty(message)) return "CPU対戦を開始できませんでした";
            if (message.Contains("Unsupported join mode")) return "このサーバーはCPU対戦に未対応です";
            if (message.Contains("Unsupported difficulty")) return "このサーバーでは選んだ強さを使えません";
            return TranslateJoinError(message);
        }
        private void UnsubscribeCpuClient()
        {
            if (_cpuJoinClient != null)
            {
                if (_cpuStartedHandler != null) _cpuJoinClient.OnMatchStartedData -= _cpuStartedHandler;
                if (_cpuErrorHandler != null) _cpuJoinClient.OnServerError -= _cpuErrorHandler;
                if (_cpuConnectionHandler != null) _cpuJoinClient.OnConnectionError -= _cpuConnectionHandler;
            }
            _cpuStartedHandler = null; _cpuErrorHandler = _cpuConnectionHandler = null; _cpuJoinClient = null;
        }
        private void CancelCpuJoin()
        {
            _cpuChoiceArmed = false;
            if (_cpuAttempt == null) return;
            var attempt = _cpuAttempt; var client = _cpuJoinClient;
            attempt.Cancel(); UnsubscribeCpuClient(); _cpuAttempt = null; _isCheckingRoom = false; SetCpuBusy(false);
            if (attempt.State != CpuJoinState.Matched)
            {
                if (client != null) { _ = client.ResetConnectionAsync(); }
                MatchJoinRequest.RequestPublic();
            }
        }
        private void OnDisable() { CancelCpuJoin(); }
        private void OnDestroy() { CancelCpuJoin(); }
    }
}
