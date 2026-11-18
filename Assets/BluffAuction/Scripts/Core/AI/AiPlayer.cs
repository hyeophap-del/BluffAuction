using System;
using System.Collections.Generic;
using BluffAuction.Core;

namespace BluffAuction.AI
{
    /// <summary>AI 가 엔진에 보내는 명령 (자기 좌석 몫만).</summary>
    public interface IAiActions
    {
        Result Register(Registration reg);
        Result Bid(int amount);
        Result UseAbility(AbilityKind kind);
        Result PriceDrop(int newPrice);
        Result Loan();
        Result Say(int line);
    }

    /// <summary>
    /// AI 상대 한 명. 엔진의 비공개 값을 보지 않고, 자기 좌석의 <see cref="GameView"/> 와 걸러진 사건만으로 판단한다.
    /// 판단: 판매자의 등록 자금 나누기(사전 분포) → 시작가·문구 → 내 능력 결과 → 남의 입찰·능력 사용(부드러운 신호)
    /// 로 가치를 추정하고, 안 쓴 돈 벌금(1/2)을 감안해 입찰 한도를 정한다.
    /// </summary>
    public sealed class AiPlayer
    {
        public readonly int Seat;
        public readonly AiProfile P;
        readonly Rng rng;
        readonly double skill;

        long registerAt = -1;
        int favorite = -1;

        // 지금 물건(경매 회차)에 대한 기억
        int lotSeq = -1;
        int lotItem = -1;
        ValueBelief baseBelief;
        ValueBelief work;
        bool beliefReady;
        double noise = 1;
        long nextActAt;
        long nextSayAt;
        int abilitiesThisLot;
        bool fakePlanned;
        long fakeAt;
        bool sniperThisLot;
        long priceDropAt = -1;
        bool saidPresent;
        double lastMaxBid;
        readonly List<Notice> notices = new List<Notice>();
        readonly Dictionary<int, int> maxBidBy = new Dictionary<int, int>();
        readonly Dictionary<int, long> lastBidAt = new Dictionary<int, long>();
        int anonymousMaxBid;

        // 여러 물건에 걸친 기억: 능력으로 범위를 좁힌 물건의 추정 가치 (같은 판매자의 남은 자금 계산)
        readonly Dictionary<int, double> hardEstimate = new Dictionary<int, double>();
        readonly Dictionary<int, ValueBelief> itemBeliefs = new Dictionary<int, ValueBelief>();
        readonly List<int> salePrices = new List<int>();

        struct Notice
        {
            public int Seat;
            public int RefPrice;
            public long Time;
        }

        readonly OpponentMemory memory;

        /// <param name="shared">세션 동안 쌓인 판매자 습관 (공개 정보로 배운 것). 이 AI 는 사본에 자기 능력 결과를 더해 쓴다.</param>
        public AiPlayer(int seat, AiProfile profile, int seed, OpponentMemory shared = null)
        {
            Seat = seat;
            P = profile;
            rng = new Rng(seed ^ (seat * 7919 + 17));
            skill = Clamp01(profile.Skill);
            memory = shared != null ? shared.Clone() : new OpponentMemory();
        }

        /// <summary>최종 공개된 가치로 판매자 습관을 배운다 (모두에게 공개된 정보라 공유 기억에 넣는다).</summary>
        public static void LearnFromReveal(OpponentMemory shared, GameView v)
        {
            if (shared == null) return;
            foreach (var it in v.Items)
            {
                if (it.RevealedValue <= 0 || it.InitialStart <= 0) continue;
                shared.Get(v.Seats[it.Seller].Key).Observe(it.InitialStart, it.RevealedValue, 1.0);
            }
        }

        SellerModel ModelOf(GameView v, int seller)
        {
            return seller >= 0 && seller < v.Seats.Length ? memory.Get(v.Seats[seller].Key) : null;
        }

        public double LastMaxBid { get { return lastMaxBid; } }
        /// <summary>시뮬레이션 진단용: 마지막으로 판단한 물건과 그때의 위험 조정 가치.</summary>
        public int LastLotItem { get { return lotItem; } }
        public double LastUtility { get; private set; }
        public double InitialEstimate { get; private set; }

        static double Clamp01(double x) { return x < 0 ? 0 : (x > 1 ? 1 : x); }

        long React() { return (long)(rng.Range(P.ReactMin, P.ReactMax) * 1000f); }

        int RoundUnit(double x, int unit) { return (int)Math.Round(x / unit) * unit; }

