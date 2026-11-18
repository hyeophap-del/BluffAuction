using System.Collections.Generic;
using BluffAuction.Audio;
using BluffAuction.Core;
using BluffAuction.Game;
using BluffAuction.Loc;
using BluffAuction.World;
using UnityEngine;
using EventType = BluffAuction.Core.EventType;
using UnityEngine.UIElements;

namespace BluffAuction.UI
{
    /// <summary>
    /// 공개(물건마다 진짜 가치가 하나씩 드러남)와 최종 순위.
    /// 공개 중에는 오른쪽 목록에 한 줄씩 쌓이고, 결과가 나오면 가운데에 순위·점수 내역·최고/최악의 거래를 보여 준다.
    /// </summary>
    public sealed class RevealPanel
    {
        public readonly VisualElement Root;
        readonly GameApp app;
        readonly VisualElement list, listPanel, results;
        readonly HashSet<int> shown = new HashSet<int>();
        bool resultsBuilt;

        public RevealPanel(GameApp app)
        {
            this.app = app;
            Root = Ui.Div("screen");
            Root.pickingMode = PickingMode.Ignore;
            listPanel = Ui.Col("panel");
            listPanel.style.position = Position.Absolute;
            listPanel.style.right = 30;
            // 오른쪽 위 일시정지 단추 아래에서 시작
            listPanel.style.top = 84;
            listPanel.style.bottom = 40;
            listPanel.style.width = 800;
            listPanel.Add(Ui.Text(L.T("reveal_title"), "h2"));
            listPanel.Add(HeaderRow());
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            list = scroll.contentContainer;
            listPanel.Add(scroll);
            Root.Add(listPanel);
            results = Ui.Col();
            results.AddToClassList("screen");
            results.style.alignItems = Align.Center;
            results.style.justifyContent = Justify.Center;
            results.Show(false);
            Root.Add(results);
        }

        static Label Col(string text, float width, params string[] cls)
        {
            var l = Ui.Text(text, "reveal-col");
            foreach (var c in cls) l.AddToClassList(c);
            if (width > 0) l.style.width = width;
            return l;
        }

        VisualElement HeaderRow()
        {
            // 아래 줄과 같은 모양으로 (점·이름 칸은 글자가 아닌 빈 상자 — 글자는 기본 여백이 있어 칸이 밀린다)
            var r = Ui.Row();
            r.style.paddingLeft = 12;
            r.style.paddingRight = 12;
            var dot = Ui.Div();
            dot.style.width = 22;
            var name = Ui.Div();
            name.style.width = 200;
            r.Add(dot, name, Col(L.T("seller"), 130, "small"), Col(L.T("reveal_paid"), 120, "small", "right-text"),
                Col(L.T("reveal_value"), 120, "small", "right-text"), Col(L.T("reveal_profit"), 120, "small", "right-text"));
            return r;
        }

        string Name(GameView v, int seat)
        {
            if (seat < 0 || seat >= v.SeatCount) return "-";
            return seat == v.Viewer ? L.T("you") : v.Seats[seat].Name;
        }

        Color SeatColor(GameView v, int seat)
        {
            if (seat < 0 || seat >= v.SeatCount) return Color.gray;
            return Stage.SeatColors[v.Seats[seat].ColorIndex % Stage.SeatColors.Length];
        }

        public void OnEvent(GameEvent e, GameView v)
        {
            if (e.Type == EventType.ItemRevealed) AddRow(v, e.Item, true);
            if (e.Type == EventType.RevealStarted)
            {
                list.Clear();
                shown.Clear();
                resultsBuilt = false;
                results.Show(false);
                listPanel.Show(true);
            }
        }

        void AddRow(GameView v, int itemId, bool animate)
        {
            if (shown.Contains(itemId)) return;
            shown.Add(itemId);
            var it = v.Items[itemId];
            foreach (var child in list.Children()) child.RemoveFromClassList("reveal-new");
            var row = Ui.Row("reveal-row", "reveal-new");
            row.Add(Ui.Dot(SeatColor(v, it.Seller), 14));
            var nameCol = Ui.Col();
            nameCol.style.width = 200;
            nameCol.Add(Ui.Text(GameScreen.ItemName(it), "reveal-col"));
            string who;
            if (it.Status == ItemStatus.Sold) who = "→ " + Name(v, it.Winner);
            else if (it.Status == ItemStatus.Returned) who = L.T("reveal_returned");
            else who = L.T("reveal_unsold", Scoring.UnsoldKeep(v.Rules, it.RevealedValue, it.LeftoverFailed).ToString("#,0") + "G");
            nameCol.Add(Ui.Text(who, "tiny"));
            row.Add(nameCol);
            row.Add(Col(Name(v, it.Seller), 130));
            bool sold = it.Status == ItemStatus.Sold;
            row.Add(Col(sold ? L.G(it.SalePrice) : "-", 120, "right-text"));
            var value = Col("", 120, "right-text", "gold");
            row.Add(value);
            var profit = Col("", 120, "right-text");
            row.Add(profit);
            list.Add(row);
            list.parent.parent.schedule.Execute(() => (list.parent.parent as ScrollView)?.ScrollTo(row)).StartingIn(50);
            Ui.FadeIn(row, 0.35f, 14f);
            int val = it.RevealedValue;
            if (animate)
            {
                Ui.CountTo(value, 0, val, 0.9f, x => L.G(x));
                row.schedule.Execute(() => SetProfit(profit, sold, val - it.SalePrice)).StartingIn(950);
            }
            else
            {
                value.text = L.G(val);
                SetProfit(profit, sold, val - it.SalePrice);
            }
        }

