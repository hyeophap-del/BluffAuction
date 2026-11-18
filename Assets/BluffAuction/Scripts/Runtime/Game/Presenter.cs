using System.Collections;
using BluffAuction.Audio;
using BluffAuction.Characters;
using BluffAuction.Core;
using BluffAuction.Loc;
using BluffAuction.UI;
using BluffAuction.World;
using UnityEngine;
using EventType = BluffAuction.Core.EventType;

namespace BluffAuction.Game
{
    /// <summary>
    /// 사건 → 연출: 카메라 구도, 손님·경매사 몸짓, 효과음·목소리, 색종이. 화면(GameScreen)에도 사건을 넘긴다.
    /// 게임 규칙은 건드리지 않는다 (보여 주기만).
    /// </summary>
    public sealed class Presenter : MonoBehaviour
    {
        GameApp app;
        GameSession session;
        GameScreen screen;
        int lastPrice;
        int lastLot = -1;
        Coroutine shotRoutine;
        ParticleSystem confetti, sparkle;
        VisualPause pause;
        int lastSecondCalled = -1;

        public void Init(GameApp app)
        {
            this.app = app;
            confetti = Fx.Confetti(transform);
            sparkle = Fx.Sparkle(transform);
            app.Stage.Auctioneer.OnImpact = g =>
            {
                if (g != Gesture.Gavel) return;
                AudioManager.I.Play("sfx_gavel_single", 1f);
                app.Rig.Kick(0.6f);
            };
        }

        public void Attach(GameSession s, GameScreen gs)
        {
            Detach();
            session = s;
            screen = gs;
            session.EventShown += OnEvent;
            session.Finished += OnFinished;
            lastLot = -1;
            app.Rig.Go(Shot.Wide, 1.2f);
        }

        public void Detach()
        {
            if (session != null)
            {
                session.EventShown -= OnEvent;
                session.Finished -= OnFinished;
            }
            if (screen != null) screen.Dispose();
            session = null;
            screen = null;
            if (pause != null) pause.Close();
            pause = null;
        }

        Stage St { get { return app.Stage; } }
        CameraRig Rig { get { return app.Rig; } }
        GameView V { get { return session.View; } }

        Guest G(int seat) { return seat >= 0 && seat < St.Guests.Count ? St.Guests[seat] : null; }

        int VoiceOf(int seat)
        {
            var g = G(seat);
            return g != null && g.Look != null ? g.Look.Voice : 0;
        }

        void Frame(World.Shot s, float seconds, Vector3 subject = default(Vector3))
        {
            // 1인칭에서는 내 고개를 함부로 돌리지 않는다 (라이어스 바처럼 내가 보는 곳이 화면)
            if (Rig.FirstPerson) return;
            Rig.Go(s, seconds, subject);
        }

        /// <summary>잠깐 다른 구도를 보여 주고 기본 구도로 돌아온다.</summary>
        void Glimpse(World.Shot s, float hold, Vector3 subject = default(Vector3), float travel = 0.6f)
        {
            if (Rig.FirstPerson) return;
            if (shotRoutine != null) StopCoroutine(shotRoutine);
            shotRoutine = StartCoroutine(GlimpseRoutine(s, hold, subject, travel));
        }

        IEnumerator GlimpseRoutine(World.Shot s, float hold, Vector3 subject, float travel)
        {
            Rig.Go(s, travel, subject);
            yield return new WaitForSecondsRealtime(hold);
            if (session != null && (V.BiddingOpen || V.Phase == Phase.Presenting || V.Phase == Phase.AuctionResult)) Rig.Go(World.Shot.Seat, 1f);
            shotRoutine = null;
        }

        void LookAllAt(Vector3 p, float seconds, int except = -1)
        {
            St.AllLookAt(p, except);
            if (seconds > 0) StartCoroutine(BackToItem(seconds));
        }

        IEnumerator BackToItem(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (session != null && St != null) St.AllLookAt(St.ItemPoint);
        }

        // ═════════════════ 사건 ═════════════════

