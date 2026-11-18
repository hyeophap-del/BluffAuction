using System;
using System.Collections.Generic;

namespace BluffAuction.Core
{
    /// <summary>
    /// 허풍 경매의 유일한 판정자. 모든 비공개 정보(등록 가치)를 갖고, 명령을 검증해 상태를 바꾸고 사건을 내보낸다.
    /// 시간은 <see cref="Advance"/> 로만 흐르므로 일시정지·배속·시뮬레이션이 쉽다.
    /// 화면과 AI 는 <see cref="BuildView"/> 와 <see cref="TryFilterEvent"/> 를 거친 정보만 본다 (온라인에서도 같은 경로).
    /// </summary>
    public sealed class AuctionEngine
    {
        public const int ItemsPerSeatSlots = GameRules.MaxItemsPerPlayer;

        public readonly GameRules R;
        public readonly int SeatCount;
        public readonly SeatState[] Seats;
        public readonly ItemState[] Items;

        public Phase Phase { get; private set; }
        public long PhaseEndMs { get; private set; }
        public int PhaseDurationMs { get; private set; }
        public int Round { get; private set; }
        public int Turn { get; private set; }
        public bool LeftoverPass { get; private set; }
        public int CurrentItem { get; private set; }
        public int HighestBid { get; private set; }
        public int HighestBidder { get; private set; }
        public int ExtensionCount { get; private set; }
        public int AuctionSeq { get; private set; }
        public int RevealCount { get; private set; }
        public bool EndedEarly { get; private set; }
        public int AbortReason { get; private set; }
        public long Now { get; private set; }
        /// <summary>상태가 바뀔 때마다 1씩 오른다 (화면 갱신·네트워크 전송 판단).</summary>
        public int Version { get; private set; }
        public readonly List<int> AuctionOrder = new List<int>();
        public readonly List<DealRecord> Deals = new List<DealRecord>();

        readonly int[,] roundOrder;
        readonly Rng rng;
        readonly List<GameEvent> pending = new List<GameEvent>();
        readonly List<AbilityRecord>[] abilityLog;

        public AuctionEngine(GameRules rules, IList<SeatInfo> seats, int seed)
        {
            if (seats == null || seats.Count < 1 || seats.Count > GameRules.MaxSeats) throw new ArgumentException("seats");
            SeatCount = seats.Count;
            R = rules.Resolve(SeatCount);
            string err = R.Validate(SeatCount);
            if (err.Length > 0) throw new ArgumentException("rules: " + err);
            rng = new Rng(seed);
            Seats = new SeatState[SeatCount];
            abilityLog = new List<AbilityRecord>[SeatCount];
            for (int i = 0; i < SeatCount; i++)
            {
                Seats[i] = new SeatState { Index = i, Info = seats[i] };
                abilityLog[i] = new List<AbilityRecord>();
            }
            Items = new ItemState[SeatCount * ItemsPerSeatSlots];
            for (int i = 0; i < Items.Length; i++)
                Items[i] = new ItemState { Id = i, Seller = i / ItemsPerSeatSlots, Slot = i % ItemsPerSeatSlots, Status = ItemStatus.None };
            roundOrder = new int[GameRules.MaxItemsPerPlayer, SeatCount];
            CurrentItem = -1;
            HighestBidder = -1;
            Phase = Phase.Setup;
        }

        public int Rounds { get { return R.ItemsPerPlayer; } }
        public static int ItemId(int seat, int slot) { return seat * ItemsPerSeatSlots + slot; }
        public static int SellerOf(int item) { return item < 0 ? -1 : item / ItemsPerSeatSlots; }
        public int SellerForTurn(int r, int t) { return (r >= 0 && r < Rounds && t >= 0 && t < SeatCount) ? roundOrder[r, t] : -1; }
        public IReadOnlyList<AbilityRecord> AbilityLog(int seat) { return abilityLog[seat]; }

        public bool IsParticipant(int seat) { return seat >= 0 && seat < SeatCount && Seats[seat].Active; }

        public int RemainingMs
        {
            get
            {
                if (!HasTimer(Phase)) return 0;
                long r = PhaseEndMs - Now;
                return r < 0 ? 0 : (int)Math.Min(r, int.MaxValue);
            }
        }