        // ═════════════════ 사건 ═════════════════

        public void OnEvent(GameEvent e, GameView v)
        {
            switch (e.Type)
            {
                case EventType.GameStarted:
                    favorite = P.FavoriteAppearance == -2 ? rng.Range(0, Content.AppearanceCount) : P.FavoriteAppearance;
                    break;
                case EventType.LotPresented:
                    ResetLot(v, e.Item);
                    if (e.Seat == Seat && !saidPresent && rng.Chance(P.Chattiness * 0.8))
                    {
                        saidPresent = true;
                        nextSayAt = v.NowMs + 600 + rng.Range(0, 1200);
                    }
                    break;
                case EventType.BidPlaced:
                    if (e.Item != lotItem) break;
                    if (e.Seat >= 0)
                    {
                        int prev;
                        if (!maxBidBy.TryGetValue(e.Seat, out prev) || e.A > prev) maxBidBy[e.Seat] = e.A;
                        lastBidAt[e.Seat] = e.TimeMs;
                    }
                    else if (e.Seat == -2 && e.A > anonymousMaxBid) anonymousMaxBid = e.A;
                    if (e.Seat != Seat)
                    {
                        // 새 입찰을 보고 다시 생각하기까지 사람처럼 잠깐 뜸을 들인다
                        long t = v.NowMs + React();
                        if (nextActAt < t) nextActAt = t;
                        if (e.B == Seat && rng.Chance(P.Chattiness * 0.12)) QueueSay(v, Content.LineLamentFirst, Content.LineLamentCount, 400);
                    }
                    else if (e.B >= 0 && rng.Chance(P.Chattiness * 0.18)) QueueSay(v, Content.LineTauntFirst, Content.LineTauntCount, 300);
                    break;
                case EventType.AbilityUsed:
                    if (e.Seat != Seat && e.Item == lotItem)
                    {
                        var it = v.Items[e.Item];
                        notices.Add(new Notice { Seat = e.Seat, RefPrice = v.HighestBidder != -1 ? v.HighestBid : it.StartPrice, Time = e.TimeMs });
                    }
                    break;
                case EventType.AbilityResult:
                    if (e.Audience == Seat) ApplyAbilityResult(v, e);
                    break;
                case EventType.Sold:
                    salePrices.Add(e.A);
                    if (e.Seat == Seat && rng.Chance(P.Chattiness * 0.5)) QueueSay(v, Content.LineWinFirst, Content.LineWinCount, 500);
                    else if (e.B == Seat && rng.Chance(P.Chattiness * 0.3)) QueueSay(v, Content.LineWinFirst + 1, 1, 700);
                    break;
                case EventType.NoBids:
                    if (e.Seat == Seat) priceDropAt = v.NowMs + 1200 + rng.Range(0, 2500);
                    break;
                case EventType.BiddingOpened:
                    // 소개가 끝나자마자 모두 한꺼번에 부르지 않게 각자 뜸을 들인다
                    nextActAt = v.NowMs + React() + rng.Range(0, 1500);
                    break;
            }
        }

        void QueueSay(GameView v, int first, int count, int delayMs)
        {
            pendingLine = first + rng.Range(0, count);
            nextSayAt = v.NowMs + delayMs + rng.Range(0, 900);
        }

        int pendingLine = -1;

        void ResetLot(GameView v, int item)
        {
            lotSeq = v.AuctionSeq;
            lotItem = item;
            beliefReady = false;
            abilitiesThisLot = 0;
            notices.Clear();
            maxBidBy.Clear();
            lastBidAt.Clear();
            anonymousMaxBid = 0;
            saidPresent = false;
            priceDropAt = -1;
            noise = 1 + rng.Gaussian() * 0.12 * (1.2 - skill);
            if (noise < 0.6) noise = 0.6;
            nextActAt = v.NowMs + React();
            sniperThisLot = rng.Chance(P.Snipe);
            var me = v.Me;
            int lots = Math.Max(1, v.RemainingLotsNotMine() + 1);
            fakePlanned = me != null && me.FakeLeft > 0 && v.Items[item].Seller != Seat
                && rng.Chance(Math.Min(0.9, P.FakeRate * me.FakeLeft / (double)lots * 1.6));
            fakeAt = 0;
        }

        // ═════════════════ 가치 추정 ═════════════════

