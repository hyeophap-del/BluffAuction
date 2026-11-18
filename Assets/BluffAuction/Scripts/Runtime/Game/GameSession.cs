using System;
using System.Collections.Generic;
using BluffAuction.AI;
using BluffAuction.Core;
using UnityEngine;

namespace BluffAuction.Game
{
    /// <summary>한 판을 시작하는 데 필요한 것 (메뉴의 새 게임 화면이 채운다).</summary>
    [Serializable]
    public sealed class GameSetup
    {
        public GameRules Rules = new GameRules();
        public List<SeatInfo> Seats = new List<SeatInfo>();
        public Difficulty Difficulty = Difficulty.Normal;
        public int Seed;
        public int LocalSeat;
    }

    /// <summary>
    /// 한 판의 진행. 세 가지로 돈다:
    /// - 혼자: 규칙 엔진을 시간에 맞춰 돌리고 AI 좌석을 생각하게 한다. 일시정지하면 엔진 시간이 멈춘다.
    /// - 온라인 방장: 같지만 사람 좌석(RemoteSeats)은 네트워크 명령으로 움직이고, 좌석마다 걸러진 상태·사건을 보낸다(RawPumped).
    /// - 온라인 참가자: 엔진 없이 방장이 보낸 내 좌석 상태(GameView)와 사건만 보여 주고, 명령은 방장에게 보낸다.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public AuctionEngine Engine { get; private set; }
        public GameSetup Setup { get; private set; }
        public int LocalSeat { get; private set; }
        public GameView View { get; private set; }
        public bool Paused;
        public float AiSpeed = 1f;
        /// <summary>시험용: 내 좌석도 AI 가 둔다 (자동 플레이 촬영).</summary>
        public bool Autopilot;
        /// <summary>세션 동안(재대결 포함) AI 가 배운 판매자 습관.</summary>
        public static OpponentMemory Memory = new OpponentMemory();

        /// <summary>온라인: 다른 사람이 앉은 좌석 (AI 가 두지 않는다).</summary>
        public readonly HashSet<int> RemoteSeats = new HashSet<int>();
        public bool IsClient { get; private set; }
        public bool IsOnline { get { return IsClient || RemoteSeats.Count > 0; } }
        /// <summary>참가자: 명령을 방장에게 보내는 곳.</summary>
        public Action<int, int, string, Registration> SendCommand;
        /// <summary>방장: 쌓인 원본 사건 (좌석별로 걸러 보내는 데 쓴다).</summary>
        public event Action<List<GameEvent>> RawPumped;

        public event Action<GameEvent> EventShown;
        public event Action ViewChanged;
        public event Action Finished;

        readonly List<GameEvent> drained = new List<GameEvent>();
        AiPlayer[] ais;
        SeatActions[] acts;
        GameView[] aiViews;
        int lastVersion = -1;
        float carryMs;
        bool finishedRaised;
        float viewReceivedAt;
        bool clientRunning;

        sealed class SeatActions : IAiActions
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

        public bool Running { get { return Engine != null || clientRunning; } }

        public void Begin(GameSetup setup)
        {
            IsClient = false;
            clientRunning = false;
            Setup = setup;
            LocalSeat = setup.LocalSeat;
            int seed = setup.Seed != 0 ? setup.Seed : Environment.TickCount;
            Engine = new AuctionEngine(setup.Rules, setup.Seats, seed);
            int n = Engine.SeatCount;
            ais = new AiPlayer[n];
            acts = new SeatActions[n];
            aiViews = new GameView[n];
            for (int i = 0; i < n; i++)
            {
                acts[i] = new SeatActions { E = Engine, Seat = i };
                aiViews[i] = new GameView();
                bool auto = Autopilot && i == LocalSeat;
                if ((setup.Seats[i].IsAI && i != LocalSeat && !RemoteSeats.Contains(i)) || auto)
                    ais[i] = MakeAI(i, auto ? "ledger" : setup.Seats[i].AiProfile, seed);
            }
            View = new GameView();
            finishedRaised = false;
            Paused = false;
            lastVersion = -1;
            Engine.BuildView(LocalSeat, View);
        }

        AiPlayer MakeAI(int seat, string profile, int seed)
        {
            var prof = AiProfiles.ForDifficulty(AiProfiles.Get(profile), Setup.Difficulty);
            // 설정의 'AI 반응 속도'
            float sp = Mathf.Clamp(AiSpeed, 0.25f, 4f);
            prof.ReactMin /= sp;
            prof.ReactMax /= sp;
            return new AiPlayer(seat, prof, seed + seat * 977, Memory);
        }