        public int MinNextBid
        {
            get
            {
                if (CurrentItem < 0) return 0;
                return HighestBidder < 0 ? Items[CurrentItem].StartPrice : HighestBid + R.MinBidIncrement;
            }
        }

        public int Available(int seat) { return Seats[seat].Balance - Seats[seat].Reserved; }

        public int SpentOf(int seat)
        {
            int spent = 0;
            foreach (var it in Items) if (it.Status == ItemStatus.Sold && it.Winner == seat) spent += it.SalePrice;
            return spent;
        }

        public int WonCount(int seat)
        {
            int n = 0;
            foreach (var it in Items) if (it.Status == ItemStatus.Sold && it.Winner == seat) n++;
            return n;
        }

        public int ActiveCount()
        {
            int n = 0;
            foreach (var s in Seats) if (s.Active) n++;
            return n;
        }

        public int SkipAgreeCount()
        {
            int n = 0;
            foreach (var s in Seats) if (s.Active && (s.SkipVote || s.Info.IsAI)) n++;
            return n;
        }

        public int EndAgreeCount()
        {
            int n = 0;
            foreach (var s in Seats) if (s.Active && (s.EndVote || s.Info.IsAI)) n++;
            return n;
        }

        public bool HasTimer(Phase p)
        {
            return p == Phase.Presenting || p == Phase.Bidding || p == Phase.PriceDrop || p == Phase.Reauction
                || p == Phase.AuctionResult || p == Phase.Reveal || (p == Phase.Registration && R.RegistrationSeconds > 0);
        }

        public bool IsAuctionRunning
        {
            get { return Phase == Phase.Presenting || Phase == Phase.Bidding || Phase == Phase.PriceDrop || Phase == Phase.Reauction || Phase == Phase.AuctionResult; }
        }

        public bool BiddingOpen { get { return Phase == Phase.Bidding || Phase == Phase.Reauction; } }

        // ═════════════════ 사건 ═════════════════

        void Emit(EventType type, int seat = -1, int item = -1, int a = 0, int b = 0, int c = 0, string text = null, int audience = -1)
        {
            pending.Add(new GameEvent { Type = type, Seat = seat, Item = item, A = a, B = b, C = c, Text = text, Audience = audience, TimeMs = Now });
        }

        /// <summary>쌓인 사건을 꺼낸다 (원본, 모든 좌석의 사건 포함). 보여 줄 때는 TryFilterEvent 로 거른다.</summary>
        public void DrainEvents(List<GameEvent> into)
        {
            into.AddRange(pending);
            pending.Clear();
        }

        /// <summary>좌석 viewer 에게 보여도 되는 사건이면 (블라인드 모드면 입찰자를 가려서) 돌려준다. viewer -1 은 관전자.</summary>
        public bool TryFilterEvent(GameEvent e, int viewer, out GameEvent shown)
        {
            shown = e;
            if (e.Audience >= 0 && e.Audience != viewer) return false;
            if (R.BlindMode && e.Type == EventType.BidPlaced)
            {
                if (e.Seat != viewer) shown.Seat = -2;
                if (e.B != viewer && e.B >= 0) shown.B = -2;
            }
            return true;
        }

        void Touch() { Version++; }

        // ═════════════════ 진행 ═════════════════

        public void Start()
        {
            if (Phase != Phase.Setup) return;
            for (int i = 0; i < SeatCount; i++)
            {
                var s = Seats[i];
                s.Active = true;
                s.Registered = false;
                s.Balance = R.BuyFund;
                s.Reserved = 0;
                s.CompareLeft = R.CompareUses;
                s.AppraiseLeft = R.AppraiseUses;
                s.FakeLeft = R.FakeUses;
            }
            BuildSellerOrder();
            Round = 0;
            Turn = 0;
            SetPhase(Phase.Registration, R.RegistrationSeconds > 0 ? R.RegistrationSeconds * 1000 : 0);
            Emit(EventType.GameStarted, a: SeatCount);
            Touch();
        }