        void OnEvent(GameEvent e)
        {
            if (screen != null) screen.OnEvent(e);
            var v = V;
            switch (e.Type)
            {
                case EventType.GameStarted:
                    AudioManager.I.Play("sfx_cards", 0.8f);
                    Frame(World.Shot.Register, 1.6f);
                    St.Auctioneer.Play(Gesture.Talk, 2f);
                    AudioManager.I.Voice(8);
                    break;

                case EventType.Registered:
                    if (e.Seat != v.Viewer)
                    {
                        var g = G(e.Seat);
                        if (g != null) g.Play(Gesture.Think, 1.5f);
                    }
                    AudioManager.I.Play("sfx_cards", 0.4f, 1.2f);
                    break;

                case EventType.LotPresented:
                {
                    var it = v.Items[e.Item];
                    St.ShowLot(it.Appearance, ItemImages.Get(e.Item, v.Viewer));
                    lastPrice = 0;
                    lastLot = e.Item;
                    lastSecondCalled = -1;
                    AudioManager.I.Play("sfx_bell", 0.8f);
                    AudioManager.I.Voice(8);
                    St.Auctioneer.Play(Gesture.Talk, 2.2f);
                    St.AllLookAt(St.ItemPoint);
                    var seller = G(e.Seat);
                    if (seller != null && e.Seat != v.Viewer) seller.Play(Gesture.Talk, 1.8f);
                    Glimpse(e.B == 1 ? World.Shot.Item : World.Shot.ItemLow, Mathf.Max(1.2f, v.Rules.PresentSeconds - 0.8f), default(Vector3), 0.9f);
                    break;
                }

                case EventType.BiddingOpened:
                    if (Rig.Current != World.Shot.Seat && shotRoutine == null) Frame(World.Shot.Seat, 0.9f);
                    break;

                case EventType.BidPlaced:
                {
                    int jump = lastPrice > 0 ? e.A - lastPrice : 0;
                    bool big = lastPrice > 0 && jump >= Mathf.Max(800, lastPrice / 4);
                    lastPrice = e.A;
                    int seat = e.Seat;
                    if (seat >= 0)
                    {
                        var g = G(seat);
                        if (g != null) g.Play(big ? Gesture.BigBid : Gesture.Bid);
                        if (seat != v.Viewer)
                        {
                            Vector3 head = St.SeatHead(seat);
                            LookAllAt(head, 1.3f, seat);
                            St.Auctioneer.PointAt(head, 1.2f);
                            if (screen != null) screen.FloatText(seat, L.G(e.A), Stage.SeatColors[v.Seats[seat].ColorIndex % Stage.SeatColors.Length]);
                            if (big) Glimpse(World.Shot.Bidder, 1.3f, St.SeatWorld(seat));
                        }
                        else
                        {
                            St.Auctioneer.PointAt(Rig.transform.position, 1.0f);
                            if (app.TableKit != null) app.TableKit.PaddlePop(big);
                        }
                    }
                    else St.Auctioneer.Play(Gesture.Talk, 0.8f);
                    AudioManager.I.Play(big ? "sfx_bid_big" : "sfx_bid", big ? 0.9f : 0.75f, Random.Range(0.95f, 1.05f));
                    if (big) AudioManager.I.Play("crowd_ooh", 0.5f);
                    Rig.Kick(big ? 0.5f : 0.15f);
                    break;
                }

                case EventType.AbilityUsed:
                {
                    AudioManager.I.Play("sfx_ability", 0.8f);
                    var g = G(e.Seat);
                    if (g != null)
                    {
                        if (e.Seat != v.Viewer) g.Play(Gesture.Think, 1.6f);
                        sparkle.transform.position = St.SeatHead(e.Seat) + Vector3.up * 0.1f;
                        sparkle.Play();
                        if (e.Seat != v.Viewer) LookAllAt(St.SeatHead(e.Seat), 1.0f, e.Seat);
                    }
                    break;
                }

                case EventType.SellerSaid:
                {
                    AudioManager.I.Voice(VoiceOf(e.Seat));
                    var g = G(e.Seat);
                    if (g != null && e.Seat != v.Viewer) g.Play(Gesture.Talk, 1.4f);
                    break;
                }

                case EventType.NoBids:
                {
                    St.Auctioneer.Play(Gesture.Shrug);
                    var g = G(e.Seat);
                    if (g != null && e.Seat != v.Viewer) g.Play(Gesture.Slump, 2f);
                    AudioManager.I.Play("crowd_gasp", 0.35f);
                    break;
                }

                case EventType.Deferred:
                    AudioManager.I.Play("sfx_whoosh", 0.7f);
                    StartCoroutine(HideItemLater(1.2f));
                    break;

                case EventType.Unsold:
                {
                    AudioManager.I.Play("sfx_unsold", 0.8f);
                    var g = G(e.Seat);
                    if (g != null && e.Seat != v.Viewer) g.Play(Gesture.Slump, 2f);
                    StartCoroutine(HideItemLater(1.6f));
                    break;
                }

                case EventType.Sold:
                {
                    St.Auctioneer.Play(Gesture.Gavel);
                    var winner = G(e.Seat);
                    if (winner != null) winner.Play(Gesture.Cheer);
                    for (int i = 0; i < St.Guests.Count; i++) if (i != e.Seat && i != v.Viewer && Random.value < 0.6f) St.Guests[i].Play(Gesture.Clap, 1.8f);
                    StartCoroutine(Celebrate(e.Seat, v.Viewer));
                    StartCoroutine(HideItemLater(2.4f));
                    break;
                }

                case EventType.LoanTaken:
                    AudioManager.I.Play("sfx_loan", 0.8f);
                    if (e.Seat != v.Viewer && G(e.Seat) != null) G(e.Seat).Play(Gesture.Laugh, 1.4f);
                    break;

                case EventType.RevealStarted:
                    AudioManager.I.Duck(0.35f);
                    AudioManager.I.Play("sfx_drumroll", 0.8f);
                    Frame(World.Shot.Reveal, 1.4f);
                    St.Auctioneer.Play(Gesture.Talk, 2.4f);
                    AudioManager.I.Voice(8);
                    St.HideItem();
                    break;

                case EventType.ItemRevealed:
                {
                    var it = v.Items[e.Item];
                    St.ShowLot(it.Appearance, ItemImages.Get(e.Item, v.Viewer));
                    St.AllLookAt(St.ItemPoint);
                    bool sold = it.Status == ItemStatus.Sold;
                    int profit = e.A - e.B;
                    if (sold && profit >= Mathf.Max(1000, e.A / 4))
                    {
                        AudioManager.I.Play("sfx_reveal_good", 0.8f);
                        if (G(it.Winner) != null) G(it.Winner).Play(Gesture.Cheer, 1.8f);
                        if (G(it.Seller) != null) G(it.Seller).Play(Gesture.Slump, 1.8f);
                    }
                    else if (sold && profit < -Mathf.Max(800, e.B / 5))
                    {
                        AudioManager.I.Play("sfx_reveal_bad", 0.8f);
                        if (G(it.Winner) != null) G(it.Winner).Play(Gesture.Slump, 2f);
                        if (G(it.Seller) != null) G(it.Seller).Play(Gesture.Laugh, 1.8f);
                        AudioManager.I.Play("crowd_laugh", 0.45f);
                    }
                    else AudioManager.I.Play("sfx_reveal_ting", 0.8f);
                    break;
                }

                case EventType.Results:
                    break;
            }
        }

