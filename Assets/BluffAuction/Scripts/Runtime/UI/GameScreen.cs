using System.Collections.Generic;
using BluffAuction.Audio;
using BluffAuction.Core;
using BluffAuction.Game;
using BluffAuction.Loc;
using BluffAuction.World;
using UnityEngine;
using EventType = BluffAuction.Core.EventType;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    /// <summary>
    /// 한 판 동안의 화면. 단계에 따라 등록 화면 / 경매 화면 / 공개·결과 화면을 바꿔 보이고,
    /// 이름표·말풍선·떠오르는 금액을 3D 좌석 위에 띄운다. 단축키: Space 입찰, 1~4 올릴 금액, Q/W/E 능력, T 한마디, Esc 일시정지.
    /// </summary>
    public sealed partial class GameScreen
    {
        public readonly VisualElement Root;
        readonly GameApp app;
        GameSession S { get { return app.Session; } }
        GameView V { get { return app.Session.View; } }

        VisualElement hud, top, lotCard, priceBox, bidBar, sellerBar, wallet, abilities, players, feed, resultCard, rightCol;
        Label lotTag, lotName, lotSeller, lotPhrase, lotCustom, ribbon, priceLabel, priceValue, priceBidder, timerText, bidReason, skipLabel;
        VisualElement timerFill, lotDot;
        Button bidButton, skipButton, linesButton, loanButton;
        readonly List<Button> chips = new List<Button>();
        readonly int[] chipSteps = { 0, 500, 1000, 2000 };
        int chip;
        Label walletCash, walletAvail, walletPenalty, resultText;
        readonly Button[] abilityButtons = new Button[3];
        readonly Label[] abilityCounts = new Label[3];
        readonly List<VisualElement> playerRows = new List<VisualElement>();
        readonly List<Label> playerCash = new List<Label>(), playerMeta = new List<Label>();
        readonly List<VisualElement> nameplates = new List<VisualElement>();
        readonly List<Label> nameplateBid = new List<Label>();
        readonly float[] bidFlash = new float[8];
        readonly int[] bidFlashAmount = new int[8];
        readonly List<Bubble> bubbles = new List<Bubble>();
        RegistrationPanel registration;
        RevealPanel reveal;
        VisualElement priceDrop, banner, linesPopup;
        Label bannerText, bannerSub;
        float bannerUntil;
        int lastTickSecond = -1;
        int shownSeq = -1;
        Phase lastPhase = Phase.Setup;
        readonly HashSet<string> hintsShown = new HashSet<string>();

        sealed class Bubble
        {
            public VisualElement El;
            public int Seat;
            public float Until;
        }

        public GameScreen(GameApp app)
        {
            this.app = app;
            Root = Ui.Div();
            Root.pickingMode = PickingMode.Ignore;
            BuildHud();
            registration = new RegistrationPanel(app);
            Root.Add(registration.Root);
            reveal = new RevealPanel(app);
            Root.Add(reveal.Root);
            BuildBanner();
            BuildNameplates();
            BuildFirstPerson();
            BuildSocial();
            S.ViewChanged += Refresh;
            Refresh();
        }

        // ═════════════════ 만들기 ═════════════════

        void BuildHud()
        {
            hud = Ui.Div("screen");
            hud.pickingMode = PickingMode.Ignore;
            Root.Add(hud);

            // ── 위: 물건 카드 + 가격
            top = Ui.Col();
            top.pickingMode = PickingMode.Ignore;
            top.style.position = Position.Absolute;
            top.style.left = 0;
            top.style.right = 0;
            top.style.top = 18;
            top.style.alignItems = Align.Center;
            hud.Add(top);

            lotCard = Ui.Col("paper", "lot-card");
            var tagRow = Ui.Row();
            lotTag = Ui.Text("", "lot-tag");
            ribbon = Ui.Text(L.T("reauction"), "ribbon");
            ribbon.style.marginLeft = 10;
            tagRow.Add(lotTag, ribbon);
            lotName = Ui.Text("", "lot-name");
            var sellerRow = Ui.Row();
            lotDot = Ui.Dot(Color.white, 14);
            lotSeller = Ui.Text("", "lot-seller");
            sellerRow.Add(lotDot, lotSeller);
            lotPhrase = Ui.Text("", "quote");
            lotPhrase.style.marginTop = 6;
            lotCustom = Ui.Text("", "quote");
            lotCard.Add(tagRow, lotName, sellerRow, lotPhrase, lotCustom);
            top.Add(lotCard);

            priceBox = Ui.Col("panel-soft", "price-box");
            priceBox.style.marginTop = 10;
            priceLabel = Ui.Text("", "price-label");
            priceValue = Ui.Text("", "price-value");
            priceBidder = Ui.Text("", "price-bidder");
            var track = Ui.Div("timer-track");
            timerFill = Ui.Div("timer-fill");
            track.Add(timerFill);
            timerText = Ui.Text("", "timer-text");
            priceBox.Add(priceLabel, priceValue, priceBidder, track, timerText);
            top.Add(priceBox);

            // ── 왼쪽: 참가자
            players = Ui.Col("panel", "side-panel");
            players.style.position = Position.Absolute;
            players.style.left = 24;
            players.style.top = 24;
            players.Add(Ui.Text(L.T("players"), "h3"));
            for (int i = 0; i < GameRules.MaxSeats; i++)
            {
                var row = Ui.Row("player-row");
                var face = Ui.Div("portrait", "portrait-small");
                face.name = "face";
                face.style.width = face.style.height = 38;
                face.style.flexShrink = 0;
                row.Add(face);
                var dot = Ui.Dot(Color.white, 14);
                dot.name = "dot";
                var nameCol = Ui.Col();
                nameCol.style.flexGrow = 1;
                var name = Ui.Text("", "player-name");
                name.name = "name";
                var meta = Ui.Text("", "player-meta");
                nameCol.Add(name, meta);
                var cash = Ui.Text("", "player-cash");
                // 목소리·채팅 음소거 (온라인 사람 좌석만)
                int seatIdx = i;
                var mute = new Button { name = "mute" };
                mute.AddToClassList("mute-btn");
                Ui.Wire(mute, () => { if (SocialHub.I != null) SocialHub.I.ToggleMute(seatIdx); Refresh(); }, "ui_toggle");
                mute.Show(false);
                row.Add(dot, nameCol, cash, mute);
                players.Add(row);
                playerRows.Add(row);
                playerCash.Add(cash);
                playerMeta.Add(meta);
            }
            hud.Add(players);

            // ── 오른쪽 위: 기록 + 능력 결과
            rightCol = Ui.Col();
            rightCol.pickingMode = PickingMode.Ignore;
            rightCol.style.position = Position.Absolute;
            rightCol.style.right = 24;
            rightCol.style.top = 24;
            rightCol.style.alignItems = Align.FlexEnd;
            feed = Ui.Col("panel-soft", "feed");
            feed.pickingMode = PickingMode.Ignore;
            rightCol.Add(feed);
            resultCard = Ui.Col("result-card");
            resultCard.style.marginTop = 10;
            resultCard.Add(Ui.Text(L.T("abilities"), "small"));
            resultText = Ui.Text("", "result-text");
            resultCard.Add(resultText);
            resultCard.Show(false);
            rightCol.Add(resultCard);
            var menuRow = Ui.Row();
            menuRow.style.marginTop = 10;
            menuRow.Add(Ui.KeyBtn("Ⅱ", "Esc", () => app.Presenter.TogglePause(), "btn-small", "btn-ghost"));
            rightCol.Add(menuRow);
            hud.Add(rightCol);

            // ── 아래: 지갑 · 입찰 · 능력
            var bottom = Ui.Row();
            bottom.pickingMode = PickingMode.Ignore;
            bottom.style.position = Position.Absolute;
            bottom.style.left = 24;
            bottom.style.right = 24;
            bottom.style.bottom = 20;
            bottom.style.alignItems = Align.FlexEnd;
            hud.Add(bottom);

            wallet = Ui.Col("panel", "wallet");
            wallet.Add(Ui.Text(L.T("wallet"), "small"));
            walletCash = Ui.Text("", "wallet-big");
            walletAvail = Ui.Text("", "small");
            walletPenalty = Ui.Text("", "small", "wrap");
            walletPenalty.style.marginTop = 4;
            loanButton = Ui.Btn(L.T("loan"), AskLoan, "btn-small", "btn-red");
            loanButton.style.alignSelf = Align.FlexStart;
            loanButton.style.marginTop = 6;
            wallet.Add(walletCash, walletAvail, walletPenalty, loanButton);
            bottom.Add(wallet);
            bottom.Add(Ui.Div("spacer"));

            var center = Ui.Col();
            center.style.alignItems = Align.Center;
            bidBar = Ui.Col("panel", "bidbar");
            var chipRow = Ui.Row();
            for (int i = 0; i < chipSteps.Length; i++)
            {
                int ci = i;
                var b = Ui.KeyBtn(i == 0 ? L.T("bid_min") : "+" + chipSteps[i].ToString("#,0"), (i + 1).ToString(), () => SelectChip(ci), "btn-chip", "btn-secondary");
                chips.Add(b);
                chipRow.Add(b);
            }
            bidBar.Add(chipRow);
            bidButton = Ui.KeyBtn("", "Space", PressBid, "btn-huge", "bid-main");
            bidBar.Add(bidButton);
            bidReason = Ui.Text("", "bid-reason");
            bidBar.Add(bidReason);
            var actions = Ui.Row();
            skipButton = Ui.Btn(L.T("skip"), () => S.Skip(true), "btn-small", "btn-ghost");
            skipLabel = Ui.Text("", "tiny");
            linesButton = Ui.KeyBtn(L.T("lines_title"), "T", ToggleLines, "btn-small", "btn-ghost");
            actions.Add(skipButton, skipLabel, Ui.Div("spacer"), linesButton);
            bidBar.Add(actions);
            center.Add(bidBar);

            sellerBar = Ui.Col("panel", "bidbar");
            sellerBar.Add(Ui.Text(L.T("your_lot"), "h3", "center-text"));
            var hype = Ui.Row();
            for (int i = 0; i < 3; i++)
            {
                int line = Content.LineHypeFirst + i;
                hype.Add(Ui.Btn(L.T("line_" + line), () => Say(line), "btn-small", "btn-purple"));
            }
            sellerBar.Add(hype);
            var more = Ui.Btn(L.T("lines_title"), ToggleLines, "btn-small", "btn-ghost");
            sellerBar.Add(more);
            center.Add(sellerBar);
            bottom.Add(center);
            bottom.Add(Ui.Div("spacer"));

            abilities = Ui.Col("panel");
            abilities.Add(Ui.Text(L.T("abilities"), "small"));
            var abRow = Ui.Row();
            string[] keys = { "Q", "W", "E" };
            string[] names = { "ab_compare", "ab_appraise", "ab_fake" };
            string[] cls = { "btn-green", "btn-green", "btn-purple" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var b = new Button();
                b.AddToClassList("btn");
                b.AddToClassList("ability-btn");
                b.AddToClassList(cls[i]);
                b.tooltip = L.T(names[i] + "_desc");
                var nm = Ui.Text(L.T(names[i]), "ability-name");
                var cnt = Ui.Text("", "ability-count");
                var key = Ui.Text(keys[i], "key");
                b.Add(nm, cnt, key);
                Ui.Wire(b, () => UseAbility((AbilityKind)k), "ui_click");
                abilityButtons[i] = b;
                abilityCounts[i] = cnt;
                abRow.Add(b);
            }
            abilities.Add(abRow);
            bottom.Add(abilities);
        }

        void BuildBanner()
        {
            banner = Ui.Col("banner", "banner-out");
            banner.pickingMode = PickingMode.Ignore;
            bannerText = Ui.Text("", "banner-text");
            bannerSub = Ui.Text("", "banner-sub");
            banner.Add(bannerText, bannerSub);
            bannerWrap = Ui.Div("screen");
            bannerWrap.pickingMode = PickingMode.Ignore;
            bannerWrap.style.alignItems = Align.Center;
            bannerWrap.style.justifyContent = Justify.Center;
            bannerWrap.Add(banner);
            Root.Add(bannerWrap);
        }

        VisualElement bannerWrap;

        /// <summary>1인칭에서는 알림 띠를 위쪽(가격 띠 아래)으로 올리고 작게 (가운데 물건·조준점을 가리지 않게).</summary>
        void PlaceBanner(bool firstPerson)
        {
            bannerWrap.style.justifyContent = firstPerson ? Justify.FlexStart : Justify.Center;
            bannerWrap.style.paddingTop = firstPerson ? 104 : 0;
            banner.EnableInClassList("banner-fp", firstPerson);
        }

        void BuildNameplates()
        {
            var layer = UiRoot.I.WorldLayer;
            layer.Clear();
            nameplates.Clear();
            nameplateBid.Clear();
            for (int i = 0; i < 8; i++)
            {
                var np = Ui.Col("nameplate");
                np.pickingMode = PickingMode.Ignore;
                var nr = Ui.Row();
                nr.Add(Ui.Dot(Color.white, 10), Ui.Text("", "nameplate-name"));
                DecorateNameplate(nr);
                var bid = Ui.Text("", "nameplate-bid");
                np.Add(nr, bid);
                np.style.translate = new Translate(Length.Percent(-50), Length.Percent(-100));
                np.Show(false);
                layer.Add(np);
                nameplates.Add(np);
                nameplateBid.Add(bid);
            }
        }

        // ═════════════════ 갱신 ═════════════════

        public void Dispose()
        {
            S.ViewChanged -= Refresh;
            UiRoot.I.WorldLayer.Clear();
            ReleaseCursor();
        }

        Color SeatColor(int seat)
        {
            if (seat < 0 || seat >= V.SeatCount) return Color.gray;
            return Stage.SeatColors[V.Seats[seat].ColorIndex % Stage.SeatColors.Length];
        }

        public string SeatName(int seat)
        {
            if (seat == -2) return L.T("someone");
            if (seat < 0 || seat >= V.SeatCount) return "?";
            return seat == V.Viewer ? L.T("you") : V.Seats[seat].Name;
        }

        public static string ItemName(ItemView it)
        {
            if (it == null) return "";
            if (!string.IsNullOrEmpty(it.CustomName)) return it.CustomName;
            return it.Appearance >= 0 ? L.T("item_" + it.Appearance) : "???";
        }

        void Refresh()
        {
            var v = V;
            if (v == null || v.Seats.Length == 0) return;
            bool reg = v.Phase == Phase.Registration;
            bool auction = v.Phase == Phase.Presenting || v.Phase == Phase.Bidding || v.Phase == Phase.PriceDrop || v.Phase == Phase.Reauction || v.Phase == Phase.AuctionResult;
            bool rev = v.Phase == Phase.Reveal || v.Phase == Phase.Results;
            registration.Root.Show(reg);
            if (reg) registration.Refresh(v);
            top.Show(auction && v.Current != null);
            bidBar.parent.Show(auction);
            wallet.Show(auction || reg);
            abilities.Show(auction);
            players.Show(!rev);
            feed.Show(!rev);
            // 마지막 물건의 능력 결과가 공개·결과 표 위에 남지 않게
            if (rev && On(resultCard)) resultCard.Show(false);
            reveal.Root.Show(rev);
            if (rev) reveal.Refresh(v);
            if (v.Phase != lastPhase)
            {
                OnPhase(v.Phase, lastPhase);
                lastPhase = v.Phase;
            }

            // 참가자 목록
            for (int i = 0; i < playerRows.Count; i++)
            {
                bool on = i < v.SeatCount;
                playerRows[i].Show(on);
                if (!on) continue;
                var s = v.Seats[i];
                playerRows[i].Q("dot").style.backgroundColor = SeatColor(i);
                var face = playerRows[i].Q("face");
                if (face.userData == null && i < app.Stage.Guests.Count && app.Stage.Guests[i].Look != null)
                    Ui.SetPortrait(face, app.Stage.Guests[i].Look);
                playerRows[i].Q<Label>("name").text = SeatName(i) + (s.Loan ? "  ◆" : "");
                playerCash[i].text = L.G(s.Balance);
                string meta = reg ? (s.Registered ? "✓ " + L.T("reg_done") : "… " + L.T("reg_pending")) : L.T("won_items", s.WonCount);
                if (s.Loan) meta += " · " + L.T("loan_taken");
                int uses = AbilityUsesThisGame(i);
                if (uses > 0) meta += " · " + L.T("ab_total", uses);
                int lotBid = LotBidOf(i);
                if (auction && lotBid > 0) meta += " · " + L.T("bid_meta", L.G(lotBid));
                bool isHigh = v.HighestBidder == i;
                bool isSeller = v.Current != null && v.Current.Seller == i && auction;
                if (isSeller) meta = L.T("seller_meta") + " · " + meta;
                playerMeta[i].text = meta;
                playerRows[i].EnableInClassList("player-row-high", isHigh);
                playerRows[i].EnableInClassList("player-row-seller", isSeller);
                playerRows[i].EnableInClassList("player-row-me", i == v.Viewer);
                var muteBtn = playerRows[i].Q<Button>("mute");
                bool human = !s.IsAI && i != v.Viewer && S.IsOnline;
                muteBtn.Show(human);
                muteBtn.EnableInClassList("mute-on", SocialHub.I != null && SocialHub.I.Muted.Contains(i));
            }

            // 지갑
            var me = v.Me;
            if (me != null)
            {
                walletCash.text = L.G(me.Balance);
                walletAvail.text = L.T("available") + " " + L.G(v.MyAvailable) + (me.Reserved > 0 ? "   " + L.T("reserved") + " " + L.G(me.Reserved) : "");
                int pen = v.MyPenaltyNow;
                walletPenalty.Show(!reg);
                walletPenalty.text = pen > 0 ? L.T("penalty_now", L.G(pen)) + "\n" + L.T("penalty_hint", L.G(v.MyUnspentBudget)) : L.T("penalty_none");
                walletPenalty.EnableInClassList("red", pen > 0);
                walletPenalty.EnableInClassList("green", pen == 0);
                loanButton.Show(v.Rules.LoanAmount > 0 && !me.Loan && auction);
                abilityCounts[0].text = L.T("ab_left", me.CompareLeft);
                abilityCounts[1].text = L.T("ab_left", me.AppraiseLeft);
                abilityCounts[2].text = L.T("ab_left", me.FakeLeft);
            }

            if (auction && v.Current != null) RefreshLot(v);
            RefreshMode(v);
            if (app.TableKit != null) app.TableKit.Refresh();
        }

        void RefreshLot(GameView v)
        {
            var it = v.Current;
            int total = v.SeatCount * v.Rounds;
            lotTag.text = (L.T("lot_of", v.AuctionOrder.Count, total) + "  ·  " + L.T("round", v.Round + 1)).ToUpperInvariant();
            ribbon.Show(v.LeftoverPass);
            lotName.text = ItemName(it);
            lotDot.style.backgroundColor = SeatColor(it.Seller);
            lotSeller.text = L.T("seller") + "  " + SeatName(it.Seller);
            lotPhrase.text = it.Phrase >= 0 ? "“" + L.T("phrase_" + it.Phrase) + "”" : "";
            lotCustom.text = !string.IsNullOrEmpty(it.CustomPhrase) ? "“" + it.CustomPhrase + "”" : "";
            lotCustom.Show(!string.IsNullOrEmpty(it.CustomPhrase));

            bool hasBid = v.HighestBidder != -1;
            string pv;
            if (it.Status == ItemStatus.Sold)
            {
                // 낙찰 직후: 낙찰가와 새 주인
                priceLabel.text = L.T("reveal_paid").ToUpperInvariant();
                pv = L.G(it.SalePrice);
                priceBidder.text = it.Winner == v.Viewer ? L.T("you_are_high") : SeatName(it.Winner);
                priceBidder.EnableInClassList("price-me", it.Winner == v.Viewer);
            }
            else
            {
                priceLabel.text = (hasBid ? L.T("current_bid") : L.T("start_price")).ToUpperInvariant();
                pv = L.G(hasBid ? v.HighestBid : it.StartPrice);
                priceBidder.text = hasBid ? (v.IAmHighest ? L.T("you_are_high") : SeatName(v.HighestBidder)) : L.T("no_bids_yet");
                priceBidder.EnableInClassList("price-me", v.IAmHighest);
            }
            if (priceValue.text != pv)
            {
                priceValue.text = pv;
                Ui.Pop(priceValue, 0.1f);
            }

            bool seller = v.IAmSeller;
            bidBar.Show(!seller);
            sellerBar.Show(seller && (v.BiddingOpen || v.Phase == Phase.Presenting));
            UpdateBidButton();
            skipLabel.text = L.T("skip_votes", v.SkipAgree, v.ActiveCount);
            skipButton.SetEnabled(v.BiddingOpen || v.Phase == Phase.Presenting);
            bool abilityOk = v.BiddingOpen && !seller;
            for (int i = 0; i < 3; i++)
            {
                int left = i == 0 ? v.Me.CompareLeft : (i == 1 ? v.Me.AppraiseLeft : v.Me.FakeLeft);
                abilityButtons[i].SetEnabled(abilityOk && left > 0);
            }
            UpdatePriceDrop(v);
        }

        int ChipAmount()
        {
            var v = V;
            int amount = v.MinNextBid + chipSteps[chip];
            return amount;
        }

        void UpdateBidButton()
        {
            var v = V;
            int amount = ChipAmount();
            Ui.SetText(bidButton, L.T("bid_button", L.G(amount)));
            string reason = "";
            bool ok = true;
            if (!v.BiddingOpen) { ok = false; reason = L.T("bid_blocked_wait"); }
            else if (v.IAmSeller) { ok = false; reason = L.T("bid_blocked_seller"); }
            else if (v.IAmHighest) { ok = false; reason = L.T("bid_blocked_high"); }
            else if (amount > v.MyAvailable) { ok = false; reason = L.T("bid_blocked_cash"); }
            bidButton.SetEnabled(ok);
            bidReason.text = reason;
            for (int i = 0; i < chips.Count; i++)
            {
                chips[i].EnableInClassList("btn-chip-on", i == chip);
                chips[i].SetEnabled(v.MinNextBid + chipSteps[i] <= v.MyAvailable || i == 0);
            }
        }

        void SelectChip(int i)
        {
            chip = Mathf.Clamp(i, 0, chipSteps.Length - 1);
            UpdateBidButton();
            Ui.Pop(bidButton, 0.06f);
        }

        // ═════════════════ 조작 ═════════════════

        void PressBid()
        {
            var v = V;
            if (!v.BiddingOpen || v.IAmSeller || v.IAmHighest || ChipAmount() > v.MyAvailable)
            {
                Ui.Deny(bidButton);
                return;
            }
            var r = S.Bid(ChipAmount());
            if (r == Result.Ok)
            {
                chip = 0;
                AudioManager.I.Play("ui_coin", 0.9f, 1f, Bus.Ui);
                Ui.Pop(bidButton, 0.1f);
            }
            else Ui.Deny(bidButton);
        }

        void UseAbility(AbilityKind k)
        {
            var r = S.Ability(k);
            if (r != Result.Ok)
            {
                Ui.Deny(abilityButtons[(int)k]);
                UiRoot.I.Toast(L.T("ab_blocked"), "toast-alert", 2f);
            }
        }

        void Say(int line)
        {
            if (S.Say(line) == Result.Ok) CloseLines();
        }

        void AskLoan()
        {
            var r = V.Rules;
            UiRoot.I.Confirm(L.T("loan_title"), L.T("loan_body", L.G(r.LoanAmount), L.G(r.LoanAmount + r.LoanInterest)), L.T("loan_take"), () =>
            {
                if (S.Loan() == Result.Ok) AudioManager.I.Play("sfx_loan", 0.9f);
            }, L.T("cancel"));
        }

        void ToggleLines()
        {
            if (linesPopup != null) { CloseLines(); return; }
            var panel = Ui.Col("panel");
            panel.style.width = 760;
            panel.Add(Ui.Text(L.T("lines_title"), "h3"));
            AddLineGroup(panel, L.T("lines_hype"), Content.LineHypeFirst, Content.LineHypeCount);
            AddLineGroup(panel, L.T("lines_taunt"), Content.LineTauntFirst, Content.LineTauntCount);
            AddLineGroup(panel, L.T("lines_doubt"), Content.LineDoubtFirst, Content.LineDoubtCount);
            AddLineGroup(panel, L.T("lines_bluff"), Content.LineBluffFirst, Content.LineBluffCount);
            AddLineGroup(panel, L.T("lines_react"), Content.LineWinFirst, Content.LineWinCount + 0);
            AddLineGroup(panel, "", Content.LineLamentFirst, Content.LineLamentCount);
            panel.Add(Ui.Btn(L.T("close"), CloseLines, "btn-small", "btn-ghost"));
            linesPopup = UiRoot.I.Open(panel, false);
        }

        void AddLineGroup(VisualElement panel, string title, int first, int count)
        {
            if (!string.IsNullOrEmpty(title)) panel.Add(Ui.Text(title, "small"));
            var row = Ui.Row();
            row.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < count; i++)
            {
                int line = first + i;
                row.Add(Ui.Btn(L.T("line_" + line), () => Say(line), "btn-small", "btn-secondary"));
            }
            panel.Add(row);
        }

        void CloseLines()
        {
            if (linesPopup == null) return;
            UiRoot.I.Close(linesPopup);
            linesPopup = null;
        }

        // ── 무입찰: 판매자의 시작가 결정
        int dropPrice;

        void UpdatePriceDrop(GameView v)
        {
            bool mine = v.Phase == Phase.PriceDrop && v.IAmSeller;
            if (mine && priceDrop == null) OpenPriceDrop(v);
            if (!mine && priceDrop != null)
            {
                UiRoot.I.Close(priceDrop);
                priceDrop = null;
            }
        }

        void OpenPriceDrop(GameView v)
        {
            var it = v.Current;
            int start = it.StartPrice;
            int min = v.Rules.MinStartPrice;
            int unit = v.Rules.MoneyUnit;
            dropPrice = Mathf.Max(min, (start / 2) / unit * unit);
            var panel = Ui.Col("paper");
            panel.style.width = 720;
            panel.style.alignItems = Align.Center;
            panel.Add(Ui.Text(L.T("nobid_title"), "lot-name"));
            var sub = Ui.Text(L.T("nobid_sub"), "ink-soft", "wrap", "center-text");
            sub.style.marginTop = 8;
            panel.Add(sub);
            int value = it.MyValue;
            if (value > 0) panel.Add(Ui.Text(L.T("reg_value") + " " + L.G(value), "ink"));
            Label priceLbl = Ui.Text(L.G(dropPrice), "reg-big");
            var slider = new SliderInt(min / unit, start / unit) { value = dropPrice / unit };
            slider.style.width = 520;
            Button lower = null;
            slider.RegisterValueChangedCallback(e =>
            {
                dropPrice = e.newValue * unit;
                priceLbl.text = L.G(dropPrice);
                lower.text = L.T("nobid_lower", L.G(dropPrice));
                AudioManager.I.Play("ui_slider", 0.4f, 1f, Bus.Ui);
            });
            panel.Add(priceLbl, slider);
            var row = Ui.Row();
            row.style.marginTop = 10;
            row.Add(Ui.Btn(L.T("nobid_keep", L.G(start)), () => S.PriceDrop(start), "btn-secondary"));
            lower = Ui.Btn(L.T("nobid_lower", L.G(dropPrice)), () => S.PriceDrop(dropPrice));
            row.Add(lower);
            row.Add(Ui.Btn(L.T("nobid_giveup"), () => S.PriceDrop(0), "btn-red"));
            panel.Add(row);
            var t = Ui.Text("", "ink-soft");
            t.schedule.Execute(() => t.text = L.T("seconds", Mathf.CeilToInt(S.RemainingMs / 1000f))).Every(200);
            panel.Add(t);
            priceDrop = UiRoot.I.Open(panel);
        }

        // ═════════════════ 사건 (Presenter 가 부른다) ═════════════════

        public void OnEvent(GameEvent e)
        {
            var v = V;
            TrackAbility(e, v);
            switch (e.Type)
            {
                case EventType.LotPresented:
                    chip = 0;
                    resultCard.Show(false);
                    Feed(L.T("feed_lot", SeatName(e.Seat), ItemName(v.Items[e.Item]), L.G(e.A)));
                    if (e.Seat == v.Viewer) Hint("seller", L.T("hint_seller"));
                    break;
                case EventType.BiddingOpened:
                    Hint("bid", L.T("hint_bid"));
                    break;
                case EventType.BidPlaced:
                    Feed(L.T("feed_bid", SeatName(e.Seat), L.G(e.A)));
                    if (e.Seat >= 0 && e.Seat < bidFlash.Length)
                    {
                        bidFlash[e.Seat] = Time.unscaledTime + 2.2f;
                        bidFlashAmount[e.Seat] = e.A;
                        nameplateBid[e.Seat].text = L.G(e.A);
                    }
                    if (e.B == v.Viewer && e.Seat != v.Viewer)
                    {
                        UiRoot.I.Toast(L.T("outbid"), "toast-alert", 2.2f);
                        AudioManager.I.Play("ui_notify", 0.8f, 1f, Bus.Ui);
                    }
                    if (e.C == 1) FlashTimer(L.T("extended", v.Rules.ExtendSeconds));
                    if (v.AuctionOrder.Count >= 2) Hint("ability", L.T("hint_ability"));
                    break;
                case EventType.AbilityUsed:
                    Feed(L.T("feed_ability", SeatName(e.Seat)));
                    if (e.Seat != v.Viewer) UiRoot.I.Toast(L.T("ab_used_by", SeatName(e.Seat)), null, 2.6f);
                    break;
                case EventType.AbilityResult:
                    ShowAbilityResult(e);
                    break;
                case EventType.SellerSaid:
                    string text = e.A >= 0 ? L.T("line_" + e.A) : e.Text;
                    Feed(L.T("feed_say", SeatName(e.Seat), text));
                    ShowBubble(e.Seat, text, e.B == 1);
                    break;
                case EventType.Sold:
                {
                    string price = L.G(e.A);
                    if (e.Seat == v.Viewer) ShowBanner(L.T("sold_to_you", price), ItemName(v.Items[e.Item]));
                    else if (e.B == v.Viewer) ShowBanner(L.T("sold_yours", price), SeatName(e.Seat));
                    else ShowBanner(L.T("sold_to", SeatName(e.Seat), price), ItemName(v.Items[e.Item]));
                    Feed(L.T("feed_sold", ItemName(v.Items[e.Item]), SeatName(e.Seat), price));
                    if (v.AuctionOrder.Count >= 3) Hint("penalty", L.T("hint_penalty"));
                    break;
                }
                case EventType.Unsold:
                    ShowBanner(L.T("unsold"), ItemName(v.Items[e.Item]));
                    Feed(L.T("feed_unsold", ItemName(v.Items[e.Item])));
                    break;
                case EventType.Deferred:
                    ShowBanner(L.T("deferred", L.G(e.A)), ItemName(v.Items[e.Item]));
                    Feed(L.T("feed_deferred", ItemName(v.Items[e.Item]), L.G(e.A)));
                    break;
                case EventType.LoanTaken:
                    Feed(L.T("feed_loan", SeatName(e.Seat), L.G(e.A)));
                    if (e.Seat != v.Viewer) UiRoot.I.Toast(L.T("feed_loan", SeatName(e.Seat), L.G(e.A)), null, 2.6f);
                    break;
                case EventType.EndedEarly:
                    UiRoot.I.Toast(L.T("ended_early"), null, 3f);
                    break;
                case EventType.ItemRevealed:
                case EventType.RevealStarted:
                case EventType.Results:
                    reveal.OnEvent(e, v);
                    if (e.Type == EventType.ItemRevealed && app.TableKit != null) app.TableKit.ShowReveal(e.Item, e.A, e.B);
                    break;
            }
        }

        void ShowAbilityResult(GameEvent e)
        {
            string text;
            var k = (AbilityKind)e.C;
            if (k == AbilityKind.Fake) text = L.T("ab_result_fake", e.B);
            else if (k == AbilityKind.Compare) text = e.B > 0 ? L.T("ab_result_higher", L.G(e.A)) : (e.B < 0 ? L.T("ab_result_lower", L.G(e.A)) : L.T("ab_result_equal", L.G(e.A)));
            else text = L.T("ab_result_range", L.G(e.A), L.G(e.B));
            resultText.text = text;
            resultCard.Show(true);
            Ui.FadeIn(resultCard, 0.3f, 12f);
            AudioManager.I.Play("ui_reveal", 0.9f, 1f, Bus.Ui);
        }

        void Feed(string line)
        {
            // 한국어: '나' 뒤에 붙는 높임말을 바로잡는다 (나님이 → 내가)
            // (이름이 '하나' 같은 사람은 건드리지 않게 낱말 처음의 '나'만)
            if (L.Current == Lang.Korean && line.Contains("나님"))
            {
                line = System.Text.RegularExpressions.Regex.Replace(line, @"(?<![\p{L}\p{N}])나님(이 |을 |에게|께)?", m =>
                {
                    switch (m.Groups[1].Value)
                    {
                        case "이 ": return "내가 ";
                        case "을 ": return "나를 ";
                        case "에게": case "께": return "나에게";
                        default: return "나";
                    }
                });
            }
            var l = Ui.Text(line, "feed-line");
            feed.Add(l);
            TrimFeed();
            Ui.FadeIn(l, 0.25f, 8f);
        }

        public void ShowBanner(string text, string sub, float seconds = 2.8f)
        {
            bannerText.text = text;
            bannerSub.text = sub ?? "";
            banner.RemoveFromClassList("banner-out");
            banner.AddToClassList("banner-in");
            bannerUntil = Time.unscaledTime + seconds;
        }

        void FlashTimer(string text)
        {
            timerText.text = text;
            Ui.Pop(timerText, 0.25f);
        }

        void Hint(string id, string text)
        {
            if (!SaveData.I.Settings.Hints || hintsShown.Contains(id)) return;
            if (SaveData.I.Stats.Games > 3) return;
            hintsShown.Add(id);
            UiRoot.I.Toast(text, "toast-hint", 6f);
        }

        void OnPhase(Phase now, Phase before)
        {
            if (now == Phase.Registration)
            {
                Hint("register", L.T("hint_register"));
                app.Rig.LookTowards(0f, -26f);
            }
            if (now == Phase.Reveal) app.Rig.LookTowards(0f, -22f);
            if (now == Phase.Presenting && before == Phase.Registration) app.Rig.LookTowards(0f, -12f);
            if (now == Phase.Reveal || now == Phase.Results) CloseLines();
        }

        public void ShowBubble(int seat, string text, bool seller)
        {
            if (seat < 0) return;
            foreach (var old in bubbles) if (old.Seat == seat) old.Until = 0;
            var el = Ui.Div("bubble");
            if (seller) el.AddToClassList("bubble-seller");
            el.pickingMode = PickingMode.Ignore;
            el.Add(Ui.Text(text, "bubble-text"));
            el.style.translate = new Translate(Length.Percent(-50), Length.Percent(-100));
            UiRoot.I.WorldLayer.Add(el);
            Ui.Pop(el, 0.15f);
            bubbles.Add(new Bubble { El = el, Seat = seat, Until = Time.unscaledTime + 3.4f });
        }

        /// <summary>좌석 위에 떠오르는 글자 (+1,500G 등).</summary>
        public void FloatText(int seat, string text, Color color)
        {
            // 1인칭에서는 이름표의 입찰가가 번쩍이므로 떠오르는 글씨는 겹쳐 보여 생략
            if (app.Rig.FirstPerson) return;
            Vector2 p;
            if (!UiRoot.I.WorldToPanel(app.Rig.Cam, app.Stage.SeatHead(seat) + Vector3.up * 0.25f, out p)) return;
            var l = Ui.Text(text, "float-text");
            l.style.color = color;
            l.pickingMode = PickingMode.Ignore;
            l.style.left = p.x;
            l.style.top = p.y;
            l.style.translate = new Translate(Length.Percent(-50), 0);
            UiRoot.I.WorldLayer.Add(l);
            l.schedule.Execute(() => { l.style.translate = new Translate(Length.Percent(-50), -70); l.style.opacity = 0; }).StartingIn(30);
            l.schedule.Execute(() => l.RemoveFromHierarchy()).StartingIn(1600);
        }

        // ═════════════════ 매 프레임 ═════════════════

        public void Tick()
        {
            var v = V;
            if (v == null || v.Seats.Length == 0) return;
            HandleKeys(v);
            TickFirstPerson(v);
            if (app.TableKit != null) app.TableKit.Tick();
            // 알림은 오른쪽 기록·능력 결과 바로 아래에 쌓는다 (겹치지 않게)
            if (rightCol.resolvedStyle.display == DisplayStyle.Flex && !float.IsNaN(rightCol.worldBound.yMax))
                UiRoot.I.ToastLayer.style.paddingTop = Mathf.Max(90f, rightCol.worldBound.yMax + 12f);
            if (v.Phase == Phase.Registration) registration.Tick(v);

            // 타이머
            if (top.resolvedStyle.display == DisplayStyle.Flex)
            {
                int ms = S.RemainingMs;
                float frac = v.DurationMs > 0 ? Mathf.Clamp01(ms / (float)v.DurationMs) : 0f;
                timerFill.style.width = Length.Percent(frac * 100f);
                bool hot = v.BiddingOpen && ms < 5000;
                timerFill.EnableInClassList("timer-fill-hot", hot);
                timerText.EnableInClassList("timer-text-hot", hot);
                int sec = Mathf.CeilToInt(ms / 1000f);
                if (v.Phase == Phase.Presenting) timerText.text = L.T("presenting");
                else if (v.Phase == Phase.AuctionResult) timerText.text = "";
                else if (sec != lastTickSecond || timerText.text.Length == 0) timerText.text = L.T("seconds", sec);
                if (v.BiddingOpen && sec != lastTickSecond && sec <= 5 && sec > 0)
                {
                    AudioManager.I.Play(sec <= 2 ? "sfx_tick_final" : "sfx_tick", 0.7f);
                    Ui.Pop(timerText, 0.12f);
                }
                lastTickSecond = sec;
            }

            if (bannerUntil > 0 && Time.unscaledTime > bannerUntil)
            {
                bannerUntil = 0;
                banner.RemoveFromClassList("banner-in");
                banner.AddToClassList("banner-out");
            }
            UpdateWorldLabels(v);
            TickSocial(v);
        }

        void UpdateWorldLabels(GameView v)
        {
            var cam = app.Rig.Cam;
            // 공개 · 결과 표가 화면을 덮는 동안에는 이름표를 숨긴다
            bool show = v.Phase != Phase.Registration && v.Phase != Phase.Results && v.Phase != Phase.Reveal;
            MeasureTopHud();
            // 1인칭 알림 글자(낙찰!·하나…)는 위 띠들 바로 아래에 (띠 위에 겹치지 않게)
            if (lookMode && topHud.height > 0f) bannerWrap.style.paddingTop = Mathf.Max(104f, topHud.yMax + 10f);
            for (int i = 0; i < nameplates.Count; i++)
            {
                var np = nameplates[i];
                if (!show || i >= v.SeatCount || i == v.Viewer) { np.Show(false); continue; }
                Vector2 p;
                if (!UiRoot.I.WorldToPanel(cam, app.Stage.SeatHead(i) + Vector3.up * 0.22f, out p)) { np.Show(false); continue; }
                np.Show(true);
                // 화면 밖으로 반쯤 나가지 않게 가장자리 안쪽으로, 위 가운데 띠와는 겹치지 않게
                var size = UiRoot.I.Root.layout.size;
                var at = AvoidHud(new Vector2(Mathf.Clamp(p.x, 90, Mathf.Max(90, size.x - 90)), Mathf.Clamp(p.y, 70, Mathf.Max(70, size.y - 20))), np);
                np.style.left = at.x;
                np.style.top = at.y;
                np.Q<VisualElement>(null, "dot").style.backgroundColor = SeatColor(i);
                np.Q<Label>(null, "nameplate-name").text = v.Seats[i].Name;
                // 최고 입찰자의 이름표에는 ★ 금액이 계속 남는다 (밀려난 사람은 입찰한 순간에만 잠깐)
                bool flash = Time.unscaledTime < bidFlash[i];
                bool inLot = v.Current != null && (v.BiddingOpen || v.Phase == Phase.AuctionResult);
                bool high = inLot && v.HighestBidder == i;
                nameplateBid[i].Show(flash || high);
                if (high) nameplateBid[i].text = "★ " + L.G(v.HighestBid);
                // 방금 밀려난 사람은 ★ 없이 부른 금액만 잠깐
                else if (flash) nameplateBid[i].text = L.G(bidFlashAmount[i]);
                np.EnableInClassList("player-row-high", high);
                UpdateNameplateSocial(v, i);
            }
            for (int b = bubbles.Count - 1; b >= 0; b--)
            {
                var bub = bubbles[b];
                if (Time.unscaledTime > bub.Until)
                {
                    bub.El.RemoveFromHierarchy();
                    bubbles.RemoveAt(b);
                    continue;
                }
                Vector2 p;
                if (bub.Seat == v.Viewer && app.Rig.FirstPerson)
                {
                    // 내 말은 화면 아래 입찰 안내 바로 위에 (가운데 물건을 가리지 않게)
                    bub.El.style.left = Length.Percent(50);
                    bub.El.style.top = Mathf.Max(200f, UiRoot.I.Root.layout.height - 150f);
                    continue;
                }
                Vector3 at = bub.Seat == v.Viewer ? cam.transform.position + cam.transform.forward * 1.2f - cam.transform.up * 0.15f : app.Stage.SeatHead(bub.Seat) + Vector3.up * 0.42f;
                if (UiRoot.I.WorldToPanel(cam, at, out p))
                {
                    var q = AvoidHud(p, bub.El);
                    bub.El.style.left = q.x;
                    bub.El.style.top = q.y;
                }
            }
        }

        /// <summary>위 가운데 띠(가격 · 능력 쓴 사람 · 최고 입찰 · 커서 모드 물건 카드)가 차지한 영역 (이번 프레임).</summary>
        Rect topHud, rightHud;

        void MeasureTopHud()
        {
            // 오른쪽 위 기록 · 능력 결과 · 일시정지 단추 묶음
            rightHud = rightCol != null && rightCol.panel != null && Shown(rightCol) ? rightCol.worldBound : Rect.zero;
            topHud = Rect.zero;
            bool any = false;
            var pill = fpTop != null ? fpTop.Q(className: "fp-pill") : null;
            foreach (var e in new[] { pill, abilityStrip, bidStrip, top })
            {
                if (e == null || e.panel == null || !Shown(e)) continue;
                var wb = e.worldBound;
                if (wb.width <= 1f || wb.height <= 1f) continue;
                topHud = any ? Rect.MinMaxRect(Mathf.Min(topHud.xMin, wb.xMin), Mathf.Min(topHud.yMin, wb.yMin), Mathf.Max(topHud.xMax, wb.xMax), Mathf.Max(topHud.yMax, wb.yMax)) : wb;
                any = true;
            }
        }

        static bool Shown(VisualElement e)
        {
            for (var x = e; x != null; x = x.parent)
                if (x.resolvedStyle.display == DisplayStyle.None || x.resolvedStyle.opacity < 0.05f) return false;
            return true;
        }

        /// <summary>
        /// 아래 가운데가 기준점인 표시(이름표·말풍선)가 위 띠와 겹치면 띠 바로 아래로 내리고,
        /// 오른쪽 위 기록·능력 결과 묶음과 겹치면 그 왼쪽으로 민다.
        /// </summary>
        Vector2 AvoidHud(Vector2 p, VisualElement el)
        {
            float h = el.layout.height > 1f ? el.layout.height : 56f, w = el.layout.width > 1f ? el.layout.width : 160f;
            if (topHud.width > 0f && p.x + w * 0.5f >= topHud.xMin - 6f && p.x - w * 0.5f <= topHud.xMax + 6f)
                p.y = Mathf.Max(p.y, topHud.yMax + h + 6f);
            if (rightHud.width > 0f && p.x + w * 0.5f >= rightHud.xMin - 6f && p.y - h <= rightHud.yMax + 6f && p.y >= rightHud.yMin)
                p.x = Mathf.Min(p.x, rightHud.xMin - w * 0.5f - 6f);
            return p;
        }

        static bool TextFocused()
        {
            var f = UiRoot.I.Root.panel != null ? UiRoot.I.Root.panel.focusController.focusedElement : null;
            return f is TextField || (f is VisualElement ve && ve.GetFirstAncestorOfType<TextField>() != null);
        }

        void HandleKeys(GameView v)
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool esc = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (esc)
            {
                if (linesPopup != null) CloseLines();
                else app.Presenter.TogglePause();
                return;
            }
            if (app.Session.Paused || UiRoot.I.ModalOpen || TextFocused()) return;
            bool auction = v.BiddingOpen || v.Phase == Phase.Presenting;
            if (!auction) return;
            if ((kb != null && kb.spaceKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame)) PressBid();
            // 바퀴: 올릴 금액
            var mouse = Mouse.current;
            if (mouse != null && !app.Rig.ZoomHeld)
            {
                float sc = mouse.scroll.ReadValue().y;
                if (sc > 0.1f) SelectChip(chip + 1);
                else if (sc < -0.1f) SelectChip(chip - 1);
            }
            if (kb != null && !WheelOpen)
            {
                if (kb.digit1Key.wasPressedThisFrame) SelectChip(0);
                if (kb.digit2Key.wasPressedThisFrame) SelectChip(1);
                if (kb.digit3Key.wasPressedThisFrame) SelectChip(2);
                if (kb.digit4Key.wasPressedThisFrame) SelectChip(3);
                if (kb.qKey.wasPressedThisFrame) UseAbility(AbilityKind.Compare);
                if (kb.wKey.wasPressedThisFrame) UseAbility(AbilityKind.Appraise);
                if (kb.eKey.wasPressedThisFrame) UseAbility(AbilityKind.Fake);
                if (kb.tKey.wasPressedThisFrame) ToggleLines();
            }
            if (pad != null)
            {
                if (pad.dpad.right.wasPressedThisFrame) SelectChip(chip + 1);
                if (pad.dpad.left.wasPressedThisFrame) SelectChip(chip - 1);
                if (pad.buttonWest.wasPressedThisFrame) UseAbility(AbilityKind.Compare);
                if (pad.buttonNorth.wasPressedThisFrame) UseAbility(AbilityKind.Appraise);
            }
        }
    }
}