        /// <summary>시간을 ms 만큼 흘린다. 마감된 단계를 차례로 처리한다.</summary>
        public void Advance(int ms)
        {
            if (ms > 0) Now += ms;
            for (int guard = 0; guard < 64; guard++)
            {
                if (!HasTimer(Phase) || Now < PhaseEndMs) return;
                OnTimeout();
            }
        }

        void SetPhase(Phase p, int durationMs)
        {
            Phase = p;
            PhaseDurationMs = durationMs;
            PhaseEndMs = Now + durationMs;
        }

        void OnTimeout()
        {
            switch (Phase)
            {
                case Phase.Registration:
                    for (int i = 0; i < SeatCount; i++)
                    {
                        if (!Seats[i].Active || Seats[i].Registered) continue;
                        var reg = RegistrationHelper.Random(R, rng);
                        if (ValidateRegistration(reg) == Result.Ok) StoreRegistration(i, reg);
                    }
                    TryBeginAuctions();
                    if (Phase == Phase.Registration) SetPhase(Phase.Registration, 1000);
                    break;
                case Phase.Presenting:
                    OpenBidding();
                    break;
                case Phase.Bidding:
                case Phase.Reauction:
                    CloseAuction();
                    break;
                case Phase.PriceDrop:
                    if (CurrentItem >= 0) Defer(CurrentItem, Items[CurrentItem].StartPrice);
                    else GoToTurn(Round, Turn + 1);
                    break;
                case Phase.AuctionResult:
                    GoToTurn(Round, Turn + 1);
                    break;
                case Phase.Reveal:
                    RevealNext();
                    break;
                default:
                    PhaseEndMs = long.MaxValue;
                    break;
            }
        }

        /// <summary>바퀴마다 판매 순서를 섞는다: 앞 바퀴 마지막 판매자는 바로 다음 첫 판매자가 아니고,
        /// 첫 판매자였던 사람은 모두 한 번씩 첫 판매자가 될 때까지 다시 1번이 되지 않는다.</summary>
        void BuildSellerOrder()
        {
            int n = SeatCount;
            var tmp = new List<int>(n);
            var wasFirst = new bool[n];
            int prevLast = -1;
            for (int r = 0; r < GameRules.MaxItemsPerPlayer; r++)
            {
                bool anyFresh = false;
                for (int k = 0; k < n; k++) if (!wasFirst[k]) anyFresh = true;
                if (!anyFresh) for (int k = 0; k < n; k++) wasFirst[k] = false;
                for (int attempt = 0; attempt < 80; attempt++)
                {
                    tmp.Clear();
                    for (int k = 0; k < n; k++) tmp.Add(k);
                    ListUtil.Shuffle(tmp, rng);
                    if (n < 2) break;
                    bool ok = tmp[0] != prevLast;
                    if (ok && attempt < 60 && wasFirst[tmp[0]]) ok = false;
                    if (ok) break;
                }
                for (int k = 0; k < n; k++) roundOrder[r, k] = tmp[k];
                wasFirst[tmp[0]] = true;
                prevLast = tmp[n - 1];
            }
        }

        void TryBeginAuctions()
        {
            if (Phase != Phase.Registration) return;
            int active = 0;
            for (int i = 0; i < SeatCount; i++)
            {
                if (!Seats[i].Active) continue;
                if (!Seats[i].Registered) return;
                active++;
            }
            if (active == 0) { Abort(1); return; }
            GoToTurn(0, 0);
        }

        /// <summary>(r, t) 부터 다음 물건을 찾아 소개한다. 한 바퀴는 일반 경매를 모두 한 뒤 그 바퀴의 유찰품 경매를 같은 순서로 한다.</summary>
        void GoToTurn(int r, int t)
        {
            CurrentItem = -1;
            while (r < Rounds)
            {
                while (t < SeatCount)
                {
                    int seat = SellerForTurn(r, t);
                    if (seat >= 0)
                    {
                        var it = Items[ItemId(seat, r)];
                        var want = LeftoverPass ? ItemStatus.Deferred : ItemStatus.Pending;
                        if (it.Status == want)
                        {
                            if (Seats[seat].Active && it.Known)
                            {
                                Round = r;
                                Turn = t;
                                Present(it.Id, LeftoverPass);
                                return;
                            }
                            it.Status = ItemStatus.Excluded;
                        }
                    }
                    t++;
                }
                if (!LeftoverPass) LeftoverPass = true;
                else
                {
                    LeftoverPass = false;
                    r++;
                }
                t = 0;
            }
            LeftoverPass = false;
            BeginReveal();
        }

