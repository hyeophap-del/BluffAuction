using System.Collections.Generic;
using BluffAuction.Characters;
using BluffAuction.Core;
using BluffAuction.Game;
using BluffAuction.Loc;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using EventType = BluffAuction.Core.EventType;

namespace BluffAuction.UI
{
    /// <summary>
    /// 게임 화면의 어울리기: 채팅 상자 · 감정 표현 바퀴(R) · 누가 능력을 썼는지 늘 보이는 표시 · 말하는 사람 표시 · 맞았을 때 화면 얼룩.
    /// </summary>
    public sealed partial class GameScreen
    {
        ChatBox chat;
        VisualElement wheel, splat, abilityStrip;
        Label abilityStripText;
        readonly List<Label> wheelItems = new List<Label>();
        bool wheelOpen;
        int wheelPick = -1, wheelTarget = -1;
        Vector2 wheelAim;
        readonly List<VisualElement> nameplateTalk = new List<VisualElement>();
        readonly List<VisualElement> nameplateAbility = new List<VisualElement>();
        readonly List<Label> nameplateAbilityText = new List<Label>();
        // 이번 물건에서 좌석마다 능력을 쓴 횟수 · 판 전체 횟수
        readonly Dictionary<int, int> lotAbilities = new Dictionary<int, int>();
        readonly Dictionary<int, int> gameAbilities = new Dictionary<int, int>();
        readonly float[] abilityFlash = new float[8];
        int abilityLot = -1;

        static readonly Gesture[] WheelEmotes = { Gesture.Wave, Gesture.ThumbsUp, Gesture.Clap, Gesture.Peace, Gesture.Mock, Gesture.Facepalm, Gesture.Angry, Gesture.MiddleFinger };

        void BuildSocial()
        {
            chat = new ChatBox(SeatName, SeatColor, false);
            chat.Root.AddToClassList("chatbox-game");
            Root.Add(chat.Root);
            chat.OnLine = (seat, text) => ShowBubble(seat, text, false);

            // 감정 표현 바퀴
            wheel = Ui.Div("emote-wheel");
            wheel.pickingMode = PickingMode.Ignore;
            var center = Ui.Text(L.T("emote_title"), "emote-center");
            center.pickingMode = PickingMode.Ignore;
            wheel.Add(center);
            for (int i = 0; i < WheelEmotes.Length; i++)
            {
                float a = (90f - i * 360f / WheelEmotes.Length) * Mathf.Deg2Rad;
                var item = Ui.Text((i + 1) + "  " + L.T("emote_" + WheelEmotes[i].ToString().ToLowerInvariant()), "emote-item");
                item.pickingMode = PickingMode.Ignore;
                item.style.left = Length.Percent(50 + Mathf.Cos(a) * 38f);
                item.style.top = Length.Percent(50 - Mathf.Sin(a) * 38f);
                wheel.Add(item);
                wheelItems.Add(item);
            }
            wheel.Show(false);
            Root.Add(wheel);

            // 맞았을 때 화면 가장자리 얼룩
            splat = Ui.Div("splat");
            splat.pickingMode = PickingMode.Ignore;
            splat.style.opacity = 0;
            Root.Add(splat);

            // 위 가격 띠 아래: [판매자 · 최고 입찰] → 이번 물건에 능력을 쓴 사람들
            BuildBidStrip();
            abilityStrip = Ui.Row("ability-strip");
            abilityStrip.pickingMode = PickingMode.Ignore;
            var icon = Ui.Div("ability-icon");
            abilityStripText = Ui.Text("", "ability-strip-text");
            abilityStrip.Add(icon, abilityStripText);
            abilityStrip.Show(false);
            fpTop.Add(abilityStrip);

            var hub = SocialHub.I;
            if (hub != null)
            {
                hub.EmoteShown += OnEmoteShown;
                hub.HitShown += OnHitShown;
                hub.BonkShown += OnBonkShown;
            }
            Root.RegisterCallback<DetachFromPanelEvent>(e =>
            {
                if (SocialHub.I == null) return;
                SocialHub.I.EmoteShown -= OnEmoteShown;
                SocialHub.I.HitShown -= OnHitShown;
                SocialHub.I.BonkShown -= OnBonkShown;
            });
        }