        IEnumerator HideItemLater(float t)
        {
            int lot = lastLot;
            yield return new WaitForSecondsRealtime(t);
            if (session != null && lastLot == lot && (V.Phase == Phase.AuctionResult || V.Phase == Phase.Reveal)) St.HideItem();
        }

        IEnumerator Celebrate(int winner, int viewer)
        {
            yield return new WaitForSecondsRealtime(0.62f);
            AudioManager.I.Play("crowd_applause", winner == viewer ? 0.8f : 0.5f);
            if (winner >= 0)
            {
                confetti.transform.position = winner == viewer ? Rig.transform.position + Rig.transform.forward * 1.6f + Vector3.up * 0.8f : St.SeatHead(winner) + Vector3.up * 0.9f;
                confetti.Play();
                if (winner != viewer) Glimpse(World.Shot.Bidder, 1.8f, St.SeatWorld(winner), 0.7f);
                else AudioManager.I.Play("ui_coin", 1f);
            }
        }

        void OnFinished()
        {
            var v = V;
            AudioManager.I.Duck(1f);
            AudioManager.I.PlayMusic("bgm_results", 1.2f);
            AudioManager.I.Play("sfx_fanfare", 0.8f);
            Rig.FirstPerson = false;
            Rig.MouseLook = false;
            Frame(World.Shot.Wide, 2f);
            St.HideItem();
            int me = v.Viewer;
            for (int i = 0; i < v.SeatCount; i++)
            {
                var g = G(i);
                if (g == null || i == me) continue;
                g.Play(v.Seats[i].Rank == 1 ? Gesture.Cheer : Gesture.Clap, 3f);
            }
            bool won = me >= 0 && v.Seats[me].Rank == 1;
            StartCoroutine(PlayLater(won ? "sfx_win" : "sfx_lose", 3.2f));
            Achievements.Evaluate(v, session.Setup);
        }

        IEnumerator PlayLater(string clip, float t)
        {
            yield return new WaitForSecondsRealtime(t);
            AudioManager.I.Play(clip, 0.9f);
        }

        void Update()
        {
            if (session == null || !session.Running) return;
            // 마감 직전 경매사의 '하나… 둘…'
            var v = V;
            if (v.BiddingOpen && v.HighestBidder != -1)
            {
                int sec = Mathf.CeilToInt(session.RemainingMs / 1000f);
                if (sec != lastSecondCalled && (sec == 3 || sec == 2))
                {
                    lastSecondCalled = sec;
                    St.Auctioneer.Play(Gesture.Talk, 0.8f);
                    AudioManager.I.Voice(8, 0.02f);
                    if (screen != null) screen.ShowBanner(L.T(sec == 3 ? "going_once" : "going_twice"), "", 0.9f);
                }
            }
        }

        // ═════════════════ 일시정지 ═════════════════

        public void TogglePause()
        {
            if (session == null) return;
            if (pause != null)
            {
                pause.Close();
                pause = null;
                session.Paused = false;
                return;
            }
            session.Paused = true;
            pause = new VisualPause(app, () => { pause = null; session.Paused = false; });
        }
    }
}