        void Present(int item, bool leftover)
        {
            var it = Items[item];
            CurrentItem = item;
            it.Status = ItemStatus.Active;
            if (!leftover)
            {
                it.Presented = true;
                it.StartPrice = it.InitialStart;
                it.Dropped = false;
                if (!AuctionOrder.Contains(item)) AuctionOrder.Add(item);
            }
            HighestBid = 0;
            HighestBidder = -1;
            ExtensionCount = 0;
            ClearVotes();
            AuctionSeq++;
            SetPhase(Phase.Presenting, (int)(R.PresentSeconds * 1000f));
            Emit(EventType.LotPresented, it.Seller, item, it.StartPrice, leftover ? 1 : 0);
            Touch();
        }

        void OpenBidding()
        {
            if (CurrentItem < 0) { GoToTurn(Round, Turn + 1); return; }
            int ms = (LeftoverPass ? R.ReauctionSeconds : R.BidSeconds) * 1000;
            SetPhase(LeftoverPass ? Phase.Reauction : Phase.Bidding, ms);
            Emit(EventType.BiddingOpened, Items[CurrentItem].Seller, CurrentItem, ms);
            Touch();
        }

        void CloseAuction()
        {
            int item = CurrentItem;
            if (item < 0 || Items[item].Status != ItemStatus.Active) { GoToTurn(Round, Turn + 1); return; }
            if (!BiddingOpen) return;
            var it = Items[item];
            if (HighestBidder >= 0) { SettleSale(item); return; }
            if (Phase == Phase.Bidding)
            {
                if (it.StartPrice - R.MoneyUnit >= R.MinStartPrice)
                {
                    ClearVotes();
                    AuctionSeq++;
                    SetPhase(Phase.PriceDrop, R.PriceDropSeconds * 1000);
                    Emit(EventType.NoBids, it.Seller, item);
                    Touch();
                    return;
                }
                Defer(item, it.StartPrice);
                return;
            }
            it.LeftoverFailed = true;
            MarkUnsold(item);
        }

        void Defer(int item, int newPrice)
        {
            var it = Items[item];
            if (it.Status != ItemStatus.Active) return;
            ReleaseHighest();
            ClearVotes();
            bool dropped = newPrice < it.StartPrice;
            if (dropped) it.Dropped = true;
            it.StartPrice = newPrice;
            it.Status = ItemStatus.Deferred;
            AuctionSeq++;
            Emit(EventType.Deferred, it.Seller, item, newPrice, dropped ? 1 : 0);
            SetPhase(Phase.AuctionResult, (int)(R.ResultSeconds * 1000f));
            Touch();
        }

        void SettleSale(int item)
        {
            var it = Items[item];
            if (it.Settled || it.Status != ItemStatus.Active) return;
            int buyer = HighestBidder;
            int price = HighestBid;
            int seller = it.Seller;
            if (buyer < 0 || price <= 0 || buyer == seller) { MarkUnsold(item); return; }
            Seats[buyer].Reserved -= price;
            if (Seats[buyer].Reserved < 0) Seats[buyer].Reserved = 0;
            Seats[buyer].Balance -= price;
            Seats[seller].Balance += price;
            it.Winner = buyer;
            it.SalePrice = price;
            it.Status = ItemStatus.Sold;
            it.Settled = true;
            HighestBid = 0;
            HighestBidder = -1;
            ClearVotes();
            AuctionSeq++;
            Emit(EventType.Sold, buyer, item, price, seller);
            SetPhase(Phase.AuctionResult, (int)(R.ResultSeconds * 1000f));
            Touch();
        }