        /// <summary>이름표에 말하는 중 표시 · 능력 사용 표시를 붙인다 (BuildNameplates 가 부른다).</summary>
        void DecorateNameplate(VisualElement row)
        {
            var talk = Ui.Div("np-talk");
            talk.Show(false);
            // 지금 물건의 판매자
            var sell = Ui.Row("np-seller");
            sell.Add(Ui.Div("seller-icon"), Ui.Text(L.T("np_seller"), "np-seller-text"));
            sell.Show(false);
            var abl = Ui.Row("np-ability");
            abl.Add(Ui.Div("ability-icon"));
            var t = Ui.Text("", "np-ability-text");
            abl.Add(t);
            abl.Show(false);
            row.Add(talk, sell, abl);
            nameplateTalk.Add(talk);
            nameplateSeller.Add(sell);
            nameplateAbility.Add(abl);
            nameplateAbilityText.Add(t);
        }

        readonly List<VisualElement> nameplateSeller = new List<VisualElement>();

        void OnEmoteShown(int seat, Gesture g, int target)
        {
            if (V == null || seat < 0 || seat >= V.SeatCount) return;
            string name = L.T("emote_" + g.ToString().ToLowerInvariant());
            if (target >= 0 && target < V.SeatCount && target != seat) Feed(L.T("feed_emote_to", SeatName(seat), SeatName(target), name));
            else Feed(L.T("feed_emote", SeatName(seat), name));
        }

        void OnBonkShown(int attacker, int victim, int kind)
        {
            if (V == null || attacker < 0 || victim < 0 || attacker >= V.SeatCount || victim >= V.SeatCount) return;
            Feed(L.T("feed_bonk", SeatName(attacker), SeatName(victim), L.T("melee_with_0")));
        }

        void OnHitShown(int thrower, int victim)
        {
            if (V == null || thrower < 0 || victim < 0 || thrower >= V.SeatCount || victim >= V.SeatCount) return;
            Feed(L.T("feed_hit", SeatName(thrower), SeatName(victim)));
        }

        /// <summary>이번 물건의 능력 사용·입찰 기록 (사건마다).</summary>
        void TrackAbility(GameEvent e, GameView v)
        {
            bool lotEvent = e.Type == EventType.LotPresented || e.Type == EventType.AbilityUsed || e.Type == EventType.BidPlaced;
            if (lotEvent && e.Item >= 0 && e.Item != abilityLot)
            {
                // 새 물건: 둘 다 비운다
                lotAbilities.Clear();
                lotBids.Clear();
                abilityLot = e.Item;
                abilityStrip.Show(false);
                bidPart.Show(false);
            }
            if (e.Type == EventType.BidPlaced && e.Seat >= 0)
            {
                lotBids[e.Seat] = e.A;
                lastBidder = e.Seat;
                lastBidAt = Time.unscaledTime;
                RefreshBidStrip(v);
                return;
            }
            if (e.Type != EventType.AbilityUsed || e.Seat < 0) return;
            int n;
            lotAbilities.TryGetValue(e.Seat, out n);
            lotAbilities[e.Seat] = n + 1;
            gameAbilities.TryGetValue(e.Seat, out n);
            gameAbilities[e.Seat] = n + 1;
            if (e.Seat < abilityFlash.Length) abilityFlash[e.Seat] = Time.unscaledTime + 1.6f;
            RefreshAbilityStrip(v);
        }

        void RefreshAbilityStrip(GameView v)
        {
            var parts = new List<string>();
            foreach (var kv in lotAbilities)
                if (kv.Value > 0 && kv.Key < v.SeatCount) parts.Add(SeatName(kv.Key) + " ×" + kv.Value);
            abilityStripText.text = L.T("ab_strip") + "  " + string.Join("  ·  ", parts);
            abilityStrip.Show(parts.Count > 0);
            if (parts.Count > 0) Ui.Pop(abilityStrip, 0.15f);
        }

        public int AbilityUsesThisGame(int seat)
        {
            int n;
            return gameAbilities.TryGetValue(seat, out n) ? n : 0;
        }

        // ── 위 띠: [판매자 ● 이름] [최고 입찰 ★ 이름 금액]. 판매자는 물건이 나와 있는 내내, 입찰은 입찰이 있을 때만
        readonly Dictionary<int, int> lotBids = new Dictionary<int, int>();
        VisualElement bidStrip, bidChips, bidPart, sellerPart, sellerChip, sellerDot;
        Label sellerName;
        int lastBidder = -1, shownSeller = -2;
        float lastBidAt;

