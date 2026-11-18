using System;
using System.Collections.Generic;

namespace BluffAuction.Core
{
    [Serializable]
    public sealed class SeatView
    {
        public int Index;
        public string Name = "";
        /// <summary>여러 판에 걸쳐 같은 사람을 알아보는 열쇠 (AI 의 판매자 습관 기억).</summary>
        public string Key = "";
        public bool IsAI;
        public int ColorIndex;
        public int CharacterId;
        public bool Active;
        public bool Registered;
        public int Balance;
        /// <summary>묶인 돈. 블라인드 모드에서 남의 값은 -1 (최고 입찰자가 드러나지 않게).</summary>
        public int Reserved;
        public int Spent;
        public int WonCount;
        public bool Loan;
        public bool SkipVote;
        public bool EndVote;
        /// <summary>능력 남은 횟수. 남의 값은 -1 (거짓 통보를 구별하지 못하게).</summary>
        public int CompareLeft = -1;
        public int AppraiseLeft = -1;
        public int FakeLeft = -1;
        public int ItemValueSum;
        public int Score;
        public int Rank;
    }

    [Serializable]
    public sealed class ItemView
    {
        public int Id;
        public int Seller;
        public int Slot;
        public ItemStatus Status;
        public bool Presented;
        public int Appearance = -1;
        public int InitialStart;
        public int StartPrice;
        public int Phrase = -1;
        public string CustomName = "";
        public string CustomPhrase = "";
        public bool Dropped;
        public bool LeftoverFailed;
        public int Winner = -1;
        public int SalePrice;
        public int BidCount;
        public int BidderMask;
        /// <summary>공개된 가치 (공개 전 -1).</summary>
        public int RevealedValue = -1;
        /// <summary>내 물건이면 등록 가치 (남의 물건 -1).</summary>
        public int MyValue = -1;

        public bool IsMine(int seat) { return seat >= 0 && Seller == seat; }
        public int KnownValue { get { return RevealedValue >= 0 ? RevealedValue : MyValue; } }
    }

    /// <summary>한 좌석이 볼 수 있는 게임 상태. 화면·AI·네트워크 클라이언트가 모두 이것만 쓴다.</summary>
    [Serializable]
    public sealed class GameView
    {
        public int Viewer = -1;
        public GameRules Rules;
        public Phase Phase;
        public int RemainingMs;
        public int DurationMs;
        public int Round;
        public int Rounds;
        public int Turn;
        public bool LeftoverPass;
        public int CurrentItem = -1;
        public int HighestBid;
        /// <summary>-1 없음, -2 블라인드 모드라 가려짐.</summary>
        public int HighestBidder = -1;
        public int MinNextBid;
        public int AuctionSeq;
        public int ExtensionCount;
        public int SkipAgree;
        public int EndAgree;
        public int ActiveCount;
        public bool EndedEarly;
        public int RevealCount;
        public int Version;
        public long NowMs;
        public SeatView[] Seats = new SeatView[0];
        public ItemView[] Items = new ItemView[0];
        public List<int> AuctionOrder = new List<int>();
        public List<AbilityRecord> MyAbilities = new List<AbilityRecord>();
        public List<DealRecord> Deals = new List<DealRecord>();
        /// <summary>판매 순서 [바퀴 * 좌석수 + 차례] → 좌석</summary>
        public int[] SellerOrder = new int[0];

        public int SeatCount { get { return Seats.Length; } }
        public SeatView Me { get { return Viewer >= 0 && Viewer < Seats.Length ? Seats[Viewer] : null; } }
        public ItemView Current { get { return CurrentItem >= 0 ? Items[CurrentItem] : null; } }
        public bool BiddingOpen { get { return Phase == Phase.Bidding || Phase == Phase.Reauction; } }
        public bool IAmSeller { get { return Current != null && Current.Seller == Viewer; } }
        public bool IAmHighest { get { return Viewer >= 0 && HighestBidder == Viewer; } }

        public int MyAvailable
        {
            get { var m = Me; return m == null ? 0 : m.Balance - m.Reserved; }
        }