        void MarkUnsold(int item)
        {
            var it = Items[item];
            if (it.Status != ItemStatus.Active) return;
            ReleaseHighest();
            ClearVotes();
            it.Status = ItemStatus.Unsold;
            AuctionSeq++;
            Emit(EventType.Unsold, it.Seller, item, 0, it.LeftoverFailed ? 1 : 0);
            SetPhase(Phase.AuctionResult, (int)(R.ResultSeconds * 1000f));
            Touch();
        }

        void ReleaseHighest()
        {
            if (HighestBidder >= 0)
            {
                Seats[HighestBidder].Reserved -= HighestBid;
                if (Seats[HighestBidder].Reserved < 0) Seats[HighestBidder].Reserved = 0;
            }
            HighestBid = 0;
            HighestBidder = -1;
        }

        void ClearVotes()
        {
            foreach (var s in Seats) s.SkipVote = false;
        }

        void BeginReveal()
        {
            CurrentItem = -1;
            HighestBid = 0;
            HighestBidder = -1;
            RevealCount = 0;
            AuctionSeq++;
            SetPhase(Phase.Reveal, (int)(R.RevealIntervalSeconds * 1000f));
            Emit(EventType.RevealStarted, a: AuctionOrder.Count);
            Touch();
        }

        void RevealNext()
        {
            if (RevealCount < AuctionOrder.Count)
            {
                var it = Items[AuctionOrder[RevealCount]];
                it.Revealed = true;
                RevealCount++;
                SetPhase(Phase.Reveal, (int)(R.RevealIntervalSeconds * 1000f));
                Emit(EventType.ItemRevealed, it.Seller, it.Id, it.Value, it.Status == ItemStatus.Sold ? it.SalePrice : 0);
                Touch();
                return;
            }
            ComputeResults();
            SetPhase(Phase.Results, 0);
            Emit(EventType.Results);
            Touch();
        }

        void ComputeResults()
        {
            foreach (var s in Seats) { s.ItemValueSum = 0; s.Score = 0; s.Rank = 0; }
            Deals.Clear();
            foreach (var it in Items)
            {
                if (!it.Revealed) continue;
                int v = it.Value;
                if (it.Status == ItemStatus.Sold)
                {
                    if (it.Winner >= 0) Seats[it.Winner].ItemValueSum += v;
                    Deals.Add(new DealRecord { Item = it.Id, Buyer = it.Winner, Price = it.SalePrice, Value = v });
                }
                else if (it.Status == ItemStatus.Returned) Seats[it.Seller].ItemValueSum += v;
                else if (it.Status == ItemStatus.Unsold) Seats[it.Seller].ItemValueSum += Scoring.UnsoldNet(R, v, it.LeftoverFailed, it.StartPrice);
            }
            long cash = 0;
            for (int i = 0; i < SeatCount; i++)
            {
                var s = Seats[i];
                s.Score = s.Balance - Scoring.CashPenalty(R, SpentOf(i), s.Loan) + s.ItemValueSum - Scoring.LoanRepay(R, s.Loan);
                cash += s.Balance - (s.Loan ? R.LoanAmount : 0);
            }
            for (int i = 0; i < SeatCount; i++)
            {
                int higher = 0;
                for (int o = 0; o < SeatCount; o++) if (o != i && Seats[o].Score > Seats[i].Score) higher++;
                Seats[i].Rank = higher + 1;
            }
            if (cash != (long)SeatCount * R.BuyFund) throw new InvalidOperationException("cash mismatch " + cash);
        }

        void Abort(int reason)
        {
            ReleaseHighest();
            foreach (var s in Seats) s.Reserved = 0;
            CurrentItem = -1;
            AbortReason = reason;
            SetPhase(Phase.Aborted, 0);
            Emit(EventType.Aborted, a: reason);
            Touch();
        }

        void EndGameEarly()
        {
            ReleaseHighest();
            ClearVotes();
            foreach (var it in Items)
            {
                if (it.Status != ItemStatus.Active && it.Status != ItemStatus.Pending && it.Status != ItemStatus.Deferred) continue;
                it.Status = ItemStatus.Returned;
                if (!AuctionOrder.Contains(it.Id)) AuctionOrder.Add(it.Id);
            }
            foreach (var s in Seats) s.EndVote = false;
            EndedEarly = true;
            Emit(EventType.EndedEarly);
            BeginReveal();
        }