        void EnsureBelief(GameView v)
        {
            if (beliefReady || lotItem < 0) return;
            var it = v.Items[lotItem];
            var r = v.Rules;
            ValueBelief stored;
            if (itemBeliefs.TryGetValue(lotItem, out stored))
            {
                // 유찰품 경매로 다시 나온 물건: 처음 경매 때의 추정에 "아무도 안 샀다"를 더한다
                baseBelief = stored;
                if (it.InitialStart > 0) baseBelief.Tilt(it.InitialStart, 0.75, 1.0);
            }
            else
            {
                baseBelief = new ValueBelief(r);
                int fund = r.RegistrationFund;
                int k = r.ItemsPerPlayer;
                if (skill >= 0.5)
                {
                    // 같은 판매자의 다른 물건 가운데 능력으로 범위를 좁힌 것은 남은 자금에서 뺀다
                    for (int s = 0; s < r.ItemsPerPlayer; s++)
                    {
                        int other = AuctionEngine.ItemId(it.Seller, s);
                        if (other == lotItem) continue;
                        double est;
                        if (hardEstimate.TryGetValue(other, out est))
                        {
                            fund -= (int)est;
                            k--;
                        }
                    }
                    int minFund = r.EffectiveMinValue * Math.Max(1, k);
                    if (fund < minFund) fund = minFund;
                }
                baseBelief.SetPrior(fund, Math.Max(1, k), r.EffectiveMinValue, 0.5);
                // 솜씨가 낮으면 판매자마다의 습관을 구별하지 않는다
                var model = skill >= 0.45 ? ModelOf(v, it.Seller) : null;
                baseBelief.ApplyStartPrice(it.InitialStart > 0 ? it.InitialStart : it.StartPrice, 0.45 + 0.55 * skill, model);
                int sentiment = it.Phrase >= 0 && it.Phrase < Content.PhraseSentiment.Length ? Content.PhraseSentiment[it.Phrase] : 0;
                baseBelief.ApplyPhrase(sentiment, P.Gullible, r.RegistrationFund / (double)r.ItemsPerPlayer);
                itemBeliefs[lotItem] = baseBelief;
            }
            work = new ValueBelief(r);
            beliefReady = true;
            InitialEstimate = baseBelief.Quantile(0.5);
        }

        void ApplyAbilityResult(GameView v, GameEvent e)
        {
            if (e.Item < 0) return;
            if (e.Item == lotItem) EnsureBelief(v);
            ValueBelief b;
            if (!itemBeliefs.TryGetValue(e.Item, out b)) return;
            int unit = v.Rules.MoneyUnit;
            var kind = (AbilityKind)e.C;
            var it = v.Items[e.Item];
            var model = ModelOf(v, it.Seller);
            if (kind == AbilityKind.Compare)
            {
                if (e.B > 0) b.Restrict(e.A + unit, int.MaxValue);
                else if (e.B == 0) b.Restrict(e.A, e.A);
                else b.Restrict(0, e.A - unit);
                // 시작가보다도 싸다 = 허풍을 쳤다
                if (e.B < 0 && it.InitialStart > 0 && e.A <= it.InitialStart && model != null) model.ObserveBluff(0.8);
            }
            else if (kind == AbilityKind.Appraise)
            {
                b.Restrict(e.A, e.B);
                hardEstimate[e.Item] = b.Mean();
                if (model != null && it.InitialStart > 0) model.Observe(it.InitialStart, (e.A + e.B) / 2, 0.5);
            }
            // 새 정보는 바로 반영해 생각한다
            nextActAt = v.NowMs + (long)(rng.Range(0.4f, 1.2f) * 1000);
        }