        void BuildBidStrip()
        {
            bidStrip = Ui.Row("bid-strip");
            bidStrip.pickingMode = PickingMode.Ignore;
            sellerPart = Ui.Row("strip-part");
            sellerPart.pickingMode = PickingMode.Ignore;
            sellerChip = Ui.Row("seller-chip");
            sellerDot = Ui.Dot(Color.white, 9);
            sellerName = Ui.Text("", "seller-chip-text");
            sellerChip.Add(sellerDot, sellerName);
            sellerPart.Add(Ui.Div("seller-icon"), Ui.Text(L.T("seller_strip"), "bid-strip-label"), sellerChip);
            bidPart = Ui.Row("strip-part", "bid-part");
            bidPart.pickingMode = PickingMode.Ignore;
            var icon = Ui.Div("bid-icon");
            var label = Ui.Text(L.T("bid_strip"), "bid-strip-label");
            bidChips = Ui.Row();
            bidChips.pickingMode = PickingMode.Ignore;
            bidPart.Add(icon, label, bidChips);
            bidPart.Show(false);
            sellerPart.Show(false);
            bidStrip.Add(sellerPart, bidPart);
            bidStrip.Show(false);
            fpTop.Add(bidStrip);
        }

        /// <summary>지금 나와 있는 물건의 판매자 (소개 · 입찰 · 유찰 · 낙찰 동안, 없으면 -1).</summary>
        static int LotSeller(GameView v)
        {
            if (v.Current == null) return -1;
            bool lot = v.Phase == Phase.Presenting || v.BiddingOpen || v.Phase == Phase.PriceDrop || v.Phase == Phase.AuctionResult;
            return lot && v.Current.Seller >= 0 && v.Current.Seller < v.SeatCount ? v.Current.Seller : -1;
        }

        static bool On(VisualElement e) { return e.style.display.value != DisplayStyle.None; }

        void TickSeller(GameView v)
        {
            int seller = LotSeller(v);
            if (seller != shownSeller)
            {
                shownSeller = seller;
                if (seller >= 0)
                {
                    bool me = seller == v.Viewer;
                    sellerName.text = me ? L.T("seller_me") : SeatName(seller);
                    sellerDot.style.backgroundColor = SeatColor(seller);
                    sellerChip.EnableInClassList("seller-chip-me", me);
                    Ui.Pop(sellerChip, 0.2f);
                }
                sellerPart.Show(seller >= 0);
            }
            bidStrip.Show(On(sellerPart) || On(bidPart));
        }

        /// <summary>이번 물건에 이 좌석이 낸 마지막 입찰 (없으면 0).</summary>
        public int LotBidOf(int seat)
        {
            int a;
            return lotBids.TryGetValue(seat, out a) ? a : 0;
        }

        void RefreshBidStrip(GameView v)
        {
            bidChips.Clear();
            var order = new List<KeyValuePair<int, int>>(lotBids);
            order.Sort((a, b) => b.Value.CompareTo(a.Value));
            int shown = 0;
            foreach (var kv in order)
            {
                if (kv.Key >= v.SeatCount) continue;
                // 최고 입찰자 한 명만 보인다
                if (shown++ >= 1) break;
                bool high = true;
                var chip = Ui.Row("bid-chip");
                var dot = Ui.Dot(SeatColor(kv.Key), 9);
                var t = Ui.Text((high ? "★ " : "") + SeatName(kv.Key) + "  " + L.G(kv.Value), "bid-chip-text");
                chip.Add(dot, t);
                chip.EnableInClassList("bid-chip-high", high);
                chip.EnableInClassList("bid-chip-me", kv.Key == v.Viewer);
                if (kv.Key == lastBidder && Time.unscaledTime - lastBidAt < 0.5f) Ui.Pop(chip, 0.2f);
                bidChips.Add(chip);
            }
            bidPart.Show(shown > 0);
        }

