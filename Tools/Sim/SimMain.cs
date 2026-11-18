using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BluffAuction.AI;
using BluffAuction.Core;

// 규칙 엔진 + AI 를 Unity 없이 돌리는 시뮬레이션. 불변식(돈 총합 등) 검사와 성격별 성적·밸런스 통계를 낸다.
// 사용: Tools\Sim\run-sim.ps1 --games 2000 --players 4 --items 3 --seed 1 [--naive 1] [--difficulty 0|1|2] [--log]
public static class SimMain
{
    sealed class Actions : IAiActions
    {
        public AuctionEngine E;
        public int Seat;
        public Result Register(Registration reg) { return E.SubmitRegistration(Seat, reg); }
        public Result Bid(int amount) { return E.Bid(Seat, amount); }
        public Result UseAbility(AbilityKind kind) { return E.UseAbility(Seat, kind); }
        public Result PriceDrop(int newPrice) { return E.DecidePriceDrop(Seat, newPrice); }
        public Result Loan() { return E.TakeLoan(Seat); }
        public Result Say(int line) { return E.Say(Seat, line, null); }
    }

    /// <summary>비교용 단순 봇: 정보 없이 시작가의 일정 배수까지만 부른다 (VRChat판 봇처럼 가치를 보지 않는 버전).
    /// Bluffer 면 늘 허풍: 싼 물건은 가치보다 높은 시작가, 비싼 물건은 아주 낮은 시작가.</summary>
    sealed class NaiveBot
    {
        public int Seat;
        public Rng Rng;
        public bool Bluffer;
        long nextAt;
        int lot = -1;
        double mult;
        public void Think(GameView v, AuctionEngine e)
        {
            if (v.Phase == Phase.Registration && !v.Me.Registered)
            {
                var reg = RegistrationHelper.Random(v.Rules, Rng);
                if (Bluffer)
                {
                    double mean = v.Rules.RegistrationFund / (double)v.Rules.ItemsPerPlayer;
                    for (int i = 0; i < v.Rules.ItemsPerPlayer; i++)
                    {
                        int val = reg.Value[i];
                        double r = val < mean ? Rng.Range(1.1f, 1.8f) : Rng.Range(0.05f, 0.15f);
                        int st = (int)(val * r) / 100 * 100;
                        reg.Start[i] = Math.Max(100, Math.Min(v.Rules.BuyFund, st));
                    }
                }
                e.SubmitRegistration(Seat, reg);
                return;
            }
            if (v.Phase == Phase.PriceDrop && v.IAmSeller) { e.DecidePriceDrop(Seat, v.Current.StartPrice); return; }
            if (!v.BiddingOpen || v.IAmSeller || v.IAmHighest) return;
            if (lot != v.AuctionSeq) { lot = v.AuctionSeq; mult = Rng.Range(1.0f, 2.2f); }
            if (v.NowMs < nextAt) return;
            nextAt = v.NowMs + Rng.Range(800, 2600);
            int cap = (int)(v.Current.StartPrice * mult);
            if (v.MinNextBid <= cap && v.MinNextBid <= v.MyAvailable) e.Bid(Seat, v.MinNextBid);
        }
    }

    sealed class Stat
    {
        public int Games, RankSum, Wins;
        public long ScoreSum;
        public void Add(int rank, int score) { Games++; RankSum += rank; ScoreSum += score; if (rank == 1) Wins++; }
        public override string ToString()
        {
            if (Games == 0) return "-";
            return string.Format("games {0,5}  avgRank {1,5:F2}  win {2,5:P0}  avgScore {3,7:F0}", Games, RankSum / (double)Games, Wins / (double)Games, ScoreSum / (double)Games);
        }
    }

