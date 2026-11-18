using System;
using System.Collections.Generic;
using BluffAuction.AI;
using BluffAuction.Audio;
using BluffAuction.Characters;
using BluffAuction.Game;
using BluffAuction.Loc;
using UnityEngine;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    // ═════════════════ 타이틀 ═════════════════

    public sealed class TitleScreen
    {
        public readonly VisualElement Root;

        public TitleScreen(GameApp app)
        {
            Root = Ui.Row("vignette");
            Root.style.paddingLeft = 110;
            Root.style.paddingTop = 80;
            Root.style.paddingBottom = 60;
            Root.style.alignItems = Align.Stretch;

            var left = Ui.Col();
            left.style.justifyContent = Justify.Center;
            var logo = Ui.Col();
            logo.style.alignItems = Align.FlexStart;
            logo.style.marginBottom = 40;
            var t = Ui.Text("BLUFF AUCTION", "h1");
            t.style.fontSize = 96;
            t.style.unityTextAlign = TextAnchor.MiddleLeft;
            var line = Ui.Div("divider");
            line.style.width = 560;
            var sub = Ui.Text(L.T("title") + "  ·  " + L.T("title_sub"), "h3");
            logo.Add(t, line, sub);
            left.Add(logo);

            if (app.HasLastSetup) left.Add(MenuButton(L.T("menu_continue"), "", () => app.Rematch()));
            left.Add(MenuButton(L.T("menu_play"), L.T("menu_play_sub"), () => app.GoSetup()));
            left.Add(MenuButton(L.T("menu_online"), L.T("menu_online_sub"), () => app.GoOnline()));
            left.Add(MenuButton(L.T("cz_title"), "", () => app.GoCustomize(() => app.GoTitle())));
            left.Add(MenuButton(L.T("menu_howto"), "", () => app.GoHowTo(() => app.GoTitle())));
            left.Add(MenuButton(L.T("menu_settings"), "", () => app.GoSettings(() => app.GoTitle())));
            left.Add(MenuButton(L.T("menu_records"), "", () => app.GoRecords(() => app.GoTitle())));
            left.Add(MenuButton(L.T("menu_quit"), "", () => UiRoot.I.Confirm(L.T("quit_confirm"), null, L.T("menu_quit"), Quit, L.T("cancel"))));
            Root.Add(left);
            Root.Add(Ui.Div("spacer"));
            var corner = Ui.Col();
            corner.style.justifyContent = Justify.FlexEnd;
            corner.style.paddingRight = 40;
            corner.Add(Ui.Text("v" + Application.version, "tiny", "right-text"));
            Root.Add(corner);
        }

        static void Quit()
        {
            SaveData.Save();
            Application.Quit();
        }

        public static Button MenuButton(string title, string subtitle, Action onClick)
        {
            var b = new Button();
            b.AddToClassList("menu-btn");
            b.Add(Ui.Text(title, "menu-btn-title"));
            if (!string.IsNullOrEmpty(subtitle)) b.Add(Ui.Text(subtitle, "menu-btn-sub"));
            Ui.Wire(b, onClick, "ui_confirm");
            return b;
        }
    }

    // ═════════════════ 새 게임 ═════════════════

    public sealed class SetupScreen
    {
        public readonly VisualElement Root;
        readonly GameApp app;
        readonly LastSetup last;
        readonly List<VisualElement> rivalCards = new List<VisualElement>();
        Label rivalsLabel, estimate, diffDesc;
        readonly List<Button> diffButtons = new List<Button>();

        public SetupScreen(GameApp app)
        {
            this.app = app;
            last = SaveData.I.Last;
            if (last.RivalIds == null) last.RivalIds = new List<string>();
            Root = Ui.Col();
            Root.style.paddingLeft = 80;
            Root.style.paddingRight = 80;
            Root.style.paddingTop = 50;
            Root.style.paddingBottom = 40;
            Root.AddToClassList("dim-bg");

            var head = Ui.Row();
            head.Add(Ui.Btn("◀ " + L.T("back"), () => app.GoTitle(), "btn-ghost"));
            var title = Ui.Text(L.T("setup_title"), "h2");
            title.style.marginLeft = 20;
            head.Add(title);
            Root.Add(head);

            var body = Ui.Row();
            body.style.alignItems = Align.Stretch;
            body.style.flexGrow = 1;
            body.style.marginTop = 20;
            Root.Add(body);

            // ── 왼쪽: 상대 고르기 + 난이도
            var left = Ui.Col("panel");
            left.style.flexGrow = 1;
            left.style.marginRight = 20;
            var rh = Ui.Row();
            rh.Add(Ui.Text(L.T("setup_rivals"), "h3"));
            rivalsLabel = Ui.Text("", "sub");
            rivalsLabel.style.marginLeft = 14;
            rh.Add(rivalsLabel, Ui.Div("spacer"));
            rh.Add(Ui.Btn("−", () => ChangeCount(-1), "btn-icon"), Ui.Btn("+", () => ChangeCount(1), "btn-icon"));
            rh.Add(Ui.Btn(L.T("setup_random_rivals"), RandomRivals, "btn-small", "btn-ghost"));
            left.Add(rh);
            var grid = Ui.Row();
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 10;
            foreach (var p in AiProfiles.All)
            {
                var id = p.Id;
                var card = Ui.Row("rival");
                var look = CharacterLook.Guests[p.CharacterId];
                card.Add(Ui.Portrait(look.Id, 86));
                var info = Ui.Col();
                info.style.flexGrow = 1;
                info.style.flexShrink = 1;
                info.style.marginLeft = 10;
                info.Add(Ui.Text(L.T("ai_" + id), "rival-name"));
                var bio = Ui.Text(L.T("ai_" + id + "_bio"), "rival-bio");
                bio.style.marginTop = 4;
                info.Add(bio);
                card.Add(info);
                card.RegisterCallback<ClickEvent>(e => ToggleRival(id));
                card.userData = id;
                rivalCards.Add(card);
                grid.Add(card);
            }
            left.Add(grid);
            left.Add(Ui.Div("divider"));
            left.Add(Ui.Text(L.T("setup_difficulty"), "h3"));
            var dr = Ui.Row();
            for (int d = 0; d < 3; d++)
            {
                int dd = d;
                var b = Ui.Btn(L.T("diff_" + d), () => SetDifficulty(dd), "btn-chip");
                diffButtons.Add(b);
                dr.Add(b);
            }
            diffDesc = Ui.Text("", "small");
            diffDesc.style.marginLeft = 14;
            dr.Add(diffDesc);
            left.Add(dr);
            body.Add(left);

            // ── 오른쪽: 나 + 규칙
            var right = Ui.Col("panel");
            right.style.width = 620;
            right.Add(Ui.Text(L.T("setup_you"), "h3"));
            var nameRow = Ui.Row("option-row");
            nameRow.Add(Ui.Text(L.T("setup_name"), "option-label"));
            var nameField = new TextField { value = last.Name, maxLength = 16 };
            nameField.textEdition.placeholder = L.T("name_placeholder");
            nameField.style.width = 300;
            nameField.RegisterValueChangedCallback(e => last.Name = e.newValue);
            nameRow.Add(nameField);
            right.Add(nameRow);
            // 내 생김새: 얼굴 사진 + 꾸미기 화면으로
            var lookRow = Ui.Row("option-row");
            lookRow.Add(Ui.Text(L.T("setup_look"), "option-label"), Ui.Div("spacer"));
            lookRow.Add(Ui.Portrait(SaveData.MyLook, 64));
            var cz = Ui.Btn(L.T("cz_open"), () => app.GoCustomize(() => app.GoSetup()), "btn-small");
            cz.style.marginLeft = 12;
            lookRow.Add(cz);
            right.Add(lookRow);
            right.Add(Ui.Div("divider"));
            right.Add(Ui.Text(L.T("setup_rules"), "h3"));
            right.Add(Stepper(L.T("setup_items"), () => last.Items.ToString(), d => last.Items = Mathf.Clamp(last.Items + d, 2, 4)));
            right.Add(Stepper(L.T("setup_bidtime"), () => L.T("seconds", last.BidSeconds), d => last.BidSeconds = Mathf.Clamp(last.BidSeconds + d * 5, 15, 90)));
            right.Add(Stepper(L.T("setup_extend"), () => last.Extend == 0 ? L.T("off") : L.T("seconds", last.Extend), d => last.Extend = Mathf.Clamp(last.Extend + d, 0, 10)));
            right.Add(Toggle(L.T("setup_loan"), () => last.Loan, v => last.Loan = v));
            right.Add(Toggle(L.T("setup_blind"), () => last.Blind, v => last.Blind = v));
            right.Add(Ui.Div("spacer"));
            estimate = Ui.Text("", "small", "center-text");
            right.Add(estimate);
            var start = Ui.Btn(L.T("setup_start"), Start, "btn-big");
            right.Add(start);
            body.Add(right);

            RefreshRivals();
            SetDifficulty(last.Difficulty);
            schedule = Root.schedule.Execute(UpdateEstimate).Every(250);
        }

        IVisualElementScheduledItem schedule;

        void ChangeCount(int d)
        {
            last.Rivals = Mathf.Clamp(last.Rivals + d, 2, 7);
            while (last.RivalIds.Count > last.Rivals) last.RivalIds.RemoveAt(last.RivalIds.Count - 1);
            RefreshRivals();
        }

        void ToggleRival(string id)
        {
            if (last.RivalIds.Contains(id)) last.RivalIds.Remove(id);
            else
            {
                if (last.RivalIds.Count >= last.Rivals)
                {
                    if (last.Rivals < 7) last.Rivals++;
                    else last.RivalIds.RemoveAt(0);
                }
                last.RivalIds.Add(id);
            }
            if (AudioManager.I != null) AudioManager.I.Play("ui_toggle", 0.8f, 1f, Bus.Ui);
            RefreshRivals();
        }

        void RandomRivals()
        {
            last.RivalIds.Clear();
            RefreshRivals();
        }

        void RefreshRivals()
        {
            rivalsLabel.text = L.T("setup_rivals_count", last.Rivals) + (last.RivalIds.Count < last.Rivals ? "  (" + L.T("random") + " " + (last.Rivals - last.RivalIds.Count) + ")" : "");
            foreach (var c in rivalCards)
            {
                bool on = last.RivalIds.Contains((string)c.userData);
                c.EnableInClassList("rival-on", on);
            }
        }

        void SetDifficulty(int d)
        {
            last.Difficulty = Mathf.Clamp(d, 0, 2);
            for (int i = 0; i < diffButtons.Count; i++) diffButtons[i].EnableInClassList("btn-chip-on", i == last.Difficulty);
            diffDesc.text = L.T("diff_" + last.Difficulty + "_desc");
        }

        void UpdateEstimate()
        {
            int players = last.Rivals + 1;
            int lots = players * last.Items;
            float min = lots * (last.BidSeconds * 0.8f + 9f) / 60f;
            estimate.text = L.T("setup_estimate", Mathf.RoundToInt(min));
        }

        void Start()
        {
            SaveData.Save();
            app.StartGame(GameApp.BuildSetup(last));
        }

        public static VisualElement Stepper(string label, Func<string> value, Action<int> change, bool ink = false)
        {
            var row = Ui.Row("option-row");
            row.Add(Ui.Text(label, ink ? "option-label-ink" : "option-label"));
            var st = Ui.Row("stepper");
            Label v = Ui.Text(value(), ink ? "stepper-value" : "stepper-value-light");
            st.Add(Ui.Btn("−", () => { change(-1); v.text = value(); Ui.Pop(v); }, "btn-icon", ink ? "btn-secondary" : ""));
            st.Add(v);
            st.Add(Ui.Btn("+", () => { change(1); v.text = value(); Ui.Pop(v); }, "btn-icon", ink ? "btn-secondary" : ""));
            row.Add(st);
            return row;
        }

        public static VisualElement Toggle(string label, Func<bool> get, Action<bool> set)
        {
            var row = Ui.Row("option-row");
            row.Add(Ui.Text(label, "option-label"));
            Button b = null;
            b = Ui.Btn(get() ? L.T("on") : L.T("off"), () =>
            {
                set(!get());
                b.text = get() ? L.T("on") : L.T("off");
                b.EnableInClassList("btn-chip-on", get());
            }, "btn-chip");
            b.EnableInClassList("btn-chip-on", get());
            b.style.minWidth = 110;
            row.Add(b);
            return row;
        }

        public static VisualElement Slider(string label, Func<float> get, Action<float> set, float min = 0f, float max = 1f)
        {
            var row = Ui.Row("option-row");
            row.Add(Ui.Text(label, "option-label"));
            var s = new Slider(min, max) { value = get() };
            s.style.width = 300;
            var pct = Ui.Text(Mathf.RoundToInt(get() * 100) + "%", "small");
            pct.style.width = 56;
            pct.style.unityTextAlign = TextAnchor.MiddleRight;
            s.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                pct.text = Mathf.RoundToInt(e.newValue * 100) + "%";
                if (AudioManager.I != null) AudioManager.I.Play("ui_slider", 0.5f, 0.8f + e.newValue * 0.5f, Bus.Ui);
            });
            var r = Ui.Row();
            r.Add(s, pct);
            row.Add(r);
            return row;
        }
    }

    // ═════════════════ 설정 ═════════════════

    public sealed class SettingsScreen
    {
        public readonly VisualElement Root;

        public SettingsScreen(GameApp app, Action back)
        {
            var s = SaveData.I.Settings;
            Root = Ui.Col("dim-bg");
            Root.style.alignItems = Align.Center;
            Root.style.paddingTop = 50;
            Root.style.paddingBottom = 40;
            var panel = Ui.Col("panel");
            panel.style.width = 900;
            panel.style.flexGrow = 1;
            var head = Ui.Row();
            head.Add(Ui.Btn("◀ " + L.T("back"), () => { SaveData.Save(); back(); }, "btn-ghost"));
            var title = Ui.Text(L.T("menu_settings"), "h2");
            title.style.marginLeft = 20;
            head.Add(title, Ui.Div("spacer"));
            head.Add(Ui.Btn(L.T("set_reset"), () =>
            {
                SaveData.I.Settings = new SettingsData();
                app.ApplySettings();
                app.ApplyDisplay();
                UiRoot.I.SetScreen(new SettingsScreen(app, back).Root);
            }, "btn-small", "btn-ghost"));
            panel.Add(head);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 10;
            panel.Add(scroll);

            // 언어
            scroll.Add(Section(L.T("set_language")));
            var langRow = Ui.Row();
            langRow.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < L.LanguageNames.Length; i++)
            {
                int li = i;
                var b = Ui.Btn(L.LanguageNames[i], () =>
                {
                    s.Language = li;
                    app.ApplySettings();
                    SaveData.Save();
                    UiRoot.I.SetScreen(new SettingsScreen(app, back).Root);
                }, "btn-chip");
                b.EnableInClassList("btn-chip-on", (int)L.Current == i);
                langRow.Add(b);
            }
            scroll.Add(langRow);

            // 소리
            scroll.Add(Section(L.T("set_audio")));
            scroll.Add(SetupScreen.Slider(L.T("set_master"), () => s.Master, v => s.Master = v));
            scroll.Add(SetupScreen.Slider(L.T("set_music"), () => s.Music, v => s.Music = v));
            scroll.Add(SetupScreen.Slider(L.T("set_sfx"), () => s.Sfx, v => s.Sfx = v));
            scroll.Add(SetupScreen.Slider(L.T("set_voice"), () => s.Voice, v => s.Voice = v));
            scroll.Add(SetupScreen.Slider(L.T("set_ambience"), () => s.Ambience, v => s.Ambience = v));

            // 음성 채팅 · 어울리기
            scroll.Add(Section(L.T("set_social")));
            scroll.Add(SetupScreen.Stepper(L.T("set_mic_mode"), () => L.T(s.MicMode == 1 ? "set_mic_open" : "set_mic_ptt"), d => s.MicMode = s.MicMode == 1 ? 0 : 1));
            var devices = new List<string> { "" };
            devices.AddRange(Net.VoiceCapture.Devices);
            int di = Mathf.Max(0, devices.IndexOf(s.MicDevice ?? ""));
            scroll.Add(SetupScreen.Stepper(L.T("set_mic_device"), () => devices[di].Length == 0 ? L.T("set_mic_default") : Short(devices[di]), d =>
            {
                di = (di + d + devices.Count) % devices.Count;
                s.MicDevice = devices[di];
            }));
            scroll.Add(SetupScreen.Slider(L.T("set_mic_threshold"), () => Mathf.InverseLerp(0.1f, 0.005f, s.MicThreshold), v => s.MicThreshold = Mathf.Lerp(0.1f, 0.005f, v)));
            // 마이크 시험: 켜면 내 목소리가 그대로 들리고 크기 막대가 움직인다
            var testRow = Ui.Row("option-row");
            testRow.Add(Ui.Text(L.T("set_mic_test"), "option-label"));
            var meter = Ui.Div("mic-meter");
            meter.style.width = 200;
            var fill = Ui.Div("mic-fill");
            meter.Add(fill);
            var hub = SocialHub.I;
            Button testBtn = null;
            testBtn = Ui.Btn(L.T("off"), () =>
            {
                if (hub == null) return;
                hub.SetMicTest(!hub.Testing);
                testBtn.text = hub.Testing ? L.T("on") : L.T("off");
                testBtn.EnableInClassList("btn-chip-on", hub.Testing);
            }, "btn-chip");
            testBtn.style.minWidth = 110;
            var tr = Ui.Row();
            tr.style.alignItems = Align.Center;
            meter.style.marginRight = 12;
            tr.Add(meter, testBtn);
            testRow.Add(tr);
            scroll.Add(testRow);
            testRow.schedule.Execute(() =>
            {
                if (hub == null) return;
                fill.style.width = Length.Percent(Mathf.Clamp01(hub.Mic.Level) * 100f);
                fill.style.backgroundColor = hub.Mic.Transmitting ? new Color(0.43f, 1f, 0.66f) : new Color(1f, 1f, 1f, 0.35f);
            }).Every(50);
            testRow.RegisterCallback<DetachFromPanelEvent>(e => { if (hub != null) hub.SetMicTest(false); });
            scroll.Add(SetupScreen.Slider(L.T("set_chat_volume"), () => s.ChatVolume, v => s.ChatVolume = v));
            scroll.Add(SetupScreen.Toggle(L.T("set_show_rude"), () => s.ShowRude, v => s.ShowRude = v));
            scroll.Add(SetupScreen.Toggle(L.T("set_show_images"), () => s.ShowCustomImages, v => s.ShowCustomImages = v));
            if (devices.Count == 1) scroll.Add(Ui.Text(L.T("set_mic_none"), "small", "wrap"));

            // 화면
            scroll.Add(Section(L.T("set_display")));
            scroll.Add(SetupScreen.Toggle(L.T("set_fullscreen"), () => s.Fullscreen, v => { s.Fullscreen = v; app.ApplyDisplay(); }));
            var resolutions = DistinctResolutions();
            int ri = Mathf.Max(0, resolutions.FindIndex(r => r.x == (s.ResWidth > 0 ? s.ResWidth : Screen.width) && r.y == (s.ResHeight > 0 ? s.ResHeight : Screen.height)));
            if (resolutions.Count > 0)
            {
                scroll.Add(SetupScreen.Stepper(L.T("set_resolution"), () => resolutions[ri].x + " × " + resolutions[ri].y, d =>
                {
                    ri = Mathf.Clamp(ri + d, 0, resolutions.Count - 1);
                    s.ResWidth = resolutions[ri].x;
                    s.ResHeight = resolutions[ri].y;
                    app.ApplyDisplay();
                }));
            }
            scroll.Add(SetupScreen.Toggle(L.T("set_vsync"), () => s.VSync, v => { s.VSync = v; app.ApplySettings(); }));
            scroll.Add(SetupScreen.Stepper(L.T("set_quality"), () => L.T("q_" + s.Quality), d => { s.Quality = Mathf.Clamp(s.Quality + d, 0, 2); app.ApplySettings(); }));
            scroll.Add(SetupScreen.Toggle(L.T("set_dof"), () => s.Dof, v => { s.Dof = v; app.ApplySettings(); }));
            scroll.Add(SetupScreen.Toggle(L.T("set_grain"), () => s.Grain, v => { s.Grain = v; app.ApplySettings(); }));

            // 게임
            scroll.Add(Section(L.T("set_gameplay")));
            scroll.Add(SetupScreen.Toggle(L.T("set_mouse_look"), () => s.MouseLook, v => s.MouseLook = v));
            scroll.Add(SetupScreen.Stepper(L.T("set_sensitivity"), () => Mathf.RoundToInt(s.Sensitivity * 100) + "%", d => { s.Sensitivity = Mathf.Clamp(s.Sensitivity + d * 0.1f, 0.2f, 3f); app.Rig.Sensitivity = s.Sensitivity; }));
            scroll.Add(SetupScreen.Toggle(L.T("set_invert"), () => s.InvertY, v => { s.InvertY = v; app.Rig.InvertY = v; }));
            scroll.Add(SetupScreen.Slider(L.T("set_camera"), () => s.CameraMotion, v => { s.CameraMotion = v; app.ApplySettings(); }));
            scroll.Add(SetupScreen.Stepper(L.T("set_ui_scale"), () => Mathf.RoundToInt(s.UiScale * 100) + "%", d => { s.UiScale = Mathf.Clamp(s.UiScale + d * 0.05f, 0.8f, 1.3f); app.ApplySettings(); }));
            scroll.Add(SetupScreen.Toggle(L.T("set_hints"), () => s.Hints, v => s.Hints = v));
            scroll.Add(SetupScreen.Stepper(L.T("set_speed"), () => Mathf.RoundToInt(s.AiSpeed * 100) + "%", d => { s.AiSpeed = Mathf.Clamp(s.AiSpeed + d * 0.25f, 0.5f, 2f); app.ApplySettings(); }));
            Root.Add(panel);
        }

        static string Short(string s) { return s.Length > 26 ? s.Substring(0, 25) + "…" : s; }

        static Label Section(string title)
        {
            var l = Ui.Text(title, "h3");
            l.style.marginTop = 18;
            l.style.marginBottom = 4;
            return l;
        }

        static List<Vector2Int> DistinctResolutions()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (r.width < 1024 || list.Contains(v)) continue;
                list.Add(v);
            }
            return list;
        }
    }

    // ═════════════════ 게임 방법 ═════════════════

    public sealed class HowToScreen
    {
        public readonly VisualElement Root;
        int page;
        const int Pages = 7;
        readonly Label title, body, counter;

        public HowToScreen(Action back)
        {
            Root = Ui.Col("dim-bg");
            Root.style.alignItems = Align.Center;
            Root.style.justifyContent = Justify.Center;
            var panel = Ui.Col("paper");
            panel.style.width = 980;
            panel.style.minHeight = 560;
            panel.style.paddingLeft = panel.style.paddingRight = 50;
            panel.style.paddingTop = 30;
            var inner = Ui.Col("paper-inner");
            inner.style.flexGrow = 1;
            inner.style.alignItems = Align.Center;
            var tag = Ui.Text(L.T("menu_howto").ToUpperInvariant(), "lot-tag");
            title = Ui.Text("", "lot-name");
            title.style.marginTop = 10;
            var rule = Ui.Div("divider");
            rule.style.width = 300;
            body = Ui.Text("", "ink", "wrap");
            body.style.fontSize = 24;
            body.style.marginTop = 16;
            body.style.unityTextAlign = TextAnchor.UpperLeft;
            body.style.flexGrow = 1;
            inner.Add(tag, title, rule, body);
            panel.Add(inner);
            var nav = Ui.Row();
            nav.style.marginTop = 16;
            nav.Add(Ui.Btn("◀ " + L.T("back"), back, "btn-secondary"));
            nav.Add(Ui.Div("spacer"));
            nav.Add(Ui.Btn(L.T("prev"), () => Go(-1), "btn-secondary"));
            counter = Ui.Text("", "ink-soft");
            counter.style.marginLeft = counter.style.marginRight = 14;
            nav.Add(counter);
            nav.Add(Ui.Btn(L.T("next"), () => Go(1)));
            panel.Add(nav);
            Root.Add(panel);
            Go(0);
        }

        void Go(int d)
        {
            page = Mathf.Clamp(page + d, 0, Pages - 1);
            title.text = L.T("howto_" + (page + 1) + "_t");
            body.text = L.T("howto_" + (page + 1));
            counter.text = (page + 1) + " / " + Pages;
            if (d != 0 && AudioManager.I != null) AudioManager.I.Play("sfx_cards", 0.5f, 1.1f, Bus.Ui);
            Ui.FadeIn(body, 0.25f, 10f);
        }
    }

    // ═════════════════ 기록 ═════════════════

    public sealed class RecordsScreen
    {
        public readonly VisualElement Root;

        public RecordsScreen(Action back)
        {
            var st = SaveData.I.Stats;
            Root = Ui.Col("dim-bg");
            Root.style.alignItems = Align.Center;
            Root.style.justifyContent = Justify.Center;
            var panel = Ui.Col("panel");
            panel.style.width = 860;
            var head = Ui.Row();
            head.Add(Ui.Btn("◀ " + L.T("back"), back, "btn-ghost"));
            var t = Ui.Text(L.T("menu_records"), "h2");
            t.style.marginLeft = 20;
            head.Add(t);
            panel.Add(head);
            var grid = Ui.Row();
            grid.style.justifyContent = Justify.SpaceAround;
            grid.style.marginTop = 20;
            grid.Add(Stat(L.T("rec_games"), st.Games.ToString()));
            grid.Add(Stat(L.T("rec_wins"), st.Wins.ToString()));
            grid.Add(Stat(L.T("rec_best"), st.BestScore == int.MinValue ? "-" : L.G(st.BestScore)));
            grid.Add(Stat(L.T("rec_bargain"), st.BestBargain > 0 ? L.Signed(st.BestBargain) : "-"));
            panel.Add(grid);
            panel.Add(Ui.Div("divider"));
            panel.Add(Ui.Text(L.T("rec_achievements"), "h3"));
            foreach (var id in Achievements.All)
            {
                bool got = st.Achievements.Contains(id);
                var row = Ui.Row("player-row");
                row.style.opacity = got ? 1f : 0.45f;
                row.Add(Ui.Text(got ? "★" : "☆", "gold"));
                var name = Ui.Text(L.T("ach_" + id), "player-name");
                name.style.marginLeft = 10;
                row.Add(name, Ui.Text(L.T("ach_" + id + "_d"), "small"));
                panel.Add(row);
            }
            Root.Add(panel);
        }

        static VisualElement Stat(string label, string value)
        {
            var c = Ui.Col();
            c.style.alignItems = Align.Center;
            c.Add(Ui.Text(value, "wallet-big"), Ui.Text(label, "small"));
            return c;
        }
    }
}