        /// <summary>남의 입찰·능력 사용까지 반영한 위험 조정 가치.</summary>
        double Utility(GameView v)
        {
            work.CopyFrom(baseBelief);
            var it = v.Items[lotItem];
            // 남이 부른 값은 "그 사람은 이보다 가치 있다고 본다"는 약한 신호. 서로의 입찰을 믿고 끝없이 올리지 않도록
            // 가장 높은 남의 입찰 하나만, 그 값의 80% 를 기준으로, 솜씨가 좋을수록 덜 믿는다.
            int otherMax = anonymousMaxBid;
            foreach (var kv in maxBidBy)
            {
                if (kv.Key == Seat || kv.Key == it.Seller) continue;
                if (kv.Value > otherMax) otherMax = kv.Value;
            }
            double h = P.Herding * 0.25 * (1 - 0.5 * skill);
            if (otherMax > 0) work.Tilt((int)(otherMax * 0.8), 1, 1 - h);
            double a = P.Herding * 0.35 * skill;
            foreach (var n in notices)
            {
                long lb;
                bool bidAfter = lastBidAt.TryGetValue(n.Seat, out lb) && lb > n.Time;
                if (bidAfter) work.Tilt(n.RefPrice, 1, 1 - a);
                else if (v.NowMs - n.Time > 5000 && v.HighestBidder != n.Seat) work.Tilt(n.RefPrice, 1 - a * 0.6, 1);
            }
            // 드문 "숨긴 보물" 가능성에 평균이 끌려가지 않도록 중앙값을 기준으로 한다
            double median = work.Quantile(0.5);
            double low = work.Quantile(0.3);
            double u = (1 - P.Caution) * median + P.Caution * low;
            if (favorite >= 0 && it.Appearance == favorite) u *= 1 + P.FavoriteBonus;
            u *= noise;
            LastUtility = u;
            return u;
        }

        /// <summary>돈을 쓸 압박: 안 쓴 돈은 절반을 잃으므로, 남은 기회에 비해 돈이 많을수록 가치보다 비싸게 사도 된다 (최대 2배).</summary>
        double SpendFactor(GameView v)
        {
            var r = v.Rules;
            int unspent = v.MyUnspentBudget;
            int futureLots = v.RemainingLotsNotMine();
            int competitors = Math.Max(1, v.ActiveCount - 2);
            double expectedWins = futureLots / (double)(competitors + 1);
            double typicalPrice;
            if (salePrices.Count > 0)
            {
                long sum = 0;
                foreach (var p in salePrices) sum += p;
                typicalPrice = sum / (double)salePrices.Count;
            }
            else typicalPrice = 0.45 * r.RegistrationFund / r.ItemsPerPlayer;
            double expectedFutureSpend = expectedWins * typicalPrice;
            double pressure = unspent <= 0 ? 0 : Clamp01((unspent - expectedFutureSpend) / unspent);
            double pf = Scoring.PenaltyFraction(r);
            double maxK = pf >= 0.99 ? 3 : 1 / (1 - pf);
            return 0.85 + (maxK - 0.85) * Math.Pow(pressure, 1.3) * (0.45 + 0.55 * skill);
        }

        // ═════════════════ 판단 ═════════════════

        public void Think(GameView v, IAiActions act)
        {
            if (v.Me == null || !v.Me.Active) return;
            long now = v.NowMs;

            if (pendingLine >= 0 && now >= nextSayAt)
            {
                act.Say(pendingLine);
                pendingLine = -1;
            }

            switch (v.Phase)
            {
                case Phase.Registration:
                    if (v.Me.Registered) return;
                    if (registerAt < 0) registerAt = now + 1200 + rng.Range(0, 3500);
                    if (now >= registerAt)
                    {
                        if (act.Register(BuildRegistration(v.Rules)) != Result.Ok) act.Register(RegistrationHelper.Random(v.Rules, rng));
                        // 온라인에서는 확정이 돌아오기까지 시간이 걸리므로 한참 뒤에만 다시 보낸다
                        registerAt = now + 2500;
                    }
                    return;
                case Phase.PriceDrop:
                    if (v.Current != null && v.Current.Seller == Seat)
                    {
                        if (priceDropAt < 0) priceDropAt = now + 1500;
                        if (now >= priceDropAt) { DecidePriceDrop(v, act); priceDropAt = long.MaxValue; }
                    }
                    return;
                case Phase.Presenting:
                case Phase.Bidding:
                case Phase.Reauction:
                    break;
                default:
                    return;
            }

            var it = v.Current;
            if (it == null) return;
            if (v.AuctionSeq != lotSeq || it.Id != lotItem) ResetLot(v, it.Id);

            if (it.Seller == Seat)
            {
                SellerChatter(v, act);
                return;
            }
            if (!v.BiddingOpen) return;
            EnsureBelief(v);
            if (now < nextActAt) return;
            nextActAt = now + 350 + rng.Range(0, 500);

            var me = v.Me;
            int unit = v.Rules.MoneyUnit;
            double u = Utility(v);
            double k = SpendFactor(v);
            double maxBid = u * k * P.Greed;
            if (rng.Chance(P.Impulse * 0.25)) maxBid *= 1 + rng.Range(0.05f, 0.3f);
            lastMaxBid = maxBid;
            int need = v.MinNextBid;
            int avail = v.MyAvailable;

            // 거짓 통보: 관심 없는 척 / 아는 척
            if (fakePlanned && me.FakeLeft > 0)
            {
                if (fakeAt == 0) fakeAt = now + (long)(v.RemainingMs * rng.Range(0.15f, 0.7f));
                if (now >= fakeAt)
                {
                    fakePlanned = false;
                    if (act.UseAbility(AbilityKind.Fake) == Result.Ok)
                    {
                        if (rng.Chance(P.Chattiness * 0.35)) QueueSay(v, Content.LineBluffFirst, Content.LineBluffCount, 800);
                        nextActAt = now + React();
                        return;
                    }
                }
            }

            if (v.IAmHighest) return;
            if (TryAbility(v, act, u, k, need)) return;
            if (need > avail) { ConsiderLoan(v, act, u, need); return; }
            if (need > maxBid) return;

            // 저격수는 마감 직전까지 기다린다
            int window = (v.Rules.ExtendSeconds + 2) * 1000;
            if (sniperThisLot && v.RemainingMs > window && v.RemainingMs > 2500)
            {
                nextActAt = now + Math.Max(300, v.RemainingMs - window + rng.Range(0, 1500));
                return;
            }

            int amount = need;
            if (rng.Chance(P.JumpBid))
            {
                // 크게 올려 불러 기를 꺾는다 (한도의 남은 폭과 지금 값에 비해 적당히)
                double room = Math.Min(maxBid, avail) - need;
                double jump = Math.Min(room * rng.Range(0.05f, 0.25f), need * rng.Range(0.1f, 0.4f));
                if (jump > unit)
                {
                    int step = jump > 2000 ? 500 : unit;
                    amount = need + Math.Max(unit, RoundUnit(jump, step));
                }
            }
            amount = amount / unit * unit;
            if (amount > avail) amount = avail / unit * unit;
            if (amount < need) return;
            if (act.Bid(amount) == Result.Ok) nextActAt = now + React();
        }

