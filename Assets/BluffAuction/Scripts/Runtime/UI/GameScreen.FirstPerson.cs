using BluffAuction.Core;
using BluffAuction.Game;
using BluffAuction.Loc;
using BluffAuction.Table;
using BluffAuction.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    /// <summary>
    /// 1인칭 화면 (라이어스 바처럼): 마우스로 고개를 돌려 상대·물건·탁자 위 카드를 직접 보고, 화면에는 조준점·작은 가격 띠·입찰 안내만.
    /// Tab 을 누르면 커서가 나와 자세한 패널(참가자·지갑·능력 버튼)을 마우스로 쓸 수 있다.
    /// </summary>
    public sealed partial class GameScreen
    {
        VisualElement fpLayer, crosshair, fpTop, fpBottom, fpKeys;
        Label hoverTip, fpLabel, fpPrice, fpWho, fpTime, fpBid, fpReason;
        VisualElement fpFill;
        readonly Label[] toolCounts = new Label[3];
        bool cursorMode;
        bool lookMode;
        /// <summary>들고 있는 물건으로 조준점이 가리키는 사람 (없으면 -1) · 근접 무기가 닿는지.</summary>
        int aimSeat = -1;
        bool aimInReach;
        /// <summary>1인칭이라 아래 입찰 안내를 쓸 때 (안의 내용이 없으면 상자는 숨긴다).</summary>
        bool fpBottomWanted;
        int eyeTarget = -1;
        float eyeTime;
        float nextGlanceBack;

        void BuildFirstPerson()
        {
            fpLayer = Ui.Div("screen");
            fpLayer.pickingMode = PickingMode.Ignore;
            Root.Insert(0, fpLayer);

            crosshair = Ui.Div("crosshair");
            crosshair.pickingMode = PickingMode.Ignore;
            hoverTip = Ui.Text("", "hover-tip");
            hoverTip.pickingMode = PickingMode.Ignore;
            hoverTip.Show(false);
            fpLayer.Add(crosshair, hoverTip);

            fpTop = Ui.Col("fp-top");
            fpTop.pickingMode = PickingMode.Ignore;
            var pill = Ui.Row("fp-pill");
            fpLabel = Ui.Text("", "fp-pill-label");
            fpPrice = Ui.Text("", "fp-pill-price");
            fpWho = Ui.Text("", "fp-pill-who");
            fpTime = Ui.Text("", "fp-pill-time");
            fpTime.style.marginLeft = 18;
            pill.Add(fpLabel, fpPrice, fpWho, fpTime);
            var track = Ui.Div("fp-track");
            fpFill = Ui.Div("fp-fill");
            track.Add(fpFill);
            fpTop.Add(pill, track);
            fpLayer.Add(fpTop);

            fpBottom = Ui.Col("fp-bottom");
            fpBottom.pickingMode = PickingMode.Ignore;
            fpBid = Ui.Text("", "fp-bid");
            fpReason = Ui.Text("", "fp-reason");
            var keys = fpKeys = Ui.Row("fp-keys");
            keys.Add(Ui.Text(L.T("fp_keys_bid"), "fp-key-hint"), Ui.Text(L.T("fp_keys_tools"), "fp-key-hint"), Ui.Text(L.T("fp_keys_look"), "fp-key-hint"), Ui.Text(L.T("fp_keys_tab"), "fp-key-hint"));
            fpBottom.Add(fpBid, fpReason, keys);
            fpLayer.Add(fpBottom);

            for (int i = 0; i < 3; i++)
            {
                toolCounts[i] = Ui.Text("", "tool-count");
                toolCounts[i].pickingMode = PickingMode.Ignore;
                UiRoot.I.WorldLayer.Add(toolCounts[i]);
            }

            var kit = app.TableKit;
            if (kit != null)
            {
                kit.Tools[0].OnClick = () => UseAbility(AbilityKind.Compare);
                kit.Tools[1].OnClick = () => UseAbility(AbilityKind.Appraise);
                kit.Tools[2].OnClick = () => UseAbility(AbilityKind.Fake);
                for (int i = 0; i < 3; i++)
                {
                    int k = i;
                    kit.Tools[i].Usable = () => ToolUsable(k);
                }
            }
            BindPaddle();
        }

        /// <summary>탁자 위 내 패들을 가리켜 누르면 입찰.</summary>
        void BindPaddle()
        {
            var it = app.TableKit != null ? app.TableKit.PaddleTarget : null;
            if (it == null) return;
            it.OnClick = PressBid;
            it.Usable = () => BidUsable();
            it.Tooltip = () => BidUsable() ? L.T("bid_button", L.G(ChipAmount())) + "\n" + L.T("fp_wheel") : BidReason();
        }

        bool ToolUsable(int k)
        {
            var v = V;
            if (v == null || v.Me == null || !v.BiddingOpen || v.IAmSeller) return false;
            int left = k == 0 ? v.Me.CompareLeft : (k == 1 ? v.Me.AppraiseLeft : v.Me.FakeLeft);
            return left > 0;
        }

        bool BidUsable()
        {
            var v = V;
            return v != null && v.BiddingOpen && !v.IAmSeller && !v.IAmHighest && ChipAmount() <= v.MyAvailable;
        }

        string BidReason()
        {
            var v = V;
            if (v == null) return "";
            if (!v.BiddingOpen) return L.T("bid_blocked_wait");
            if (v.IAmSeller) return L.T("bid_blocked_seller");
            if (v.IAmHighest) return L.T("bid_blocked_high");
            if (ChipAmount() > v.MyAvailable) return L.T("bid_blocked_cash");
            return "";
        }

        bool AuctionPhase(GameView v)
        {
            return v.Phase == Phase.Presenting || v.Phase == Phase.Bidding || v.Phase == Phase.PriceDrop || v.Phase == Phase.Reauction || v.Phase == Phase.AuctionResult;
        }

        /// <summary>마우스로 보기 / 커서 모드에 따라 패널을 보이고 숨긴다.</summary>
        void RefreshMode(GameView v)
        {
            bool auction = AuctionPhase(v);
            bool fp = auction && lookMode;
            PlaceBanner(fp);
            if (chat != null) chat.Root.EnableInClassList("chatbox-raised", !fp && v.Phase != Phase.Reveal && v.Phase != Phase.Results);
            fpTop.Show(fp && v.Current != null);
            fpBottomWanted = fp;
            fpBottom.Show(fp);
            crosshair.Show(fp);
            // 커서 모드에서는 예전처럼 자세한 패널
            top.Show(auction && v.Current != null && !fp);
            bidBar.parent.Show(auction && !fp);
            wallet.Show((auction && !fp) || v.Phase == Phase.Registration);
            abilities.Show(auction && !fp);
            players.Show(!fp && v.Phase != Phase.Reveal && v.Phase != Phase.Results);
        }

        /// <summary>기록은 7줄까지 두고, 1인칭에서는 최근 4줄만 보인다 (화면을 덜 가리게).</summary>
        void TrimFeed()
        {
            while (feed.childCount > 7) feed.RemoveAt(0);
            int shown = lookMode ? 4 : 7;
            for (int i = 0; i < feed.childCount; i++) feed[i].Show(i >= feed.childCount - shown);
        }

        void ReleaseCursor()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            if (app.Rig != null) app.Rig.MouseLook = false;
        }

        void TickFirstPerson(GameView v)
        {
            var kb = Keyboard.current;
            bool auction = AuctionPhase(v);
            // 채팅 중에는 1인칭 화면은 그대로 두고 커서만 풀어 준다
            bool typing = chat != null && chat.Open;
            if (kb != null && kb.tabKey.wasPressedThisFrame && auction && !typing)
            {
                cursorMode = !cursorMode;
                Audio.AudioManager.I.Play(cursorMode ? "ui_open" : "ui_close", 0.5f, 1f, Audio.Bus.Ui);
            }
            bool blocked = S.Paused || UiRoot.I.ModalOpen || (TextFocused() && !typing) || linesPopup != null;
            bool focused = Application.isFocused || Dev.AutoShots.Requested;
            bool want = auction && !cursorMode && !blocked && SaveData.I.Settings.MouseLook && focused;
            if (want != lookMode)
            {
                lookMode = want;
                RefreshMode(v);
                TrimFeed();
            }
            bool locked = lookMode && !typing;
            app.Rig.MouseLook = locked;
            UnityEngine.Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            UnityEngine.Cursor.visible = !locked;

            // 들고 있을 때: 조준점 아래 사람 (근접 무기는 닿는지까지)
            aimSeat = -1;
            aimInReach = false;
            var th = app.Throwables;
            if (th != null && th.Holding && lookMode)
            {
                if (th.HoldingWeapon) aimSeat = th.MeleeAim(out aimInReach);
                else { aimSeat = th.AimedSeat(); aimInReach = aimSeat >= 0; }
            }
            crosshair.EnableInClassList("crosshair-target", aimSeat >= 0 && aimInReach);
            crosshair.EnableInClassList("crosshair-far", aimSeat >= 0 && !aimInReach);

            // 가리킨 물건 안내
            var hov = aimSeat >= 0 ? null : Interactor.Hovered;
            crosshair.EnableInClassList("crosshair-hover", hov != null);
            if (hov != null && hov.Tooltip != null)
            {
                hoverTip.text = hov.Tooltip();
                hoverTip.Show(true);
                if (!lookMode)
                {
                    var mouse = Mouse.current;
                    if (mouse != null)
                    {
                        Vector2 mp = mouse.position.ReadValue();
                        var pp = RuntimePanelUtils.ScreenToPanel(UiRoot.I.Root.panel, new Vector2(mp.x, Screen.height - mp.y));
                        hoverTip.style.left = pp.x;
                        hoverTip.style.top = pp.y;
                    }
                }
                else
                {
                    hoverTip.style.left = Length.Percent(50);
                    hoverTip.style.top = Length.Percent(50);
                }
            }
            else hoverTip.Show(false);

            // 위 띠: 가격·입찰자·남은 시간
            var it = v.Current;
            if (fpTop.resolvedStyle.display == DisplayStyle.Flex && it != null)
            {
                int ms = S.RemainingMs;
                bool hasBid = v.HighestBidder != -1;
                fpLabel.text = (it.Status == ItemStatus.Sold ? L.T("reveal_paid") : (hasBid ? L.T("current_bid") : L.T("start_price"))).ToUpperInvariant();
                fpPrice.text = L.G(it.Status == ItemStatus.Sold ? it.SalePrice : (hasBid ? v.HighestBid : it.StartPrice));
                fpWho.text = it.Status == ItemStatus.Sold ? SeatName(it.Winner) : (hasBid ? (v.IAmHighest ? L.T("you_are_high") : SeatName(v.HighestBidder)) : GameScreen.ItemName(it));
                fpTime.text = v.BiddingOpen || v.Phase == Phase.PriceDrop ? Mathf.CeilToInt(ms / 1000f).ToString() : "";
                bool hot = v.BiddingOpen && ms < 5000;
                fpTime.EnableInClassList("timer-text-hot", hot);
                float frac = v.DurationMs > 0 ? Mathf.Clamp01(ms / (float)v.DurationMs) : 0f;
                fpFill.style.width = Length.Percent(frac * 100f);
                fpFill.EnableInClassList("timer-fill-hot", hot);
            }
            if (fpBottomWanted)
            {
                // 입찰을 받는 동안에만 입찰 금액을 보인다 (소개 · 낙찰 중에는 탁자 위 물건 카드의 소문·시작가를 가리지 않게)
                bool presenting = !v.BiddingOpen && !v.IAmSeller;
                fpBid.Show(!presenting);
                bool ok = BidUsable();
                fpBid.text = v.IAmSeller ? L.T("fp_seller") : L.T("bid_button", L.G(ChipAmount())) + (chip > 0 ? "   (+" + chipSteps[chip].ToString("#,0") + ")" : "");
                fpBid.style.opacity = ok || v.IAmSeller ? 1f : 0.55f;
                fpReason.text = ok || v.IAmSeller || presenting ? "" : BidReason();
                if (app.Throwables != null && app.Throwables.Holding)
                {
                    bool weapon = app.Throwables.HoldingWeapon;
                    if (aimSeat >= 0 && aimInReach) fpReason.text = L.T(weapon ? "melee_hint_target" : "throw_hint_target", SeatName(aimSeat));
                    else if (aimSeat >= 0) fpReason.text = L.T("melee_far");
                    else fpReason.text = L.T(weapon ? "melee_hint" : "throw_hint");
                }
                fpReason.Show(fpReason.text.Length > 0);
                // 조작 안내는 처음 두 물건 동안만 (익숙해지면 화면을 비운다)
                fpKeys.Show(v.AuctionOrder.Count <= 2 && !v.LeftoverPass);
                // 보일 것이 없으면 바탕 상자도 숨긴다
                fpBottom.Show(On(fpBid) || On(fpReason) || On(fpKeys));
            }

            // 능력 도구 위 남은 횟수
            var kit = app.TableKit;
            for (int i = 0; i < 3; i++)
            {
                var l = toolCounts[i];
                // 커서 모드에서는 능력 버튼에 횟수가 있으니 숨긴다
                if (kit == null || !auction || !lookMode || kit.Tools[i] == null) { l.Show(false); continue; }
                Vector2 p;
                if (!UiRoot.I.WorldToPanel(app.Rig.Cam, kit.ToolBadge(i), out p)) { l.Show(false); continue; }
                var me = v.Me;
                int left = me == null ? 0 : (i == 0 ? me.CompareLeft : (i == 1 ? me.AppraiseLeft : me.FakeLeft));
                l.text = "×" + left;
                l.style.left = p.x;
                l.style.top = p.y;
                l.style.opacity = ToolUsable(i) ? 1f : 0.45f;
                l.Show(true);
            }

            TickEyeContact(v);
        }

        /// <summary>상대를 한동안 바라보면 그 사람도 나를 마주 본다 (가끔 몸짓·한마디).</summary>
        void TickEyeContact(GameView v)
        {
            if (!app.Rig.FirstPerson) return;
            var cam = app.Rig.Cam;
            int looking = -1;
            float best = 7f;
            for (int i = 0; i < app.Stage.Guests.Count; i++)
            {
                if (i == v.Viewer) continue;
                Vector3 head = app.Stage.SeatHead(i);
                float ang = Vector3.Angle(cam.transform.forward, head - cam.transform.position);
                if (ang < best) { best = ang; looking = i; }
            }
            if (looking != eyeTarget) { eyeTarget = looking; eyeTime = 0; }
            else eyeTime += Time.unscaledDeltaTime;
            if (eyeTarget < 0 || eyeTime < 0.9f || Time.unscaledTime < nextGlanceBack) return;
            var g = app.Stage.Guests[eyeTarget];
            g.LookAt(cam.transform.position);
            nextGlanceBack = Time.unscaledTime + Random.Range(3f, 6f);
            if (Random.value < 0.35f && !g.Busy) g.Play(Random.value < 0.5f ? Characters.Gesture.Shrug : Characters.Gesture.Laugh, 1.3f);
            // 잠시 뒤 다시 물건을 본다
            g.StartCoroutineSafe(2.5f, () => g.LookAt(app.Stage.ItemPoint));
        }
    }
}
