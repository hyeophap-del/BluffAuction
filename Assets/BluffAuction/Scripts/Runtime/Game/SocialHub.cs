using System;
using System.Collections;
using System.Collections.Generic;
using BluffAuction.Audio;
using BluffAuction.Characters;
using BluffAuction.Core;
using BluffAuction.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BluffAuction.Game
{
    /// <summary>
    /// 어울리기: 채팅·감정 표현·던지기·목소리·직접 넣은 물건 그림을 주고받는다.
    /// 혼자 할 때는 바로 보여 주고, 온라인에서는 방장을 거쳐 모두에게 간다 (방장이 보낸 사람 좌석을 채운다).
    /// 방장(또는 혼자)일 때 AI 손님이 채팅·몸짓·맞은 것에 반응한다.
    /// </summary>
    public sealed class SocialHub : MonoBehaviour
    {
        public static SocialHub I { get; private set; }
        public const int MaxChat = 100;

        /// <summary>채팅 한 줄을 보여 줄 때 (좌석, 글).</summary>
        public event Action<int, string> ChatShown;
        /// <summary>감정 표현 (좌석, 몸짓, 대상 좌석).</summary>
        public event Action<int, Gesture, int> EmoteShown;
        /// <summary>던진 물건이 사람에게 맞았을 때 (던진 사람, 맞은 사람).</summary>
        public event Action<int, int> HitShown;

        public readonly HashSet<int> Muted = new HashSet<int>();
        readonly Dictionary<int, VoicePlayer> players = new Dictionary<int, VoicePlayer>();
        GameApp app;
        float lastChat, lastEmote, lastThrow;
        VoiceCapture mic;
        readonly System.Random rng = new System.Random();

        public static SocialHub Create(GameApp app)
        {
            var go = new GameObject("SocialHub");
            go.transform.SetParent(app.transform, false);
            I = go.AddComponent<SocialHub>();
            I.app = app;
            I.mic = VoiceCapture.Create(go.transform);
            I.mic.Synthetic = Dev.AutoShots.Requested;
            I.mic.Packet += I.OnMicPacket;
            return I;
        }

        NetRoom Room { get { return NetRoom.I; } }
        bool Online { get { return Room != null && Room.Active; } }
        /// <summary>엔진을 가진 쪽 (혼자 또는 방장) — AI 반응을 정한다.</summary>
        bool Authority { get { return !Online || Room.IsHost; } }

        public int MySeat
        {
            get
            {
                if (Online) return Room.MySeat;
                return app.Session != null && app.Session.Running ? app.Session.LocalSeat : 0;
            }
        }

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = AuctionEngine.Sanitize(s);
            return s.Length > MaxChat ? s.Substring(0, MaxChat) : s;
        }

        Guest GuestAt(int seat)
        {
            var st = app.Stage;
            return seat >= 0 && seat < st.Guests.Count ? st.Guests[seat] : null;
        }

        bool IsAISeat(int seat)
        {
            var e = app.Session != null ? app.Session.Engine : null;
            if (e != null) return seat >= 0 && seat < e.SeatCount && e.Seats[seat].Info.IsAI && !app.Session.RemoteSeats.Contains(seat) && seat != app.Session.LocalSeat;
            if (Online && !Room.InGame) return seat >= 0 && seat < Room.Lobby.Slots.Count && Room.Lobby.Slots[seat].IsAI;
            return false;
        }

        // ═════════════════ 채팅 ═════════════════

        public bool SendChat(string text)
        {
            text = Clean(text);
            if (text.Length == 0 || Time.unscaledTime - lastChat < 0.4f) return false;
            lastChat = Time.unscaledTime;
            var m = new ChatMsg { Seat = MySeat, Text = text };
            if (Online) Room.SendSocial(Msg.Chat, m);
            OnChat(m);
            return true;
        }

        public void OnChat(ChatMsg m)
        {
            // 음소거한 사람의 글은 보이지 않는다
            if (Muted.Contains(m.Seat) && m.Seat != MySeat) return;
            if (ChatShown != null) ChatShown(m.Seat, m.Text);
            var g = GuestAt(m.Seat);
            if (g != null && !g.Busy) g.Play(Gesture.Talk, 1.2f);
            if (m.Seat != MySeat) AudioManager.I.Play("ui_chat", 0.5f, 1f, Bus.Ui);
            if (Authority && !IsAISeat(m.Seat)) StartCoroutine(BanterBack(m));
        }

        /// <summary>사람이 채팅하면 가끔 AI 손님이 한마디 받아친다 (각자 언어로 보이도록 빠른 한마디 번호로).</summary>
        IEnumerator BanterBack(ChatMsg m)
        {
            var e = app.Session != null ? app.Session.Engine : null;
            if (e == null || rng.NextDouble() > 0.4) yield break;
            var ais = new List<int>();
            for (int i = 0; i < e.SeatCount; i++) if (IsAISeat(i)) ais.Add(i);
            if (ais.Count == 0) yield break;
            yield return new WaitForSecondsRealtime(1f + (float)rng.NextDouble() * 1.8f);
            int ai = ais[rng.Next(ais.Count)];
            bool question = m.Text.IndexOf('?') >= 0 || m.Text.IndexOf('？') >= 0;
            int first = question ? Content.LineBluffFirst : Content.LineTauntFirst;
            int count = question ? Content.LineBluffCount : Content.LineTauntCount;
            app.Session.Apply(ai, 6, first + rng.Next(count), null, null);
        }

        // ═════════════════ 감정 표현 ═════════════════

        public void SendEmote(Gesture g, int target)
        {
            if (!Guest.IsEmote(g) || Time.unscaledTime - lastEmote < 0.8f) return;
            lastEmote = Time.unscaledTime;
            var m = new EmoteMsg { Seat = MySeat, Id = (int)g, Target = target };
            if (Online) Room.SendSocial(Msg.Emote, m);
            OnEmote(m);
        }

        public void OnEmote(EmoteMsg m)
        {
            var g = (Gesture)m.Id;
            if (!Guest.IsEmote(g)) return;
            // 무례한 몸짓을 끈 사람에게는 화내기로 보인다
            var shown = g == Gesture.MiddleFinger && m.Seat != MySeat && !SaveData.I.Settings.ShowRude ? Gesture.Angry : g;
            var guest = GuestAt(m.Seat);
            if (guest != null)
            {
                Vector3? at = null;
                if (m.Target >= 0 && m.Target < app.Stage.Guests.Count) at = app.Stage.SeatHead(m.Target);
                guest.Emote(shown, at);
            }
            AudioManager.I.Play("sfx_pop", 0.5f, 0.9f + (float)rng.NextDouble() * 0.2f);
            if (EmoteShown != null) EmoteShown(m.Seat, shown, m.Target);
            if (Authority && m.Target >= 0 && IsAISeat(m.Target) && !IsAISeat(m.Seat)) StartCoroutine(ReactToEmote(m.Target, m.Seat, g));
        }

        IEnumerator ReactToEmote(int ai, int from, Gesture g)
        {
            yield return new WaitForSecondsRealtime(0.7f + (float)rng.NextDouble() * 0.8f);
            Gesture back;
            switch (g)
            {
                case Gesture.Wave: back = Gesture.Wave; break;
                case Gesture.ThumbsUp: back = rng.Next(2) == 0 ? Gesture.ThumbsUp : Gesture.Laugh; break;
                case Gesture.MiddleFinger: back = rng.Next(3) == 0 ? Gesture.MiddleFinger : Gesture.Angry; break;
                case Gesture.Mock: back = rng.Next(2) == 0 ? Gesture.Angry : Gesture.Facepalm; break;
                case Gesture.Angry: back = Gesture.Shrug; break;
                case Gesture.Peace: back = Gesture.Peace; break;
                default: back = Gesture.Laugh; break;
            }
            if (rng.NextDouble() < 0.25) yield break;
            var m = new EmoteMsg { Seat = ai, Id = (int)back, Target = from };
            if (Online) Room.SendSocial(Msg.Emote, m);
            OnEmote(m);
        }

        // ═════════════════ 던지기 ═════════════════

        public void SendThrow(int kind, int target, float yaw, float pitch, float power)
        {
            if (Time.unscaledTime - lastThrow < 0.5f) return;
            lastThrow = Time.unscaledTime;
            var m = new ThrowMsg { Seat = MySeat, Kind = kind, Target = target, Yaw = yaw, Pitch = pitch, Power = power, Seed = rng.Next() };
            if (Online) Room.SendSocial(Msg.Throw, m);
            OnThrow(m);
        }

        public void OnThrow(ThrowMsg m)
        {
            if (app.Throwables != null) app.Throwables.Launch(m);
        }

        /// <summary>던진 물건이 사람에게 맞았다 (모든 컴퓨터에서 각자 본다). AI 반응은 방장만 정한다.</summary>
        public void OnHit(int thrower, int victim)
        {
            var g = GuestAt(victim);
            if (g != null) g.Play(Gesture.Shock, 1.0f);
            if (HitShown != null) HitShown(thrower, victim);
            if (Authority && IsAISeat(victim) && thrower != victim) StartCoroutine(ReactToHit(victim, thrower));
        }

        IEnumerator ReactToHit(int ai, int thrower)
        {
            yield return new WaitForSecondsRealtime(0.9f + (float)rng.NextDouble() * 0.6f);
            double r = rng.NextDouble();
            if (r < 0.45)
            {
                // 되던지기
                var m = new ThrowMsg { Seat = ai, Kind = rng.Next(Table.Throwables.KindCount), Target = thrower, Power = 1f, Seed = rng.Next() };
                if (Online) Room.SendSocial(Msg.Throw, m);
                OnThrow(m);
            }
            else
            {
                var m = new EmoteMsg { Seat = ai, Id = (int)(r < 0.75 ? Gesture.Angry : Gesture.Mock), Target = thrower };
                if (Online) Room.SendSocial(Msg.Emote, m);
                OnEmote(m);
            }
            var e = app.Session != null ? app.Session.Engine : null;
            if (e != null && rng.NextDouble() < 0.5) app.Session.Apply(ai, 6, Content.LineTauntFirst + rng.Next(Content.LineTauntCount), null, null);
        }

        // ═════════════════ 근접 무기 ═════════════════

        /// <summary>근접 무기로 때렸다 (때린 사람, 맞은 사람, 무기).</summary>
        public event Action<int, int, int> BonkShown;
        float lastMelee;

        public void SendMelee(int kind, int target)
        {
            if (Time.unscaledTime - lastMelee < 0.45f) return;
            lastMelee = Time.unscaledTime;
            var m = new MeleeMsg { Seat = MySeat, Kind = kind, Target = target };
            if (Online) Room.SendSocial(Msg.Melee, m);
            OnMelee(m);
        }

        public void OnMelee(MeleeMsg m)
        {
            if (app.Throwables != null) app.Throwables.PlaySwing(m);
        }

        /// <summary>휘두른 무기가 맞았다 (모두 각자 본다). AI 반응은 방장만.</summary>
        public void OnBonk(int attacker, int victim, int kind)
        {
            var g = GuestAt(victim);
            if (g != null) { g.Play(Gesture.Shock, 0.9f); g.Knock(); }
            if (BonkShown != null) BonkShown(attacker, victim, kind);
            if (Authority && IsAISeat(victim) && attacker != victim) StartCoroutine(ReactToBonk(victim, attacker));
        }

        IEnumerator ReactToBonk(int ai, int attacker)
        {
            yield return new WaitForSecondsRealtime(0.8f + (float)rng.NextDouble() * 0.6f);
            double r = rng.NextDouble();
            if (r < 0.4 && app.Throwables != null && app.Throwables.InReach(ai, attacker))
            {
                // 되갚아 때리기
                var m = new MeleeMsg { Seat = ai, Kind = rng.Next(Table.Throwables.MeleeCount), Target = attacker };
                if (Online) Room.SendSocial(Msg.Melee, m);
                OnMelee(m);
            }
            else if (r < 0.65)
            {
                var m = new ThrowMsg { Seat = ai, Kind = rng.Next(Table.Throwables.KindCount), Target = attacker, Power = 1f, Seed = rng.Next() };
                if (Online) Room.SendSocial(Msg.Throw, m);
                OnThrow(m);
            }
            else
            {
                var m = new EmoteMsg { Seat = ai, Id = (int)(r < 0.85 ? Gesture.Angry : Gesture.Facepalm), Target = attacker };
                if (Online) Room.SendSocial(Msg.Emote, m);
                OnEmote(m);
            }
        }

        // ═════════════════ 목소리 ═════════════════

        public VoiceCapture Mic { get { return mic; } }

        void OnMicPacket(byte[] frames, int count)
        {
            if (testing)
            {
                // 마이크 시험: 내 목소리를 압축·풀기까지 거쳐 그대로 들려준다
                if (loopback == null) loopback = VoicePlayer.Create(transform, "loopback");
                var data = Wire.PackVoice(0, frames, count);
                loopback.Push(data, 3, count);
                return;
            }
            if (!Online) return;
            Room.SendRaw(Wire.PackVoice(MySeat, frames, count));
        }

        bool testing;
        VoicePlayer loopback;
        public bool Testing { get { return testing; } }

        /// <summary>설정 화면의 마이크 시험 (켜면 내 목소리가 바로 들린다).</summary>
        public void SetMicTest(bool on)
        {
            testing = on;
            mic.MicOn = on;
            if (loopback != null)
            {
                loopback.transform.position = app.Rig.Cam != null ? app.Rig.Cam.transform.position : Vector3.zero;
                if (!on) { Destroy(loopback.gameObject); loopback = null; }
            }
        }

        /// <summary>방을 나가거나 타이틀로: 음소거 목록을 비운다 (대기실 → 게임으로는 자리 순서가 같아 그대로 둔다).</summary>
        public void ClearMutes() { Muted.Clear(); }

        /// <summary>진단용: 받은 목소리 묶음 수.</summary>
        public int VoicePacketsIn { get; private set; }

        public void OnVoice(int seat, byte[] data)
        {
            if (seat == MySeat || Muted.Contains(seat) || data.Length < 3) return;
            VoicePacketsIn++;
            VoicePlayer p;
            if (!players.TryGetValue(seat, out p) || p == null)
            {
                p = VoicePlayer.Create(transform, "seat" + seat);
                players[seat] = p;
            }
            p.Push(data, 3, data[2]);
        }

        public bool IsTalking(int seat)
        {
            if (seat == MySeat) return mic.Transmitting;
            VoicePlayer p;
            return players.TryGetValue(seat, out p) && p != null && p.Talking;
        }

        public void ToggleMute(int seat)
        {
            if (!Muted.Add(seat)) Muted.Remove(seat);
        }

        /// <summary>마이크 켜기/끄기 (M).</summary>
        public void ToggleMic()
        {
            mic.MicOn = !mic.MicOn;
            AudioManager.I.Play(mic.MicOn ? "ui_mic_on" : "ui_mic_off", 0.6f, 1f, Bus.Ui);
        }

        bool typing, wasOnline;
        /// <summary>글 입력 중에는 V·M 을 마이크로 쓰지 않는다.</summary>
        public void SetTyping(bool on) { typing = on; }

        void Update()
        {
            var s = SaveData.I.Settings;
            mic.Device = s.MicDevice;
            mic.Threshold = Mathf.Clamp(s.MicThreshold, 0.004f, 0.2f);
            var kb = Keyboard.current;
            bool social = Online || (app.Session != null && app.Session.Running);
            // '켜 두기' 방식이면 온라인 방에 들어갈 때 마이크를 켠다 (M 으로 끌 수 있다)
            bool onlineNow = Online;
            if (onlineNow && !wasOnline && s.MicMode == 1 && !Dev.AutoShots.Requested) mic.MicOn = true;
            wasOnline = onlineNow;
            if (kb != null && social && !typing)
            {
                mic.PushHeld = kb.vKey.isPressed;
                if (kb.mKey.wasPressedThisFrame) ToggleMic();
            }
            else mic.PushHeld = false;
            if (!social && mic.MicOn && !testing) mic.MicOn = false;
            if (testing && loopback != null) loopback.Volume = s.ChatVolume * s.Master;

            // 목소리는 그 사람 머리에서 나오고, 입이 움직인다
            foreach (var kv in players)
            {
                if (kv.Value == null) continue;
                var g = GuestAt(kv.Key);
                if (g != null) kv.Value.transform.position = g.Head.position;
                kv.Value.Volume = s.ChatVolume * s.Master;
                if (g != null) g.VoiceLevel = kv.Value.Level;
            }
            var me = GuestAt(MySeat);
            if (me != null) me.VoiceLevel = mic.Transmitting ? mic.Level : 0f;
        }

        /// <summary>새 판·대기실: 목소리 틀기를 비운다 (좌석이 바뀌므로).</summary>
        public void ResetSeats()
        {
            foreach (var p in players.Values) if (p != null) Destroy(p.gameObject);
            players.Clear();
        }

        // ═════════════════ 직접 넣은 물건 그림 ═════════════════

        public void SendItemImage(int item, byte[] jpg)
        {
            ItemImages.Store(item, jpg);
            if (Online) Room.SendSocial(Msg.ItemImage, new ItemImageMsg { Item = item, Data = Convert.ToBase64String(jpg) });
        }

        public void OnItemImage(ItemImageMsg m)
        {
            try { ItemImages.Store(m.Item, Convert.FromBase64String(m.Data)); }
            catch (Exception e) { Debug.LogWarning("[Social] bad image: " + e.Message); }
        }
    }
}