        bool TryAbility(GameView v, IAiActions act, double u, double k, int need)
        {
            var me = v.Me;
            int compare = me.CompareLeft, appraise = me.AppraiseLeft;
            if (compare + appraise <= 0) return false;
            int maxUses = skill > 0.85 ? 2 : 1;
            if (abilitiesThisLot >= maxUses) return false;
            int lots = Math.Max(1, v.RemainingLotsNotMine() + 1);
            double mean = work.Mean();
            double sd = work.StdDev();
            double cv = mean > 1 ? sd / mean : 1;
            if (cv < 0.18) return false;
            double optimistic = work.Quantile(0.8) * k;
            if (need > optimistic || need > v.MyAvailable) return false;
            if (need < 0.25 * mean && abilitiesThisLot == 0 && v.HighestBidder == -1) return false;
            double budget = Math.Min(1.0, (compare + appraise) / (double)lots * 1.6);
            double chance = P.AbilityRate * budget * (cv > 0.3 ? 1.0 : 0.45);
            // 한 번 생각할 때마다 굴리지 않고, 경매당 몇 번만 굴린다
            if (!rng.Chance(chance * 0.35)) return false;
            AbilityKind kind;
            bool nearPrice = Math.Abs(need - mean) < 0.4 * mean;
            if (compare > 0 && (nearPrice || appraise <= 0)) kind = AbilityKind.Compare;
            else if (appraise > 0) kind = AbilityKind.Appraise;
            else kind = AbilityKind.Compare;
            if (act.UseAbility(kind) != Result.Ok) return false;
            abilitiesThisLot++;
            if (rng.Chance(P.Chattiness * 0.25)) QueueSay(v, Content.LineBluffFirst, Content.LineBluffCount, 1200);
            return true;
        }

        void ConsiderLoan(GameView v, IAiActions act, double u, int need)
        {
            var me = v.Me;
            var r = v.Rules;
            if (me.Loan || r.LoanAmount <= 0) return;
            if (v.RemainingLotsNotMine() < 2) return;
            // 빌린 돈은 이자가 붙고, 안 쓰면 그 절반도 잃는다: 확실한 이득이 보일 때만
            double margin = u - need;
            if (margin < r.LoanInterest * 1.2) return;
            if (!rng.Chance(P.LoanChance * 0.5)) return;
            act.Loan();
        }