    public static int Main(string[] args)
    {
        int games = Arg(args, "--games", 1000);
        int players = Arg(args, "--players", 4);
        int items = Arg(args, "--items", 3);
        int seed0 = Arg(args, "--seed", 1);
        int naive = Arg(args, "--naive", 0);
        int diff = Arg(args, "--difficulty", 1);
        bool verbose = args.Contains("--log");
        if (args.Contains("--belief"))
        {
            var rr = new GameRules { ItemsPerPlayer = items }.Resolve(players);
            foreach (int st in new[] { 100, 300, 700, 1500, 3000, 6000, 9000 })
            {
                foreach (double temper in new[] { 0.8, 1.0 })
                {
                    var b = new ValueBelief(rr);
                    b.SetPrior(rr.RegistrationFund, rr.ItemsPerPlayer, rr.EffectiveMinValue, 0.3);
                    b.ApplyStartPrice(st, temper);
                    Console.WriteLine(string.Format("start {0,5} temper {1:F1}: mean {2,6:F0} median {3,6:F0} q30 {4,6:F0} sd {5,6:F0}", st, temper, b.Mean(), b.Quantile(0.5), b.Quantile(0.3), b.StdDev()));
                }
            }
            return 0;
        }

        var sw = Stopwatch.StartNew();
        var byProfile = new Dictionary<string, Stat>();
        var naiveStat = new Stat();
        long soldCount = 0, lotCount = 0, unsoldCount = 0, deferredCount = 0, bidCount = 0, abilityCount = 0, fakeCount = 0, loanCount = 0, sayCount = 0;
        double priceValueRatioSum = 0;
        long priceSum = 0, valueSoldSum = 0;
        long gameMsSum = 0;
        long penaltySum = 0;
        int failures = 0;
        var profiles = AiProfiles.Ids();
        var estSum = new double[4];
        var valSum = new double[4];
        var estN = new int[4];
        var priceB = new double[4];
        var priceN = new int[4];
        var soldValB = new double[4];
        var initSum = new double[4];

        int sessionLen = Math.Max(1, Arg(args, "--session", 1));
        int bluffers = Arg(args, "--bluffer", 0);
        var naiveFirst = new Stat();
        var naiveLast = new Stat();
        OpponentMemory shared = null;
        List<string> order = null;
        for (int g = 0; g < games; g++)
        {
            int seed = seed0 * 100003 + g;
            var rng = new Rng(seed);
            var rules = new GameRules { ItemsPerPlayer = items };
            var seats = new List<SeatInfo>();
            int gameInSession = g % sessionLen;
            if (gameInSession == 0)
            {
                // 세션: 같은 사람들이 여러 판을 하며 AI 가 판매자 습관을 배운다
                shared = new OpponentMemory();
                order = new List<string>(profiles);
                ListUtil.Shuffle(order, rng);
            }
            for (int i = 0; i < players; i++)
            {
                bool isNaive = i < naive;
                string pid = isNaive ? "naive" : order[i % order.Count];
                seats.Add(new SeatInfo { Name = pid + i, IsAI = true, ColorIndex = i, CharacterId = i, AiProfile = pid });
            }
            AuctionEngine e;
            try
            {
                e = new AuctionEngine(rules, seats, seed);
            }
            catch (Exception ex)
            {
                Console.WriteLine("setup failed: " + ex.Message);
                return 2;
            }
            var ais = new AiPlayer[players];
            var bots = new NaiveBot[players];
            var acts = new Actions[players];
            var views = new GameView[players];
            for (int i = 0; i < players; i++)
            {
                acts[i] = new Actions { E = e, Seat = i };
                views[i] = new GameView();
                if (i < naive) bots[i] = new NaiveBot { Seat = i, Rng = new Rng(seed + i * 31), Bluffer = i < bluffers };
                else ais[i] = new AiPlayer(i, AiProfiles.ForDifficulty(AiProfiles.Get(seats[i].AiProfile), (Difficulty)diff), seed + i, shared);
            }

            var evs = new List<GameEvent>();
            e.Start();
            int step = 100;
            long limit = 1000L * 60 * 90;
            try
            {
                while (e.Phase != Phase.Results && e.Phase != Phase.Aborted && e.Now < limit)
                {
                    evs.Clear();
                    e.DrainEvents(evs);
                    foreach (var ev in evs)
                    {
                        if (ev.Type == EventType.Sold || ev.Type == EventType.Deferred || ev.Type == EventType.Unsold)
                        {
                            int value = e.Items[ev.Item].Value;
                            int bucket = value < 2000 ? 0 : (value < 5000 ? 1 : (value < 10000 ? 2 : 3));
                            for (int i = 0; i < players; i++)
                            {
                                if (ais[i] == null || i == e.Items[ev.Item].Seller || ais[i].LastLotItem != ev.Item || ais[i].LastUtility <= 0) continue;
                                estSum[bucket] += ais[i].LastUtility;
                                initSum[bucket] += ais[i].InitialEstimate;
                                valSum[bucket] += value;
                                estN[bucket]++;
                            }
                            if (ev.Type == EventType.Sold) { priceB[bucket] += ev.A; soldValB[bucket] += value; priceN[bucket]++; }
                        }
                        if (ev.Type == EventType.BidPlaced) bidCount++;
                        if (ev.Type == EventType.AbilityResult) { if (ev.C == 2) fakeCount++; else abilityCount++; }
                        if (ev.Type == EventType.LoanTaken) loanCount++;
                        if (ev.Type == EventType.SellerSaid) sayCount++;
                        if (ev.Type == EventType.Deferred) deferredCount++;
                        if (verbose && g == 0) Console.WriteLine((ev.TimeMs / 1000.0).ToString("F1").PadLeft(7) + "s  " + ev + Describe(e, ev));
                        for (int i = 0; i < players; i++)
                        {
                            if (ais[i] == null) continue;
                            GameEvent shown;
                            if (!e.TryFilterEvent(ev, i, out shown)) continue;
                            e.BuildView(i, views[i]);
                            ais[i].OnEvent(shown, views[i]);
                        }
                    }
                    int startI = rng.Range(0, players);
                    for (int k = 0; k < players; k++)
                    {
                        int i = (startI + k) % players;
                        e.BuildView(i, views[i]);
                        if (ais[i] != null) ais[i].Think(views[i], acts[i]);
                        else bots[i].Think(views[i], e);
                    }
                    e.Advance(step);
                    CheckInvariants(e);
                }
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine("game " + g + " seed " + seed + " FAILED: " + ex);
                if (failures > 5) return 1;
                continue;
            }
            if (e.Phase != Phase.Results)
            {
                failures++;
                Console.WriteLine("game " + g + " seed " + seed + " did not finish (phase " + e.Phase + ", t=" + e.Now / 1000 + "s)");
                continue;
            }
            if (verbose && g == 0)
            {
                for (int i = 0; i < players; i++)
                {
                    var s = e.Seats[i];
                    Console.WriteLine(string.Format("  seat {0} {1,-10} rank {2} score {3,6} balance {4,6} spent {5,6} items {6,6} penalty {7,5} loan {8}",
                        i, s.Info.Name, s.Rank, s.Score, s.Balance, e.SpentOf(i), s.ItemValueSum, Scoring.CashPenalty(e.R, e.SpentOf(i), s.Loan), s.Loan));
                }
            }
            gameMsSum += e.Now;
            AiPlayer.LearnFromReveal(shared, e.BuildView(-1));
            for (int i = 0; i < players; i++)
            {
                var s = e.Seats[i];
                if (i < naive && gameInSession == 0) naiveFirst.Add(s.Rank, s.Score);
                if (i < naive && gameInSession == sessionLen - 1) naiveLast.Add(s.Rank, s.Score);
                if (i < naive) naiveStat.Add(s.Rank, s.Score);
                else
                {
                    Stat st;
                    if (!byProfile.TryGetValue(seats[i].AiProfile, out st)) byProfile[seats[i].AiProfile] = st = new Stat();
                    st.Add(s.Rank, s.Score);
                }
                penaltySum += Scoring.CashPenalty(e.R, e.SpentOf(i), s.Loan);
            }
            foreach (var it in e.Items)
            {
                if (it.Status == ItemStatus.None) continue;
                lotCount++;
                if (it.Status == ItemStatus.Sold)
                {
                    soldCount++;
                    priceValueRatioSum += it.SalePrice / (double)Math.Max(1, it.Value);
                    priceSum += it.SalePrice;
                    valueSoldSum += it.Value;
                }
                else if (it.Status == ItemStatus.Unsold) unsoldCount++;
            }
        }

        int done = games - failures;
        Console.WriteLine();
        Console.WriteLine(string.Format("{0} games ({1} players, {2} items each, difficulty {3}) in {4:F1}s, failures {5}", games, players, items, diff, sw.Elapsed.TotalSeconds, failures));
        if (done <= 0) return 1;
        Console.WriteLine(string.Format("avg game length {0:F1} min (game time)", gameMsSum / (double)done / 60000.0));
        Console.WriteLine(string.Format("lots {0}  sold {1:P1}  unsold {2:P1}  re-auctioned {3:P1}", lotCount, soldCount / (double)lotCount, unsoldCount / (double)lotCount, deferredCount / (double)lotCount));
        Console.WriteLine(string.Format("avg price/value per lot {0:F2}  total price/total value {1:F2}  bids/lot {2:F1}  abilities/game {3:F1}  fakes/game {4:F1}  loans/game {5:F2}  lines/game {6:F1}",
            priceValueRatioSum / Math.Max(1, soldCount), priceSum / (double)Math.Max(1, valueSoldSum), bidCount / (double)lotCount, abilityCount / (double)done, fakeCount / (double)done, loanCount / (double)done, sayCount / (double)done));
        Console.WriteLine(string.Format("avg unspent penalty per player {0:F0}", penaltySum / (double)(done * players)));
        string[] bn = { "<2k", "2-5k", "5-10k", "10k+" };
        for (int b = 0; b < 4; b++)
            Console.WriteLine(string.Format("  value {0,-6} AI initial est/value {1:F2}  final est/value {2:F2}  sale price/value {3:F2}  (n {4})",
                bn[b], initSum[b] / Math.Max(1, valSum[b]), estSum[b] / Math.Max(1, valSum[b]), priceB[b] / Math.Max(1.0, soldValB[b]), priceN[b]));
        foreach (var kv in byProfile.OrderBy(k => k.Value.RankSum / (double)Math.Max(1, k.Value.Games)))
            Console.WriteLine(kv.Key.PadRight(10) + kv.Value);
        if (naive > 0) Console.WriteLine((bluffers > 0 ? "bluffer" : "naive").PadRight(10) + naiveStat);
        if (naive > 0 && sessionLen > 1)
        {
            Console.WriteLine("  first game of session  " + naiveFirst);
            Console.WriteLine("  last game of session   " + naiveLast);
        }
        return failures > 0 ? 1 : 0;
    }

    static string Describe(AuctionEngine e, GameEvent ev)
    {
        if (ev.Item < 0) return "";
        var it = e.Items[ev.Item];
        return "   [value " + it.Value + " start " + it.InitialStart + "]";
    }

    static void CheckInvariants(AuctionEngine e)
    {
        long cash = 0;
        for (int i = 0; i < e.SeatCount; i++)
        {
            var s = e.Seats[i];
            if (s.Reserved < 0) throw new Exception("negative reserve seat " + i);
            if (s.Reserved > s.Balance) throw new Exception("reserve > balance seat " + i);
            if (s.Balance < 0) throw new Exception("negative balance seat " + i);
            if (s.Reserved > 0 && e.HighestBidder != i) throw new Exception("reserve without being highest seat " + i);
            cash += s.Balance - (s.Loan ? e.R.LoanAmount : 0);
        }
        if (cash != (long)e.SeatCount * e.R.BuyFund) throw new Exception("cash mismatch " + cash);
        if (e.HighestBidder >= 0 && e.Seats[e.HighestBidder].Reserved != e.HighestBid) throw new Exception("reserve != highest bid");
    }

    static int Arg(string[] args, string name, int def)
    {
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return int.Parse(args[i + 1]);
        return def;
    }
}