        /// <summary>온라인 참가자로 시작: 엔진 없이 방장의 상태만 받는다.</summary>
        public void BeginClient(GameSetup setup)
        {
            Engine = null;
            ais = null;
            IsClient = true;
            clientRunning = true;
            RemoteSeats.Clear();
            Setup = setup;
            LocalSeat = setup.LocalSeat;
            View = new GameView { Viewer = LocalSeat };
            finishedRaised = false;
            Paused = false;
            lastVersion = -1;
            clientAI = Autopilot ? new AiPlayer(LocalSeat, AiProfiles.Get("professor"), Environment.TickCount) : null;
            clientActs = new ClientActions { S = this };
        }

        AiPlayer clientAI;
        ClientActions clientActs;
        long viewNow;
        int viewRemaining;

        /// <summary>참가자 자동 진행(시험)용: AI 명령을 방장에게 보낸다.</summary>
        sealed class ClientActions : IAiActions
        {
            public GameSession S;
            public Result Register(Registration reg) { return S.Register(reg); }
            public Result Bid(int amount) { return S.Bid(amount); }
            public Result UseAbility(AbilityKind kind) { return S.Ability(kind); }
            public Result PriceDrop(int newPrice) { return S.PriceDrop(newPrice); }
            public Result Loan() { return S.Loan(); }
            public Result Say(int line) { return S.Say(line); }
        }

        /// <summary>화면·연출이 사건을 받을 준비가 된 뒤 엔진을 시작한다 (첫 사건을 놓치지 않게).</summary>
        public void Go()
        {
            if (Engine == null) return;
            Engine.Start();
            Pump();
        }

        public void End()
        {
            Engine = null;
            ais = null;
            clientRunning = false;
            IsClient = false;
            RemoteSeats.Clear();
            SendCommand = null;
        }

        /// <summary>방장: 나간 사람의 자리를 AI 가 이어받는다.</summary>
        public void ReplaceWithAI(int seat)
        {
            if (Engine == null || seat < 0 || seat >= Engine.SeatCount) return;
            RemoteSeats.Remove(seat);
            string profile = AiProfiles.All[seat % AiProfiles.All.Length].Id;
            Engine.ReplaceWithAI(seat, profile);
            ais[seat] = MakeAI(seat, profile, Environment.TickCount);
            Pump();
        }

        void Update()
        {
            if (IsClient && clientRunning && View != null && View.Seats.Length > 0)
            {
                // 받은 상태 사이의 시간도 흐르게 (AI 자동 진행과 시간 표시)
                int elapsed = (int)((Time.unscaledTime - viewReceivedAt) * 1000f);
                View.NowMs = viewNow + elapsed;
                View.RemainingMs = Math.Max(0, viewRemaining - elapsed);
                if (clientAI != null) clientAI.Think(View, clientActs);
            }
            if (Engine == null) return;
            // 온라인에서는 멈추지 않는다 (다른 사람이 기다리므로)
            if (!Paused || IsOnline)
            {
                // 화면이 멈칫해도 한 번에 너무 많이 흐르지 않게 (최대 0.25초)
                carryMs += Mathf.Min(Time.unscaledDeltaTime, 0.25f) * 1000f;
                int ms = (int)carryMs;
                carryMs -= ms;
                if (ms > 0) Engine.Advance(ms);
                ThinkAI();
            }
            Pump();
        }

        void ThinkAI()
        {
            for (int i = 0; i < ais.Length; i++)
            {
                if (ais[i] == null) continue;
                Engine.BuildView(i, aiViews[i]);
                ais[i].Think(aiViews[i], acts[i]);
            }
        }

        /// <summary>쌓인 사건을 AI 와 화면에 나눠 주고 내 화면 상태를 새로 만든다.</summary>
        void Pump()
        {
            if (Engine == null) return;
            drained.Clear();
            Engine.DrainEvents(drained);
            foreach (var ev in drained)
            {
                for (int i = 0; i < ais.Length; i++)
                {
                    if (ais[i] == null) continue;
                    GameEvent shown;
                    if (!Engine.TryFilterEvent(ev, i, out shown)) continue;
                    Engine.BuildView(i, aiViews[i]);
                    ais[i].OnEvent(shown, aiViews[i]);
                }
                GameEvent mine;
                if (Engine.TryFilterEvent(ev, LocalSeat, out mine))
                {
                    Engine.BuildView(LocalSeat, View);
                    if (EventShown != null) EventShown(mine);
                }
            }
            if (RawPumped != null) RawPumped(drained);
            Engine.BuildView(LocalSeat, View);
            AfterState();
            if (View.Phase == Phase.Results && !finishedRaised)
            {
                finishedRaised = true;
                AiPlayer.LearnFromReveal(Memory, Engine.BuildView(-1));
                if (Finished != null) Finished();
            }
        }

