using System.Collections.Generic;
using BluffAuction.AI;
using BluffAuction.Core;
using BluffAuction.Loc;
using BluffAuction.UI;

namespace BluffAuction.Game
{
    /// <summary>업적과 기록 갱신 (게임 끝에 한 번). Steam 업적 이름과 같은 id 를 쓴다.</summary>
    public static class Achievements
    {
        public static readonly string[] All = { "first_win", "bargain", "bluff", "collector", "debt", "hard", "full", "spender" };

        public static void Evaluate(GameView v, GameSetup setup)
        {
            int me = v.Viewer;
            if (me < 0 || me >= v.SeatCount) return;
            var seat = v.Seats[me];
            var st = SaveData.I.Stats;
            bool won = seat.Rank == 1;
            if (won) st.Wins++;
            if (seat.Score > st.BestScore) st.BestScore = seat.Score;
            var got = new List<string>();
            if (won) got.Add("first_win");
            int wins = 0;
            foreach (var it in v.Items)
            {
                if (it.Status != ItemStatus.Sold || it.RevealedValue < 0) continue;
                if (it.Winner == me)
                {
                    wins++;
                    int profit = it.RevealedValue - it.SalePrice;
                    if (profit > st.BestBargain) st.BestBargain = profit;
                    if (it.SalePrice * 2 <= it.RevealedValue && it.RevealedValue >= 1000) got.Add("bargain");
                }
                if (it.Seller == me && it.SalePrice > it.RevealedValue * 3) got.Add("bluff");
            }
            if (wins >= 4) got.Add("collector");
            if (won && seat.Loan) got.Add("debt");
            if (won && setup != null && setup.Difficulty == Difficulty.Hard) got.Add("hard");
            if (won && v.SeatCount >= 8) got.Add("full");
            if (Scoring.CashPenalty(v.Rules, seat.Spent, seat.Loan) == 0) got.Add("spender");
            foreach (var id in got)
            {
                if (st.Achievements.Contains(id)) continue;
                st.Achievements.Add(id);
                if (UiRoot.I != null) UiRoot.I.Toast("★ " + L.T("ach_unlocked") + "  " + L.T("ach_" + id), "toast-hint", 5f);
                Platform.Steam.Unlock(id);
            }
            Platform.Steam.AddGameResult(won);
            SaveData.Save();
        }
    }
}