        void UpdateNameplateSocial(GameView v, int i)
        {
            if (i >= nameplateTalk.Count) return;
            var hub = SocialHub.I;
            bool talking = hub != null && hub.IsTalking(i);
            nameplateTalk[i].Show(talking);
            if (talking) nameplateTalk[i].style.opacity = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f));
            if (i < nameplateSeller.Count) nameplateSeller[i].Show(LotSeller(v) == i);
            int n;
            bool used = lotAbilities.TryGetValue(i, out n) && n > 0 && v.Current != null && v.Current.Id == abilityLot;
            nameplateAbility[i].Show(used);
            if (used)
            {
                nameplateAbilityText[i].text = "×" + n;
                bool flash = i < abilityFlash.Length && Time.unscaledTime < abilityFlash[i];
                nameplateAbility[i].EnableInClassList("np-ability-flash", flash);
            }
        }

        void TickSocial(GameView v)
        {
            // 물건이 바뀌면 능력·입찰 표시를 비운다 (낙찰 뒤에는 입찰 경쟁도 접는다)
            bool sameLot = v.Current != null && v.Current.Id == abilityLot;
            if (!sameLot)
            {
                if (On(abilityStrip)) abilityStrip.Show(false);
                if (On(bidPart)) bidPart.Show(false);
            }
            else
            {
                if (lotAbilities.Count > 0 && !On(abilityStrip)) RefreshAbilityStrip(v);
                bool live = v.BiddingOpen || v.Phase == Phase.AuctionResult;
                if (live && lotBids.Count > 0 && !On(bidPart)) RefreshBidStrip(v);
                if (!live && On(bidPart)) bidPart.Show(false);
            }
            TickSeller(v);

            // 맞았을 때 얼룩
            float sp = app.Throwables != null ? app.Throwables.SplatAmount : 0f;
            int kind = app.Throwables != null ? app.Throwables.SplatKind : -1;
            // 물렁한 것은 잠깐 그대로 있다가 흐려지고, 딱딱한 것은 짧게 어두워진다
            splat.style.opacity = kind >= 0 ? Mathf.Clamp01(sp * 1.8f) * 0.9f : sp * 0.6f;
            splat.EnableInClassList("splat-tomato", kind == 3);
            splat.EnableInClassList("splat-apple", kind == 1);

            TickWheel(v);
        }

        void TickWheel(GameView v)
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            bool can = !chat.Open && !UiRoot.I.TextFocused && !UiRoot.I.ModalOpen && !S.Paused && v.Phase != Phase.Results;
            if (!wheelOpen && can && kb.rKey.wasPressedThisFrame)
            {
                wheelOpen = true;
                wheelPick = -1;
                wheelAim = Vector2.zero;
                wheelTarget = app.Throwables != null ? app.Throwables.AimedSeat() : -1;
                if (wheelTarget == v.Viewer) wheelTarget = -1;
                app.Rig.FreezeLook = true;
                wheel.Show(true);
                Ui.Pop(wheel, 0.12f);
            }
            if (!wheelOpen) return;
            // 마우스를 움직인 쪽 (또는 숫자 1~8) 이 고른 것
            var mouse = Mouse.current;
            if (mouse != null)
            {
                wheelAim += mouse.delta.ReadValue();
                if (wheelAim.magnitude > 140f) wheelAim = wheelAim.normalized * 140f;
                if (wheelAim.magnitude > 35f)
                {
                    float ang = Mathf.Atan2(wheelAim.y, wheelAim.x) * Mathf.Rad2Deg;
                    float fromTop = (90f - ang + 360f) % 360f;
                    wheelPick = Mathf.RoundToInt(fromTop / (360f / WheelEmotes.Length)) % WheelEmotes.Length;
                }
            }
            var digits = new[] { kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key, kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key };
            for (int i = 0; i < digits.Length; i++) if (digits[i].wasPressedThisFrame) wheelPick = i;
            for (int i = 0; i < wheelItems.Count; i++) wheelItems[i].EnableInClassList("emote-item-on", i == wheelPick);
            if (!kb.rKey.isPressed)
            {
                wheelOpen = false;
                wheel.Show(false);
                app.Rig.FreezeLook = false;
                if (wheelPick >= 0 && SocialHub.I != null) SocialHub.I.SendEmote(WheelEmotes[wheelPick], wheelTarget);
            }
        }

        /// <summary>바퀴가 열려 있으면 숫자 키를 입찰 금액에 쓰지 않는다.</summary>
        bool WheelOpen { get { return wheelOpen; } }

        // ── 개발용 자동 촬영
        public void DevOpenChat() { chat.OpenInput(); }
        public bool DevChatOpen { get { return chat != null && chat.Open; } }
        public void DevCloseChat() { if (chat != null) chat.Cancel(); }
        public void DevShowWheel(bool on, int pick)
        {
            wheel.Show(on);
            for (int i = 0; i < wheelItems.Count; i++) wheelItems[i].EnableInClassList("emote-item-on", i == pick);
        }
    }
}