        void AfterState()
        {
            if (View.Version != lastVersion)
            {
                lastVersion = View.Version;
                if (ViewChanged != null) ViewChanged();
            }
        }

        // ═════════════════ 참가자: 방장이 보낸 것 ═════════════════

        public void ReceiveView(GameView v)
        {
            if (!IsClient || v == null) return;
            View = v;
            viewReceivedAt = Time.unscaledTime;
            viewNow = v.NowMs;
            viewRemaining = v.RemainingMs;
            AfterState();
        }

        public void ReceiveEvents(List<GameEvent> events)
        {
            if (!IsClient) return;
            foreach (var e in events)
            {
                if (clientAI != null) clientAI.OnEvent(e, View);
                if (EventShown != null) EventShown(e);
            }
            if (View.Phase == Phase.Results && !finishedRaised)
            {
                finishedRaised = true;
                AiPlayer.LearnFromReveal(Memory, View);
                if (Finished != null) Finished();
            }
        }

        /// <summary>방장: 사람 좌석의 명령을 엔진에 넣는다.</summary>
        public Result Apply(int seat, int kind, int a, string text, Registration reg)
        {
            if (Engine == null) return Result.WrongPhase;
            Result r;
            switch (kind)
            {
                case 1: r = Engine.SubmitRegistration(seat, reg); break;
                case 2: r = Engine.Bid(seat, a); break;
                case 3: r = Engine.UseAbility(seat, (AbilityKind)a); break;
                case 4: r = Engine.DecidePriceDrop(seat, a); break;
                case 5: r = Engine.TakeLoan(seat); break;
                case 6: r = Engine.Say(seat, a, text); break;
                case 7: r = Engine.VoteSkip(seat, a != 0); break;
                case 8: r = Engine.VoteEnd(seat, a != 0); break;
                default: r = Result.Invalid; break;
            }
            Pump();
            return r;
        }

        // ═════════════════ 내 명령 ═════════════════

        Result Do(int kind, int a, string text, Registration reg)
        {
            if (IsClient)
            {
                if (SendCommand != null) SendCommand(kind, a, text, reg);
                return Result.Ok;
            }
            if (Engine == null) return Result.WrongPhase;
            Result r;
            switch (kind)
            {
                case 1: r = Engine.SubmitRegistration(LocalSeat, reg); break;
                case 2: r = Engine.Bid(LocalSeat, a); break;
                case 3: r = Engine.UseAbility(LocalSeat, (AbilityKind)a); break;
                case 4: r = Engine.DecidePriceDrop(LocalSeat, a); break;
                case 5: r = Engine.TakeLoan(LocalSeat); break;
                case 6: r = Engine.Say(LocalSeat, a, text); break;
                case 7: r = Engine.VoteSkip(LocalSeat, a != 0); break;
                default: r = Engine.VoteEnd(LocalSeat, a != 0); break;
            }
            Pump();
            return r;
        }

        public Result Register(Registration reg) { return Do(1, 0, null, reg); }
        public Result Bid(int amount) { return Do(2, amount, null, null); }
        public Result Ability(AbilityKind k) { return Do(3, (int)k, null, null); }
        public Result PriceDrop(int price) { return Do(4, price, null, null); }
        public Result Loan() { return Do(5, 0, null, null); }
        public Result Say(int line, string text = null) { return Do(6, line, text, null); }
        public Result Skip(bool on) { return Do(7, on ? 1 : 0, null, null); }
        public Result EndVote(bool on) { return Do(8, on ? 1 : 0, null, null); }

        /// <summary>남은 시간 (화면 표시용, 매 프레임 갱신). 참가자는 받은 뒤 흐른 시간을 뺀다.</summary>
        public int RemainingMs
        {
            get
            {
                if (Engine != null) return Engine.RemainingMs;
                if (IsClient && View != null)
                {
                    int ms = viewRemaining - (int)((Time.unscaledTime - viewReceivedAt) * 1000f);
                    return ms > 0 ? ms : 0;
                }
                return 0;
            }
        }
    }
}