        // ═════════════════ 등록 ═════════════════

        public Result ValidateRegistration(Registration reg) { return Validate(R, reg); }

        /// <summary>등록 검증 (엔진이 없는 온라인 참가자 화면도 같은 규칙으로 미리 안내한다).</summary>
        public static Result Validate(GameRules R, Registration reg)
        {
            if (R == null || reg == null || reg.Appearance == null || reg.Value == null || reg.Start == null || reg.Phrase == null) return Result.Invalid;
            int n = R.ItemsPerPlayer;
            if (reg.Appearance.Length < n || reg.Value.Length < n || reg.Start.Length < n || reg.Phrase.Length < n) return Result.Invalid;
            int sum = 0;
            int minValue = R.EffectiveMinValue;
            for (int i = 0; i < n; i++)
            {
                if (reg.Phrase[i] < 0 || reg.Phrase[i] >= R.PhraseCount) return Result.Invalid;
                if (reg.Appearance[i] < 0 || reg.Appearance[i] >= R.AppearanceCount) return Result.Invalid;
                if (!R.AllowDuplicateAppearance)
                    for (int j = 0; j < i; j++) if (reg.Appearance[j] == reg.Appearance[i]) return Result.DuplicateAppearance;
                if (reg.Value[i] < 0 || reg.Value[i] > R.RegistrationFund) return Result.Invalid;
                if (reg.Value[i] < minValue) return Result.BelowMin;
                if (reg.Value[i] % R.MoneyUnit != 0) return Result.Unit;
                sum += reg.Value[i];
                if (reg.Start[i] < R.MinStartPrice || reg.Start[i] > R.MaxStartPrice) return Result.StartRange;
                if (reg.Start[i] % R.MoneyUnit != 0) return Result.Unit;
                string cn = reg.CustomName != null && i < reg.CustomName.Length ? reg.CustomName[i] : "";
                string cp = reg.CustomPhrase != null && i < reg.CustomPhrase.Length ? reg.CustomPhrase[i] : "";
                if ((cn ?? "").Length > R.CustomNameMaxLength || (cp ?? "").Length > R.CustomPhraseMaxLength) return Result.TooLong;
            }
            if (sum != R.RegistrationFund) return Result.SumMismatch;
            return Result.Ok;
        }

        public Result SubmitRegistration(int seat, Registration reg)
        {
            if (!IsParticipant(seat)) return Result.NotParticipant;
            if (Phase != Phase.Registration) return Result.WrongPhase;
            if (Seats[seat].Registered) return Result.Already;
            var code = ValidateRegistration(reg);
            if (code != Result.Ok) return code;
            StoreRegistration(seat, reg);
            TryBeginAuctions();
            return Result.Ok;
        }

        void StoreRegistration(int seat, Registration reg)
        {
            for (int i = 0; i < Rounds; i++)
            {
                var it = Items[ItemId(seat, i)];
                it.Appearance = reg.Appearance[i];
                it.Value = reg.Value[i];
                it.InitialStart = reg.Start[i];
                it.StartPrice = reg.Start[i];
                it.Phrase = reg.Phrase[i];
                it.CustomName = Sanitize(reg.CustomName != null && i < reg.CustomName.Length ? reg.CustomName[i] : "");
                it.CustomPhrase = Sanitize(reg.CustomPhrase != null && i < reg.CustomPhrase.Length ? reg.CustomPhrase[i] : "");
                it.Known = true;
                it.Status = ItemStatus.Pending;
            }
            Seats[seat].Registered = true;
            Emit(EventType.Registered, seat);
            Touch();
        }

        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("<", "").Replace(">", "").Replace("\n", " ").Replace("\r", "").Trim();
        }

        // ═════════════════ 경매 명령 ═════════════════

        Result CheckBidding(int seat, bool allowSeller)
        {
            if (!IsParticipant(seat)) return Result.NotParticipant;
            if (!BiddingOpen) return Result.WrongPhase;
            if (CurrentItem < 0 || Items[CurrentItem].Status != ItemStatus.Active) return Result.WrongPhase;
            if (Now >= PhaseEndMs)
            {
                CloseAuction();
                return Result.TimeOver;
            }
            if (!allowSeller && Items[CurrentItem].Seller == seat) return Result.IsSeller;
            return Result.Ok;
        }