        /// <summary>지금 끝나면 물어야 할 안 쓴 살 돈 벌금.</summary>
        public int MyPenaltyNow
        {
            get { var m = Me; return m == null ? 0 : Scoring.CashPenalty(Rules, m.Spent, m.Loan); }
        }

        public int MyUnspentBudget
        {
            get { var m = Me; return m == null ? 0 : Scoring.UnspentBudget(Rules, m.Spent, m.Loan); }
        }

        public int SellerForTurn(int r, int t)
        {
            int n = Seats.Length;
            int k = r * n + t;
            return k >= 0 && k < SellerOrder.Length ? SellerOrder[k] : -1;
        }

        /// <summary>지금 이후 남은 경매(유찰품 경매 제외, 내 물건 제외) 수 — AI 의 예산 판단용.</summary>
        public int RemainingLotsNotMine()
        {
            int n = 0;
            foreach (var it in Items)
            {
                if (it.Seller == Viewer) continue;
                if (it.Status == ItemStatus.Pending || it.Status == ItemStatus.Deferred) n++;
            }
            return n;
        }

        internal void Fill(AuctionEngine e, int viewer)
        {
            Viewer = viewer;
            Rules = e.R;
            Phase = e.Phase;
            RemainingMs = e.RemainingMs;
            DurationMs = e.PhaseDurationMs;
            Round = e.Round;
            Rounds = e.Rounds;
            Turn = e.Turn;
            LeftoverPass = e.LeftoverPass;
            CurrentItem = e.CurrentItem;
            HighestBid = e.HighestBid;
            HighestBidder = e.HighestBidder;
            if (e.R.BlindMode && HighestBidder >= 0 && HighestBidder != viewer) HighestBidder = -2;
            MinNextBid = e.MinNextBid;
            AuctionSeq = e.AuctionSeq;
            ExtensionCount = e.ExtensionCount;
            SkipAgree = e.SkipAgreeCount();
            EndAgree = e.EndAgreeCount();
            ActiveCount = e.ActiveCount();
            EndedEarly = e.EndedEarly;
            RevealCount = e.RevealCount;
            Version = e.Version;
            NowMs = e.Now;
            bool results = e.Phase == Phase.Results;

            if (Seats.Length != e.SeatCount)
            {
                Seats = new SeatView[e.SeatCount];
                for (int i = 0; i < Seats.Length; i++) Seats[i] = new SeatView();
            }
            for (int i = 0; i < e.SeatCount; i++)
            {
                var s = e.Seats[i];
                var o = Seats[i];
                bool me = i == viewer;
                o.Index = i;
                o.Name = s.Info.Name;
                o.Key = s.Info.IsAI && !string.IsNullOrEmpty(s.Info.AiProfile) ? "ai:" + s.Info.AiProfile : "human:" + s.Info.Name;
                o.IsAI = s.Info.IsAI;
                o.ColorIndex = s.Info.ColorIndex;
                o.CharacterId = s.Info.CharacterId;
                o.Active = s.Active;
                o.Registered = s.Registered;
                o.Balance = s.Balance;
                o.Reserved = (e.R.BlindMode && !me && !results) ? -1 : s.Reserved;
                o.Spent = e.SpentOf(i);
                o.WonCount = e.WonCount(i);
                o.Loan = s.Loan;
                o.SkipVote = s.SkipVote;
                o.EndVote = s.EndVote;
                o.CompareLeft = me ? s.CompareLeft : -1;
                o.AppraiseLeft = me ? s.AppraiseLeft : -1;
                o.FakeLeft = me ? s.FakeLeft : -1;
                o.ItemValueSum = results ? s.ItemValueSum : 0;
                o.Score = results ? s.Score : 0;
                o.Rank = results ? s.Rank : 0;
            }

            if (Items.Length != e.Items.Length)
            {
                Items = new ItemView[e.Items.Length];
                for (int i = 0; i < Items.Length; i++) Items[i] = new ItemView();
            }
            for (int i = 0; i < e.Items.Length; i++)
            {
                var it = e.Items[i];
                var o = Items[i];
                bool mine = it.Seller == viewer;
                bool pub = it.Presented;
                o.Id = it.Id;
                o.Seller = it.Seller;
                o.Slot = it.Slot;
                o.Status = it.Status;
                o.Presented = it.Presented;
                o.Appearance = (pub || mine) ? it.Appearance : -1;
                o.InitialStart = (pub || mine) ? it.InitialStart : 0;
                o.StartPrice = (pub || mine) ? it.StartPrice : 0;
                o.Phrase = (pub || mine) ? it.Phrase : -1;
                o.CustomName = (pub || mine) ? it.CustomName : "";
                o.CustomPhrase = (pub || mine) ? it.CustomPhrase : "";
                o.Dropped = it.Dropped;
                o.LeftoverFailed = it.LeftoverFailed;
                o.Winner = it.Winner;
                o.SalePrice = it.SalePrice;
                o.BidCount = it.BidCount;
                o.BidderMask = e.R.BlindMode && !results ? 0 : it.BidderMask;
                o.RevealedValue = it.Revealed ? it.Value : -1;
                o.MyValue = mine && it.Known ? it.Value : -1;
            }

            AuctionOrder.Clear();
            AuctionOrder.AddRange(e.AuctionOrder);
            MyAbilities.Clear();
            if (viewer >= 0 && viewer < e.SeatCount) MyAbilities.AddRange(e.AbilityLog(viewer));
            Deals.Clear();
            if (results) Deals.AddRange(e.Deals);
            int n = e.SeatCount;
            if (SellerOrder.Length != e.Rounds * n) SellerOrder = new int[e.Rounds * n];
            for (int r = 0; r < e.Rounds; r++)
                for (int t = 0; t < n; t++)
                    SellerOrder[r * n + t] = e.SellerForTurn(r, t);
        }
    }

