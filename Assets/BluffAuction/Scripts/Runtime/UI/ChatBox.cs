using System;
using System.Collections.Generic;
using BluffAuction.Game;
using BluffAuction.Loc;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    /// <summary>
    /// 채팅 상자 (게임 화면 왼쪽 아래 · 대기실): 최근 글 몇 줄 + Enter 로 여는 입력 칸 + 마이크 상태.
    /// 글은 잠시 뒤 흐려지고, 입력 칸을 열면 다시 다 보인다.
    /// </summary>
    public sealed class ChatBox
    {
        public readonly VisualElement Root;
        readonly VisualElement lines;
        readonly TextField input;
        readonly VisualElement micIcon, micMeter, micFill;
        readonly Label micLabel;
        readonly Func<int, string> nameOf;
        readonly Func<int, Color> colorOf;
        readonly List<Line> shown = new List<Line>();
        const int MaxLines = 9;
        public bool Open { get; private set; }
        /// <summary>새 글이 왔을 때 (말풍선 띄우기 등).</summary>
        public Action<int, string> OnLine;

        sealed class Line { public VisualElement El; public float At; }

        public ChatBox(Func<int, string> nameOf, Func<int, Color> colorOf, bool alwaysVisible)
        {
            this.nameOf = nameOf;
            this.colorOf = colorOf;
            this.alwaysVisible = alwaysVisible;
            Root = Ui.Col("chatbox");
            Root.pickingMode = PickingMode.Ignore;

            // 마이크: 아이콘 + 상태 + 소리 크기
            var mic = Ui.Row("chat-mic");
            mic.pickingMode = PickingMode.Ignore;
            micIcon = Ui.Div("mic-icon");
            micLabel = Ui.Text("", "chat-mic-label");
            micMeter = Ui.Div("mic-meter");
            micFill = Ui.Div("mic-fill");
            micMeter.Add(micFill);
            mic.Add(micIcon, micLabel, micMeter);
            Root.Add(mic);

            lines = Ui.Col("chat-lines");
            lines.pickingMode = PickingMode.Ignore;
            Root.Add(lines);

            input = new TextField { maxLength = SocialHub.MaxChat };
            input.AddToClassList("chat-input");
            input.textEdition.placeholder = L.T("chat_placeholder");
            input.Show(false);
            input.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            // 다른 곳을 눌러 칸이 닫혀도 쓰던 글은 남겨 둔다 (다시 열면 이어 쓴다)
            input.RegisterCallback<FocusOutEvent>(e => { if (Open && !sending) Close(false); });
            Root.Add(input);
            var hint = Ui.Text(L.T("chat_hint"), "chat-hint");
            hint.pickingMode = PickingMode.Ignore;
            Root.Add(hint);

            if (SocialHub.I != null) SocialHub.I.ChatShown += Add;
            // Enter 는 입력 이벤트로 받는다: 50ms 마다 도는 Tick 에서 '이번 프레임에 눌림'을 보면 대부분 놓친다
            enterWatch = InputSystem.onAnyButtonPress.Call(c =>
            {
                var kb = Keyboard.current;
                if (kb != null && (c == kb.enterKey || c == kb.numpadEnterKey)) enterPressed = true;
            });
            Root.RegisterCallback<DetachFromPanelEvent>(e =>
            {
                if (SocialHub.I != null) SocialHub.I.ChatShown -= Add;
                if (enterWatch != null) enterWatch.Dispose();
                enterWatch = null;
                if (Open) Close(false);
            });
            Root.schedule.Execute(Tick).Every(30);
        }

        readonly bool alwaysVisible;
        IDisposable enterWatch;
        bool enterPressed, sending;
        float closedAt = -10f;
        string draft = "";

        void OnKey(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                e.StopPropagation();
                // 한글 입력기(IME)가 조합 중인 마지막 글자를 칸에 넣을 틈을 주고 보낸다
                if (sending) return;
                sending = true;
                input.schedule.Execute(() => { sending = false; Close(true); }).StartingIn(60);
            }
            else if (e.keyCode == KeyCode.Escape)
            {
                e.StopPropagation();
                Cancel();
            }
        }

        /// <summary>쓰던 글을 버리고 닫는다 (Esc).</summary>
        public void Cancel()
        {
            draft = "";
            input.value = "";
            Close(false);
        }

        public void OpenInput()
        {
            if (Open) return;
            Open = true;
            input.value = draft;
            input.Show(true);
            if (SocialHub.I != null) SocialHub.I.SetTyping(true);
            Input.imeCompositionMode = IMECompositionMode.On;
            input.schedule.Execute(() =>
            {
                if (!Open) return;
                input.Focus();
                input.SelectRange(input.value.Length, input.value.Length);
            }).StartingIn(10);
        }

        void Close(bool send)
        {
            if (!Open) return;
            Open = false;
            closedAt = Time.unscaledTime;
            string text = input.value ?? "";
            // 입력기가 아직 쥐고 있는 조합 글자도 함께
            string comp = Input.compositionString;
            if (send && !string.IsNullOrEmpty(comp) && !text.EndsWith(comp)) text += comp;
            draft = send ? "" : text;
            input.value = "";
            input.Show(false);
            input.Blur();
            Input.imeCompositionMode = IMECompositionMode.Auto;
            if (SocialHub.I != null) SocialHub.I.SetTyping(false);
            if (send && !string.IsNullOrWhiteSpace(text) && SocialHub.I != null) SocialHub.I.SendChat(text);
        }

        public void Add(int seat, string text)
        {
            var row = Ui.Row("chat-line");
            row.pickingMode = PickingMode.Ignore;
            var name = Ui.Text(nameOf(seat), "chat-name");
            name.style.color = Color.Lerp(colorOf(seat), Color.white, 0.25f);
            var body = Ui.Text(text, "chat-text");
            row.Add(name, body);
            lines.Add(row);
            shown.Add(new Line { El = row, At = Time.unscaledTime });
            while (shown.Count > MaxLines)
            {
                shown[0].El.RemoveFromHierarchy();
                shown.RemoveAt(0);
            }
            Ui.FadeIn(row, 0.2f, 6f);
            if (OnLine != null) OnLine(seat, text);
        }

        void Tick()
        {
            // Enter 로 열기 (다른 글 칸에 있지 않을 때 · 방금 보낸 그 Enter 로 다시 열리지 않게)
            if (enterPressed)
            {
                enterPressed = false;
                // 창이 뒤에 있을 때 다른 프로그램에서 누른 Enter 로 열리지 않게
                bool focused = Application.isFocused || Dev.AutoShots.Requested && Dev.AutoShots.Scenario == "social";
                if (!Open && !sending && focused && Time.unscaledTime - closedAt > 0.1f && !UiRoot.I.TextFocused && !UiRoot.I.ModalOpen && Root.panel != null && Root.resolvedStyle.display == DisplayStyle.Flex)
                    OpenInput();
            }
            float now = Time.unscaledTime;
            foreach (var l in shown)
            {
                float age = now - l.At;
                l.El.style.opacity = Open || alwaysVisible ? 1f : Mathf.Clamp01(1f - (age - 12f) / 3f);
            }
            var hub = SocialHub.I;
            if (hub == null) return;
            var mic = hub.Mic;
            bool on = mic.MicOn || mic.PushHeld;
            micIcon.EnableInClassList("mic-on", on);
            micIcon.EnableInClassList("mic-talking", mic.Transmitting);
            micLabel.text = mic.MicOn ? L.T("mic_on") : (mic.PushHeld ? L.T("mic_ptt") : L.T("mic_off"));
            micFill.style.width = Length.Percent(Mathf.Clamp01(mic.Level) * 100f);
            micMeter.Show(on);
        }
    }
}