        public Result Bid(int seat, int amount)
        {
            var c = CheckBidding(seat, false);
            if (c != Result.Ok) return c;
            if (HighestBidder == seat) return Result.AlreadyHighest;
            if (amount <= 0 || amount > R.MaxBid) return Result.Invalid;
            if (amount % R.MoneyUnit != 0) return Result.Unit;
            if (amount < MinNextBid) return Result.TooLow;
            if (amount > Available(seat)) return Result.Insufficient;

            var it = Items[CurrentItem];
            int prev = HighestBidder;
            if (prev >= 0)
            {
                Seats[prev].Reserved -= HighestBid;
                if (Seats[prev].Reserved < 0) Seats[prev].Reserved = 0;
            }
            Seats[seat].Reserved += amount;
            HighestBid = amount;
            HighestBidder = seat;
            it.BidCount++;
            it.BidderMask |= 1 << seat;

            bool extended = false;
            long remaining = PhaseEndMs - Now;
            int extendMs = R.ExtendSeconds * 1000;
            if (extendMs > 0 && remaining < extendMs && (R.MaxExtensions <= 0 || ExtensionCount < R.MaxExtensions))
            {
                PhaseEndMs = Now + extendMs;
                ExtensionCount++;
                extended = true;
            }
            Emit(EventType.BidPlaced, seat, CurrentItem, amount, prev, extended ? 1 : 0);
            Touch();
            return Result.Ok;
        }

        public Result UseAbility(int seat, AbilityKind kind)
        {
            var c = CheckBidding(seat, false);
            if (c != Result.Ok) return c;
            var s = Seats[seat];
            int left = kind == AbilityKind.Compare ? s.CompareLeft : (kind == AbilityKind.Appraise ? s.AppraiseLeft : s.FakeLeft);
            if (left <= 0) return Result.NoUses;
            var it = Items[CurrentItem];
            int a, b;
            if (kind == AbilityKind.Fake)
            {
                s.FakeLeft--;
                a = 0;
                b = s.FakeLeft;
            }
            else if (kind == AbilityKind.Compare)
            {
                a = HighestBidder >= 0 ? HighestBid : it.StartPrice;
                b = it.Value > a ? 1 : (it.Value == a ? 0 : -1);
                s.CompareLeft--;
            }
            else
            {
                int unit = R.MoneyUnit;
                int width = rng.Range(R.AppraisalWidthMin / unit, R.AppraisalWidthMax / unit + 1) * unit;
                a = it.Value - rng.Range(0, width / unit + 1) * unit;
                if (a < 0) a = 0;
                b = a + width;
                s.AppraiseLeft--;
            }
            abilityLog[seat].Add(new AbilityRecord { Item = CurrentItem, Kind = kind, A = a, B = b, TimeMs = Now });
            Emit(EventType.AbilityUsed, seat, CurrentItem);
            Emit(EventType.AbilityResult, seat, CurrentItem, a, b, (int)kind, audience: seat);
            Touch();
            return Result.Ok;
        }

        /// <summary>무입찰 뒤 판매자의 결정: newPrice 0 은 포기(가치의 1/2), 아니면 지금 시작가 이하로 유찰품 경매 시작가.</summary>
        public Result DecidePriceDrop(int seat, int newPrice)
        {
            if (!IsParticipant(seat)) return Result.NotParticipant;
            if (Phase != Phase.PriceDrop) return Result.WrongPhase;
            int item = CurrentItem;
            if (item < 0 || Items[item].Status != ItemStatus.Active) return Result.WrongPhase;
            var it = Items[item];
            if (it.Seller != seat) return Result.NotSeller;
            if (Now >= PhaseEndMs)
            {
                Defer(item, it.StartPrice);
                return Result.TimeOver;
            }
            if (newPrice == 0)
            {
                MarkUnsold(item);
                return Result.Ok;
            }
            if (newPrice > it.StartPrice || newPrice < R.MinStartPrice || newPrice % R.MoneyUnit != 0) return Result.Invalid;
            Defer(item, newPrice);
            return Result.Ok;
        }