    /// <summary>등록 도우미: 무작위 등록 (시간 초과·자동 채우기) 과 합계 맞추기.</summary>
    public static class RegistrationHelper
    {
        public static Registration Random(GameRules r, Rng rng)
        {
            var reg = new Registration();
            int n = r.ItemsPerPlayer;
            var apps = new List<int>();
            for (int i = 0; i < r.AppearanceCount; i++) apps.Add(i);
            ListUtil.Shuffle(apps, rng);
            for (int i = 0; i < n; i++)
            {
                reg.Appearance[i] = r.AllowDuplicateAppearance ? rng.Range(0, r.AppearanceCount) : apps[i % apps.Count];
                reg.Phrase[i] = rng.Range(0, r.PhraseCount);
            }
            int unit = r.MoneyUnit;
            int min = r.EffectiveMinValue;
            for (int i = 0; i < n; i++) reg.Value[i] = min;
            int units = (r.RegistrationFund - min * n) / unit;
            while (units > 0)
            {
                int chunk = rng.Range(1, units / 3 + 2);
                if (chunk > units) chunk = units;
                reg.Value[rng.Range(0, n)] += chunk * unit;
                units -= chunk;
            }
            for (int i = 0; i < n; i++)
            {
                int st = reg.Value[i] * rng.Range(20, 71) / 100 / unit * unit;
                reg.Start[i] = Clamp(st, r.MinStartPrice, r.MaxStartPrice);
            }
            return reg;
        }

        /// <summary>가치 합계가 등록 자금과 같아지도록 마지막에 고친 칸이 아닌 칸에서 모자람/넘침을 맞춘다.</summary>
        public static void Balance(GameRules r, Registration reg, int keepIndex)
        {
            int n = r.ItemsPerPlayer;
            int min = r.EffectiveMinValue;
            int sum = 0;
            for (int i = 0; i < n; i++) sum += reg.Value[i];
            int diff = r.RegistrationFund - sum;
            for (int pass = 0; pass < 2 && diff != 0; pass++)
            {
                for (int k = n - 1; k >= 0 && diff != 0; k--)
                {
                    if (k == keepIndex && pass == 0) continue;
                    int nv = reg.Value[k] + diff;
                    if (nv < min) nv = min;
                    diff -= nv - reg.Value[k];
                    reg.Value[k] = nv;
                }
            }
        }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}