        void SellerChatter(GameView v, IAiActions act)
        {
            long now = v.NowMs;
            if (pendingLine >= 0) return;
            if (now < nextSayAt) return;
            if (!rng.Chance(P.Chattiness)) { nextSayAt = now + 5000; return; }
            bool lull = v.BiddingOpen && v.HighestBidder == -1 && v.RemainingMs < v.DurationMs * 0.6;
            bool closing = v.BiddingOpen && v.RemainingMs < 6000;
            if (v.Phase == Phase.Presenting || lull || closing || rng.Chance(0.3))
            {
                act.Say(Content.LineHypeFirst + rng.Range(0, Content.LineHypeCount));
                nextSayAt = now + (long)(rng.Range(5f, 13f) / Math.Max(0.2, P.Chattiness) * 1000);
            }
            else nextSayAt = now + 2500;
        }

        void DecidePriceDrop(GameView v, IAiActions act)
        {
            var it = v.Current;
            var r = v.Rules;
            int value = it.MyValue;
            int unit = r.MoneyUnit;
            int s = it.StartPrice;
            if (value < 0) { act.PriceDrop(s); return; }
            bool stubborn = rng.Chance(P.BluffChance * 0.5);
            int target;
            if (s > value * 0.8) target = RoundUnit(value * rng.Range(0.3f, 0.55f), unit);
            else target = RoundUnit(s * rng.Range(0.55f, 0.85f), unit);
            if (target < r.MinStartPrice) target = r.MinStartPrice;
            if (target > s) target = s;
            // 아무도 안 산 값보다 가치의 절반이 확실히 크면 포기하고 절반을 갖는다
            if (value / 2 >= target * 1.25 && !stubborn)
            {
                act.PriceDrop(0);
                return;
            }
            act.PriceDrop(stubborn ? s : target);
        }

        // ═════════════════ 등록 ═════════════════

        public Registration BuildRegistration(GameRules r)
        {
            var reg = new Registration();
            int n = r.ItemsPerPlayer;
            int unit = r.MoneyUnit;
            int min = r.EffectiveMinValue;

            var apps = new List<int>();
            for (int i = 0; i < r.AppearanceCount; i++) apps.Add(i);
            ListUtil.Shuffle(apps, rng);
            for (int i = 0; i < n; i++) reg.Appearance[i] = apps[i % apps.Count];

            // 가치 나누기: 몰아넣는 성격일수록 무게 차이를 키운다
            var w = new double[n];
            double sw = 0;
            for (int i = 0; i < n; i++)
            {
                double e = -Math.Log(1 - rng.NextDouble() * 0.999);
                w[i] = Math.Pow(e, 1 + 2.5 * P.Concentration);
                sw += w[i];
            }
            int rest = r.RegistrationFund - min * n;
            int given = 0;
            for (int i = 0; i < n; i++)
            {
                int add = (int)(rest * w[i] / sw) / unit * unit;
                reg.Value[i] = min + add;
                given += add;
            }
            reg.Value[rng.Range(0, n)] += rest - given;

            double mean = r.RegistrationFund / (double)n;
            for (int i = 0; i < n; i++)
            {
                int v = reg.Value[i];
                double ratio = rng.Range(P.StartRatioMin, P.StartRatioMax);
                if (rng.Chance(P.BluffChance))
                {
                    // 허풍: 싼 물건은 비싸 보이게, 비싼 물건은 싸 보이게
                    ratio = v < mean ? rng.Range(1.0f, 1.8f) : rng.Range(0.04f, 0.15f);
                }
                int st = RoundUnit(v * ratio, unit);
                if (st > r.BuyFund) st = r.BuyFund / unit * unit;
                if (st < r.MinStartPrice) st = r.MinStartPrice;
                if (st > r.MaxStartPrice) st = r.MaxStartPrice;
                reg.Start[i] = st;

                int want = v >= mean * 1.15 ? 1 : (v < mean * 0.6 ? -1 : 0);
                if (!rng.Chance(P.PhraseHonesty)) want = rng.Chance(0.6) ? 1 : -want;
                reg.Phrase[i] = PickPhrase(want);
            }
            return reg;
        }

        int PickPhrase(int sentiment)
        {
            var list = new List<int>();
            for (int i = 0; i < Content.PhraseSentiment.Length; i++) if (Content.PhraseSentiment[i] == sentiment) list.Add(i);
            if (list.Count == 0) return rng.Range(0, Content.PhraseCount);
            return list[rng.Range(0, list.Count)];
        }
    }
}