        public Result VoteSkip(int seat, bool on)
        {
            if (!IsParticipant(seat)) return Result.NotParticipant;
            if (!BiddingOpen && Phase != Phase.Presenting) return Result.WrongPhase;
            Seats[seat].SkipVote = on;
            int agree = SkipAgreeCount();
            int total = ActiveCount();
            Emit(EventType.SkipChanged, seat, CurrentItem, on ? 1 : 0, agree, total);
            if (on && agree >= total)
            {
                int skipMs = Phase == Phase.Presenting ? 0 : R.SkipToSeconds * 1000;
                if (PhaseEndMs - Now > skipMs)
                {
                    PhaseEndMs = Now + skipMs;
                    Emit(EventType.TimeSkipped, a: skipMs);
                }
                if (Phase == Phase.Presenting) foreach (var s in Seats) s.SkipVote = false;
            }
            Touch();
            return Result.Ok;
        }

        public bool EndVoteOpen { get { return IsAuctionRunning; } }

        public Result VoteEnd(int seat, bool on)
        {
            if (!IsParticipant(seat)) return Result.NotParticipant;
            if (!EndVoteOpen) return Result.WrongPhase;
            Seats[seat].EndVote = on;
            int agree = EndAgreeCount();
            int total = ActiveCount();
            Emit(EventType.EndVoteChanged, seat, -1, on ? 1 : 0, agree, total);
            Touch();
            if (on && agree >= total) EndGameEarly();
            return Result.Ok;
        }

        public Result TakeLoan(int seat)
        {
            if (!IsParticipant(seat)) return Result.NotParticipant;
            if (R.LoanAmount <= 0) return Result.NotAllowed;
            if (!IsAuctionRunning) return Result.WrongPhase;
            var s = Seats[seat];
            if (s.Loan) return Result.Already;
            s.Loan = true;
            s.Balance += R.LoanAmount;
            Emit(EventType.LoanTaken, seat, -1, R.LoanAmount);
            Touch();
            return Result.Ok;
        }

        /// <summary>한마디 (빠른 한마디 번호 또는 직접 쓴 글). 경매가 진행되는 동안 누구나, 좌석마다 짧은 간격.</summary>
        public Result Say(int seat, int lineId, string text)
        {
            if (!IsParticipant(seat)) return Result.NotParticipant;
            if (!IsAuctionRunning && Phase != Phase.Registration && Phase != Phase.Reveal && Phase != Phase.Results) return Result.WrongPhase;
            var s = Seats[seat];
            if (Now - s.LastSayMs < R.SayCooldownMs) return Result.Cooldown;
            text = Sanitize(text);
            if (lineId < 0 && text.Length == 0) return Result.Invalid;
            if (text.Length > R.SayMaxLength) return Result.TooLong;
            s.LastSayMs = Now;
            bool isSeller = CurrentItem >= 0 && Items[CurrentItem].Seller == seat;
            Emit(EventType.SellerSaid, seat, CurrentItem, lineId, isSeller ? 1 : 0, 0, lineId < 0 ? text : null);
            return Result.Ok;
        }

        /// <summary>온라인에서 사람이 나가면 AI 가 그 자리를 이어받는다 (엔진은 모든 비공개 값을 갖고 있어 판이 깨지지 않는다).</summary>
        public void ReplaceWithAI(int seat, string profile)
        {
            if (seat < 0 || seat >= SeatCount) return;
            Seats[seat].Info.IsAI = true;
            Seats[seat].Info.AiProfile = profile ?? "";
            Emit(EventType.SeatReplacedByAI, seat);
            Touch();
        }

        // ═════════════════ 보기 ═════════════════

        /// <summary>좌석 viewer 가 알아도 되는 정보만 담은 사본. viewer -1 은 관전자.</summary>
        public GameView BuildView(int viewer, GameView into = null)
        {
            var v = into ?? new GameView();
            v.Fill(this, viewer);
            return v;
        }
    }
}
