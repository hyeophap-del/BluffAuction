using System;
using System.Collections.Generic;
using System.Text;
using BluffAuction.Core;
using BluffAuction.Game;
using UnityEngine;

namespace BluffAuction.Net
{
    /// <summary>메시지 종류 (첫 바이트). 나머지는 JSON.</summary>
    public enum Msg : byte
    {
        Hello = 1,      // 참가자 → 방장: 이름·가면·버전
        Welcome = 2,    // 방장 → 참가자: 연결 번호
        Lobby = 3,      // 방장 → 모두: 대기실 상태
        Start = 4,      // 방장 → 참가자: 게임 시작 (내 좌석·좌석 목록)
        View = 5,       // 방장 → 참가자: 내 좌석 화면 상태 (GameView)
        Event = 6,      // 방장 → 참가자: 걸러진 사건
        Cmd = 7,        // 참가자 → 방장: 명령 (입찰 등)
        CmdResult = 8,  // 방장 → 참가자: 명령 거절 이유
        Bye = 9,        // 방장 → 참가자: 내보냄 / 방 닫힘
        Ready = 10,     // 참가자 → 방장: 준비
        ToLobby = 11,   // 방장 → 모두: 대기실로 돌아감
        // ── 어울리기 (방장이 보낸 사람 좌석을 채워 다른 사람에게 그대로 전한다)
        Chat = 12,      // 채팅 한 줄
        Emote = 13,     // 감정 표현
        Throw = 14,     // 물건 던지기
        Voice = 15,     // 목소리 (JSON 이 아닌 바이트: [종류][좌석][조각 수][조각들])
        ItemImage = 16, // 직접 쓴 물건의 그림 (JPG, base64)
        Melee = 17      // 근접 무기 휘두르기
    }

    [Serializable] public sealed class MeleeMsg { public int Seat; public int Kind; public int Target = -1; }

    [Serializable] public sealed class ChatMsg { public int Seat; public string Text = ""; }
    [Serializable] public sealed class EmoteMsg { public int Seat; public int Id; public int Target = -1; }

    /// <summary>던지기: 좌석마다 탁자 배치가 다르게 보이므로 (내 자리가 늘 카메라 쪽) 좌석 기준 방향으로 보낸다.</summary>
    [Serializable]
    public sealed class ThrowMsg
    {
        public int Seat;
        public int Kind;
        public int Target = -1;   // 겨눈 사람 (없으면 -1 → Yaw/Pitch 방향)
        public float Yaw;         // 던진 사람 좌석 정면 기준 (도)
        public float Pitch;
        public float Power = 1f;
        public int Seed;
    }

    [Serializable] public sealed class ItemImageMsg { public int Item; public string Data = ""; }

    public enum CmdKind
    {
        Register = 1,
        Bid = 2,
        Ability = 3,
        PriceDrop = 4,
        Loan = 5,
        Say = 6,
        Skip = 7,
        EndVote = 8
    }

    [Serializable] public sealed class HelloMsg { public string Name; public int Look; public string LookJson = ""; public string Version; }
    [Serializable] public sealed class WelcomeMsg { public int PeerId; }
    [Serializable] public sealed class ByeMsg { public string Reason; }
    [Serializable] public sealed class ReadyMsg { public bool Ready; }

    [Serializable]
    public sealed class LobbySlot
    {
        public string Name = "";
        public int Look;
        public string LookJson = "";
        public bool IsAI;
        public string Profile = "";
        public bool Ready;
        public int PeerId = -1;     // -1 AI, 0 방장
        public bool IsHost;
    }

    [Serializable]
    public sealed class LobbyMsg
    {
        public List<LobbySlot> Slots = new List<LobbySlot>();
        public LastSetup Rules = new LastSetup();
        public string RoomName = "";
    }

    [Serializable]
    public sealed class StartMsg
    {
        public int Seat;
        public List<SeatInfo> Seats = new List<SeatInfo>();
    }

    [Serializable]
    public sealed class CmdMsg
    {
        public int Kind;
        public int A;
        public string Text;
        public Registration Reg;
    }

    [Serializable] public sealed class CmdResultMsg { public int Kind; public int Code; }

    /// <summary>사건 묶음 (화면 상태 하나 뒤에 이어지는 사건들).</summary>
    [Serializable]
    public sealed class EventsMsg
    {
        public List<GameEvent> Events = new List<GameEvent>();
    }

    public static class Wire
    {
        public const string ProtocolVersion = "2";

        public static byte[] Pack(Msg type, object payload)
        {
            string json = payload != null ? JsonUtility.ToJson(payload) : "{}";
            var body = Encoding.UTF8.GetBytes(json);
            var data = new byte[body.Length + 1];
            data[0] = (byte)type;
            Buffer.BlockCopy(body, 0, data, 1, body.Length);
            return data;
        }

        public static Msg TypeOf(byte[] data) { return data != null && data.Length > 0 ? (Msg)data[0] : 0; }

        /// <summary>목소리: [종류][좌석][조각 수][조각들].</summary>
        public static byte[] PackVoice(int seat, byte[] frames, int frameCount)
        {
            int len = frameCount * Adpcm.FrameBytes;
            var data = new byte[3 + len];
            data[0] = (byte)Msg.Voice;
            data[1] = (byte)Mathf.Clamp(seat, 0, 255);
            data[2] = (byte)frameCount;
            Buffer.BlockCopy(frames, 0, data, 3, len);
            return data;
        }

        public static T Unpack<T>(byte[] data)
        {
            string json = Encoding.UTF8.GetString(data, 1, data.Length - 1);
            return JsonUtility.FromJson<T>(json);
        }

        public static string GameVersion { get { return Application.version + "/" + ProtocolVersion; } }
    }
}