        static void SetProfit(Label l, bool sold, int profit)
        {
            if (!sold) { l.text = ""; return; }
            l.text = L.Signed(profit);
            l.EnableInClassList("green", profit >= 0);
            l.EnableInClassList("red", profit < 0);
            Ui.Pop(l, 0.2f);
        }

        public void Refresh(GameView v)
        {
            // 늦게 들어와도 이미 공개된 물건은 채운다
            foreach (int id in v.AuctionOrder) if (v.Items[id].RevealedValue >= 0 && !shown.Contains(id)) AddRow(v, id, false);
            if (v.Phase == Phase.Results && !resultsBuilt)
            {
                resultsBuilt = true;
                BuildResults(v);
            }
        }

        void BuildResults(GameView v)
        {
            listPanel.Show(false);
            results.Clear();
            results.Show(true);
            var panel = Ui.Col("panel");
            panel.style.width = 1100;
            panel.style.alignItems = Align.Stretch;
            var t = Ui.Text(L.T("results_title"), "h1");
            t.style.fontSize = 60;
            panel.Add(t);
            if (v.EndedEarly) panel.Add(Ui.Text(L.T("ended_early"), "small", "center-text"));
            var order = new List<int>();
            for (int i = 0; i < v.SeatCount; i++) order.Add(i);
            order.Sort((a, b) => v.Seats[a].Rank != v.Seats[b].Rank ? v.Seats[a].Rank.CompareTo(v.Seats[b].Rank) : v.Seats[b].Score.CompareTo(v.Seats[a].Score));
            int delay = 0;
            foreach (int s in order)
            {
                var seat = v.Seats[s];
                var row = Ui.Row("rank-row");
                if (seat.Rank == 1) row.AddToClassList("rank-first");
                row.Add(Ui.Text(L.T("results_rank", seat.Rank), "rank-num"));
                row.Add(Ui.Dot(SeatColor(v, s), 18));
                var nc = Ui.Col();
                nc.style.flexGrow = 1;
                nc.Add(Ui.Text(Name(v, s) + (s == v.Viewer ? "  ★" : ""), "rank-name"));
                int spent = seat.Spent;
                int pen = Scoring.CashPenalty(v.Rules, spent, seat.Loan);
                int loan = Scoring.LoanRepay(v.Rules, seat.Loan);
                string br = L.T("results_cash") + " " + L.G(seat.Balance) + "  ·  " + L.T("results_items") + " " + L.G(seat.ItemValueSum)
                    + "  ·  " + L.T("results_penalty") + " −" + L.G(pen) + (loan > 0 ? "  ·  " + L.T("results_loan") + " −" + L.G(loan) : "");
                nc.Add(Ui.Text(br, "rank-break"));
                row.Add(nc);
                var score = Ui.Text("0G", "rank-score");
                row.Add(score);
                panel.Add(row);
                int d = delay;
                row.style.opacity = 0;
                row.schedule.Execute(() =>
                {
                    Ui.FadeIn(row, 0.4f, 20f);
                    Ui.CountTo(score, 0, seat.Score, 1.0f, x => L.G(x));
                    AudioManager.I.Play("sfx_reveal_ting", 0.6f, 0.8f + 0.1f * (v.SeatCount - seat.Rank));
                }).StartingIn(400 + d);
                delay += 450;
            }
            // 최고·최악의 거래, 최고 낙찰가
            if (v.Deals.Count > 0)
            {
                DealRecord best = v.Deals[0], worst = v.Deals[0], top = v.Deals[0];
                foreach (var d in v.Deals)
                {
                    if (d.Profit > best.Profit) best = d;
                    if (d.Profit < worst.Profit) worst = d;
                    if (d.Price > top.Price) top = d;
                }
                var hl = Ui.Row();
                hl.style.justifyContent = Justify.SpaceAround;
                hl.style.marginTop = 14;
                hl.Add(Deal(v, L.T("results_best"), best, "green", false), Deal(v, L.T("results_worst"), worst, "red", false), Deal(v, L.T("results_top"), top, "gold", true));
                panel.Add(hl);
            }
            panel.Add(Ui.Text(L.T("results_memory"), "small", "center-text"));
            var btns = Ui.Row();
            btns.style.justifyContent = Justify.Center;
            btns.style.marginTop = 12;
            btns.Add(Ui.Btn(L.T("results_menu"), () => app.QuitToMenu(), "btn-secondary"));
            var room = Net.NetRoom.I;
            if (room != null && room.IsHost) btns.Add(Ui.Btn(L.T("net_back_lobby"), () => room.BackToLobby(), "btn-big"));
            else if (room != null && room.IsClient) btns.Add(Ui.Text(L.T("net_wait_host"), "small"));
            else btns.Add(Ui.Btn(L.T("results_again"), () => app.Rematch(), "btn-big"));
            panel.Add(btns);
            results.Add(panel);
            Ui.FadeIn(panel, 0.5f, 30f);
        }

        VisualElement Deal(GameView v, string title, DealRecord d, string cls, bool showPrice)
        {
            var c = Ui.Col("panel-soft");
            c.style.alignItems = Align.Center;
            c.style.width = 320;
            c.Add(Ui.Text(title, "small"));
            c.Add(Ui.Text(GameScreen.ItemName(v.Items[d.Item]), "num"));
            c.Add(Ui.Text(Name(v, d.Buyer) + "  " + L.G(d.Price) + " → " + L.G(d.Value), "tiny"));
            var p = Ui.Text(showPrice ? L.G(d.Price) : L.Signed(d.Profit), "h3");
            p.AddToClassList(cls);
            c.Add(p);
            return c;
        }
    }
}
