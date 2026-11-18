using System.Collections.Generic;
using BluffAuction.Audio;
using BluffAuction.Core;
using BluffAuction.Game;
using BluffAuction.Loc;
using BluffAuction.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    /// <summary>
    /// 등록 화면: 물건 카드 N 장. 누른 카드의 물건이 탁자 회전판에 올라간다.
    /// 카드마다 물건 모양 · 진짜 가치 · 시작가 · 소문(+ 직접 쓰는 이름·소문). 아래 합계 막대가 딱 맞아야 확정할 수 있다(두 번 눌러 확정).
    /// </summary>
    public sealed class RegistrationPanel
    {
        public readonly VisualElement Root;
        readonly GameApp app;
        Registration draft;
        int n;
        int selected;
        bool armed;
        bool submitted;
        readonly List<Card> cards = new List<Card>();
        VisualElement cardRow, totalFill;
        Label sub, totalText, errorText, feeText;
        Button confirm;
        GameRules rules;

        sealed class Card
        {
            public VisualElement Root;
            public Label Item, Value, Start, Phrase;
            public TextField CustomName, CustomPhrase;
            public VisualElement CustomBox, Thumb;
            public Button ImageRemove;
        }

        // 직접 넣은 그림 (카드 번호 → 줄인 JPG · 미리보기)
        readonly Dictionary<int, byte[]> images = new Dictionary<int, byte[]>();
        readonly Dictionary<int, Texture2D> previews = new Dictionary<int, Texture2D>();

        public RegistrationPanel(GameApp app)
        {
            this.app = app;
            Root = Ui.Col();
            Root.AddToClassList("screen");
            Root.pickingMode = PickingMode.Ignore;
            Root.style.alignItems = Align.Center;
            Root.style.paddingBottom = 26;
            Root.style.paddingTop = 16;

            // 머리 안내는 맨 위, 카드는 아래 — 가운데로 회전판 위 물건이 보인다
            var head = Ui.Col("panel-soft");
            head.style.alignItems = Align.Center;
            head.style.width = 1000;
            head.Add(Ui.Text(L.T("reg_title"), "h2"));
            sub = Ui.Text("", "sub", "wrap", "center-text");
            head.Add(sub);
            var hint = Ui.Text(L.T("reg_hint"), "small", "wrap", "center-text");
            hint.style.marginTop = 4;
            head.Add(hint);
            Root.Add(head);
            var gap = Ui.Div("spacer");
            gap.pickingMode = PickingMode.Ignore;
            Root.Add(gap);

            cardRow = Ui.Row();
            cardRow.style.justifyContent = Justify.Center;
            Root.Add(cardRow);

            var foot = Ui.Col("panel");
            foot.style.alignItems = Align.Center;
            foot.style.width = 1120;
            foot.style.marginTop = 6;
            var totalRow = Ui.Row();
            var track = Ui.Div("reg-total-track");
            totalFill = Ui.Div("reg-total-fill");
            track.Add(totalFill);
            totalText = Ui.Text("", "num");
            totalText.style.marginLeft = 14;
            totalText.style.minWidth = 300;
            totalRow.Add(track, totalText);
            foot.Add(totalRow);
            errorText = Ui.Text("", "small", "red");
            feeText = Ui.Text("", "small", "wrap", "center-text");
            feeText.style.color = Ui.Hex("#f0b070");
            foot.Add(errorText, feeText);
            var actions = Ui.Row();
            actions.Add(Ui.Btn(L.T("reg_balance"), Balance, "btn-secondary"));
            actions.Add(Ui.Btn(L.T("reg_randomize"), Randomize, "btn-secondary"));
            confirm = Ui.Btn(L.T("reg_confirm"), Confirm, "btn-big");
            confirm.style.minWidth = 380;
            actions.Add(confirm);
            foot.Add(actions);
            Root.Add(foot);
        }

        void Init(GameView v)
        {
            rules = v.Rules;
            n = v.Rounds;
            var rng = new Rng(System.Environment.TickCount);
            draft = RegistrationHelper.Random(rules, rng);
            // 처음에는 고르게 나눈다 (무작위 나누기는 버튼으로)
            int unit = rules.MoneyUnit;
            int even = rules.RegistrationFund / n / unit * unit;
            for (int i = 0; i < n; i++)
            {
                draft.Value[i] = even;
                draft.Start[i] = Mathf.Max(rules.MinStartPrice, even * 45 / 100 / unit * unit);
            }
            RegistrationHelper.Balance(rules, draft, -1);
            sub.text = L.T("reg_sub", L.G(rules.RegistrationFund));
            cards.Clear();
            cardRow.Clear();
            for (int i = 0; i < n; i++) cardRow.Add(BuildCard(i));
            Select(0);
            UpdateAll();
        }

        VisualElement BuildCard(int i)
        {
            var c = new Card();
            var root = Ui.Col("paper", "reg-card");
            root.RegisterCallback<PointerDownEvent>(e => Select(i), TrickleDown.TrickleDown);
            var tag = Ui.Text(L.T("reg_lot", i + 1).ToUpperInvariant(), "lot-tag");
            tag.style.alignSelf = Align.Center;
            root.Add(tag);
            Label itemLabel;
            root.Add(Ui.Picker(() => ItemLabel(i), () => ChangeAppearance(i, -1), () => ChangeAppearance(i, 1), "reg-item-name", out itemLabel));
            c.Item = itemLabel;

            root.Add(Ui.Text(L.T("reg_value"), "reg-label"));
            c.Value = Ui.Text("", "reg-big");
            root.Add(AmountRow(c.Value, new[] { -1000, -100, 100, 1000 }, d => ChangeValue(i, d)));

            root.Add(Ui.Text(L.T("reg_start"), "reg-label"));
            c.Start = Ui.Text("", "reg-big");
            root.Add(AmountRow(c.Start, new[] { -500, -100, 100, 500 }, d => ChangeStart(i, d)));

            root.Add(Ui.Text(L.T("reg_phrase"), "reg-label"));
            Label phraseLabel;
            var pp = Ui.Picker(() => PhraseLabel(i), () => ChangePhrase(i, -1), () => ChangePhrase(i, 1), "reg-phrase", out phraseLabel);
            root.Add(pp);
            c.Phrase = phraseLabel;
            var dice = Ui.Btn(L.T("random"), () => { draft.Phrase[i] = Random.Range(0, rules.PhraseCount); UpdateAll(); }, "btn-small", "btn-secondary");
            dice.style.alignSelf = Align.Center;

            c.CustomBox = Ui.Col("reg-custom");
            c.CustomBox.Add(Ui.Text(L.T("reg_custom_title"), "reg-label"));
            c.CustomName = new TextField { maxLength = rules.CustomNameMaxLength };
            c.CustomName.textEdition.placeholder = L.T("reg_custom_name");
            c.CustomName.RegisterValueChangedCallback(e => { draft.CustomName[i] = e.newValue; UpdateCard(i); });
            c.CustomPhrase = new TextField { maxLength = rules.CustomPhraseMaxLength, multiline = true };
            c.CustomPhrase.AddToClassList("reg-desc");
            c.CustomPhrase.textEdition.placeholder = L.T("reg_custom_phrase");
            c.CustomPhrase.RegisterValueChangedCallback(e => { draft.CustomPhrase[i] = e.newValue.Replace("\n", " "); UpdateCard(i); });
            // 그림: 미리보기 + 넣기/빼기 (넣은 그림은 회전판 위 액자로 모두에게 보인다)
            var imgRow = Ui.Row("reg-image-row");
            c.Thumb = Ui.Div("reg-thumb");
            var imgCol = Ui.Col();
            imgCol.style.flexGrow = 1;
            imgCol.style.marginLeft = 8;
            imgCol.Add(Ui.Btn(L.T("reg_image_pick"), () => PickImage(i), "btn-small"));
            c.ImageRemove = Ui.Btn(L.T("reg_image_remove"), () => RemoveImage(i), "btn-small", "btn-ghost");
            c.ImageRemove.Show(false);
            imgCol.Add(c.ImageRemove);
            imgCol.Add(Ui.Text(L.T("reg_image_note"), "tiny", "wrap"));
            imgRow.Add(c.Thumb, imgCol);
            c.CustomBox.Add(c.CustomName, c.CustomPhrase, imgRow);
            c.CustomBox.Show(false);
            var row = Ui.Row();
            row.style.justifyContent = Justify.Center;
            row.Add(dice, Ui.Btn(L.T("reg_custom"), () => c.CustomBox.Show(c.CustomBox.resolvedStyle.display == DisplayStyle.None), "btn-small", "btn-secondary"));
            root.Add(row, c.CustomBox);
            c.Root = root;
            cards.Add(c);
            return root;
        }

        static VisualElement AmountRow(Label value, int[] steps, System.Action<int> change)
        {
            var row = Ui.Row();
            row.style.justifyContent = Justify.Center;
            for (int k = 0; k < steps.Length; k++)
            {
                int d = steps[k];
                var b = Ui.Btn((d > 0 ? "+" : "−") + Mathf.Abs(d).ToString("#,0"), () => { change(d); Ui.Pop(value, 0.08f); }, "btn-small", "btn-secondary");
                b.style.marginLeft = b.style.marginRight = 1;
                b.style.paddingLeft = b.style.paddingRight = 4;
                b.style.fontSize = 13;
                if (k == steps.Length / 2) row.Add(value);
                row.Add(b);
            }
            return row;
        }

        string ItemLabel(int i)
        {
            if (draft == null) return "";
            return !string.IsNullOrEmpty(draft.CustomName[i]) ? draft.CustomName[i] : L.T("item_" + draft.Appearance[i]);
        }

        string PhraseLabel(int i)
        {
            if (draft == null) return "";
            return !string.IsNullOrEmpty(draft.CustomPhrase[i]) ? "“" + draft.CustomPhrase[i] + "”" : "“" + L.T("phrase_" + draft.Phrase[i]) + "”";
        }

        void Select(int i)
        {
            selected = Mathf.Clamp(i, 0, n - 1);
            for (int k = 0; k < cards.Count; k++) cards[k].Root.EnableInClassList("reg-card-selected", k == selected);
            ShowOnTable(selected);
            armed = false;
        }

        void ShowOnTable(int i)
        {
            Texture2D pic;
            previews.TryGetValue(i, out pic);
            app.Stage.ShowLot(draft.Appearance[i], pic);
        }

        void PickImage(int i)
        {
            Select(i);
            string path = Platform.FileDialog.OpenImage(L.T("reg_image_pick"), L.T("reg_image_filter"));
            if (string.IsNullOrEmpty(path)) return;
            Texture2D preview;
            string error;
            var jpg = ItemImages.Prepare(path, out preview, out error);
            if (jpg == null)
            {
                UiRoot.I.Toast(L.T("reg_image_failed") + (error == "too_big" ? " (" + L.T("reg_image_too_big") + ")" : ""), "toast-alert", 3f);
                return;
            }
            Texture2D old;
            if (previews.TryGetValue(i, out old) && old != null) Object.Destroy(old);
            images[i] = jpg;
            previews[i] = preview;
            cards[i].Thumb.style.backgroundImage = Background.FromTexture2D(preview);
            cards[i].ImageRemove.Show(true);
            AudioManager.I.Play("ui_confirm", 0.7f, 1.1f, Bus.Ui);
            ShowOnTable(i);
        }

        void RemoveImage(int i)
        {
            Texture2D old;
            if (previews.TryGetValue(i, out old) && old != null) Object.Destroy(old);
            previews.Remove(i);
            images.Remove(i);
            cards[i].Thumb.style.backgroundImage = StyleKeyword.None;
            cards[i].ImageRemove.Show(false);
            ShowOnTable(i);
        }

        /// <summary>테스트용: 파일 없이 그림을 넣는다.</summary>
        public void DevSetImage(int i, byte[] jpg)
        {
            var t = new Texture2D(2, 2);
            if (!t.LoadImage(jpg)) return;
            images[i] = jpg;
            previews[i] = t;
            if (i < cards.Count)
            {
                cards[i].Thumb.style.backgroundImage = Background.FromTexture2D(t);
                cards[i].ImageRemove.Show(true);
                cards[i].CustomBox.Show(true);
            }
        }

        public void DevSetCustom(int i, string name, string desc)
        {
            if (draft == null || i >= n) return;
            draft.CustomName[i] = name;
            draft.CustomPhrase[i] = desc;
            cards[i].CustomName.SetValueWithoutNotify(name);
            cards[i].CustomPhrase.SetValueWithoutNotify(desc);
            cards[i].CustomBox.Show(true);
            UpdateAll();
        }

        void ChangeAppearance(int i, int d)
        {
            Select(i);
            int count = rules.AppearanceCount;
            int a = draft.Appearance[i];
            for (int guard = 0; guard < count; guard++)
            {
                a = (a + d + count) % count;
                bool used = false;
                for (int k = 0; k < n; k++) if (k != i && draft.Appearance[k] == a) used = true;
                if (!used || rules.AllowDuplicateAppearance) break;
            }
            draft.Appearance[i] = a;
            ShowOnTable(i);
            AudioManager.I.Play("sfx_whoosh", 0.5f, 1.2f, Bus.Ui);
            UpdateAll();
        }

        void ChangeValue(int i, int d)
        {
            Select(i);
            int min = rules.EffectiveMinValue;
            draft.Value[i] = Mathf.Clamp(draft.Value[i] + d, min, rules.RegistrationFund);
            UpdateAll();
        }

        void ChangeStart(int i, int d)
        {
            Select(i);
            draft.Start[i] = Mathf.Clamp(draft.Start[i] + d, rules.MinStartPrice, rules.MaxStartPrice);
            UpdateAll();
        }

        void ChangePhrase(int i, int d)
        {
            Select(i);
            draft.Phrase[i] = (draft.Phrase[i] + d + rules.PhraseCount) % rules.PhraseCount;
            UpdateAll();
        }

        void Balance()
        {
            RegistrationHelper.Balance(rules, draft, selected);
            AudioManager.I.Play("sfx_coins", 0.6f, 1f, Bus.Ui);
            UpdateAll();
        }

        void Randomize()
        {
            draft = RegistrationHelper.Random(rules, new Rng(System.Environment.TickCount));
            for (int i = 0; i < n; i++)
            {
                cards[i].CustomName.SetValueWithoutNotify("");
                cards[i].CustomPhrase.SetValueWithoutNotify("");
            }
            AudioManager.I.Play("sfx_cards", 0.7f, 1f, Bus.Ui);
            Select(selected);
            UpdateAll();
        }

        void UpdateCard(int i)
        {
            var c = cards[i];
            c.Item.text = ItemLabel(i);
            c.Value.text = L.G(draft.Value[i]);
            c.Start.text = L.G(draft.Start[i]);
            c.Phrase.text = PhraseLabel(i);
            c.Start.EnableInClassList("red", draft.Start[i] > rules.BuyFund);
        }

        void UpdateAll()
        {
            if (draft == null) return;
            int sum = 0;
            bool fee = false;
            for (int i = 0; i < n; i++)
            {
                UpdateCard(i);
                sum += draft.Value[i];
                if (draft.Start[i] > rules.BuyFund) fee = true;
            }
            int fund = rules.RegistrationFund;
            totalFill.style.width = Length.Percent(Mathf.Clamp01(sum / (float)fund) * 100f);
            totalFill.EnableInClassList("reg-total-ok", sum == fund);
            totalFill.EnableInClassList("reg-total-over", sum > fund);
            string state = sum == fund ? L.T("reg_exact") : (sum < fund ? L.T("reg_left", L.G(fund - sum)) : L.T("reg_over", L.G(sum - fund)));
            totalText.text = L.T("reg_total", L.G(sum), L.G(fund)) + "  ·  " + state;
            var code = AuctionEngine.Validate(rules, draft);
            errorText.text = code == Result.Ok ? "" : ErrorText(code);
            feeText.text = fee ? L.T("reg_fee_warn", L.G(rules.BuyFund)) : "";
            confirm.SetEnabled(code == Result.Ok && !submitted);
            confirm.text = submitted ? L.T("reg_waiting") : (armed ? L.T("reg_confirm_again") : L.T("reg_confirm"));
        }

        string ErrorText(Result r)
        {
            switch (r)
            {
                case Result.SumMismatch: return L.T("reg_err_sum");
                case Result.BelowMin: return L.T("reg_err_min", L.G(rules.EffectiveMinValue));
                case Result.DuplicateAppearance: return L.T("reg_err_dup");
                default: return r.ToString();
            }
        }

        void Confirm()
        {
            if (submitted) return;
            if (!armed)
            {
                armed = true;
                UpdateAll();
                Ui.Pop(confirm, 0.08f);
                return;
            }
            var r = app.Session.Register(draft);
            if (r == Result.Ok)
            {
                submitted = true;
                // 넣은 그림은 물건 번호로 모두에게 보낸다 (경매에 나올 때 회전판 위에 보인다)
                int me = app.Session.LocalSeat;
                foreach (var kv in images)
                    if (kv.Key < n && SocialHub.I != null) SocialHub.I.SendItemImage(AuctionEngine.ItemId(me, kv.Key), kv.Value);
                AudioManager.I.Play("ui_confirm", 1f, 1f, Bus.Ui);
                AudioManager.I.Play("sfx_cards", 0.7f, 0.9f, Bus.Ui);
            }
            else
            {
                Ui.Deny(confirm);
                errorText.text = ErrorText(r);
            }
            armed = false;
            UpdateAll();
        }

        public void Refresh(GameView v)
        {
            if (draft == null) Init(v);
            if (v.Me != null && v.Me.Registered) submitted = true;
            UpdateAll();
        }

        public void Tick(GameView v)
        {
            if (draft == null) Init(v);
        }

        /// <summary>자동 진행(시험)용: 지금 초안으로 바로 확정.</summary>
        public void AutoConfirm()
        {
            if (draft == null || submitted) return;
            armed = true;
            Confirm();
        }
    }
}
