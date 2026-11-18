using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace BluffAuction.Net
{
    /// <summary>
    /// Steam 네트워킹 소켓 전송 계층. 방장은 P2P 듣기 소켓을 열고, 참가자는 방장의 SteamID 로 붙는다.
    /// Steam 릴레이를 거치므로 포트를 열 필요가 없고 IP 도 서로 모른다. 메시지는 모두 신뢰·순서 보장으로 보낸다.
    /// 한 메시지는 최대 512KB 라 그보다 큰 것(직접 넣은 그림)은 조각내 보내고 받는 쪽에서 다시 붙인다.
    /// 시험용으로 같은 코드를 IP 연결(127.0.0.1)로도 쓸 수 있다 (같은 계정끼리는 P2P 로 붙을 수 없다).
    /// </summary>
    public sealed class SteamTransport : ITransport
    {
        /// <summary>연결 번호를 TCP 와 겹치지 않게 (MultiTransport 가 번호로 계층을 가른다).</summary>
        public const int IdBase = 100000;
        public const int TestPort = 27617;
        const int MaxMessage = 4 * 1024 * 1024;
        const int Chunk = 500 * 1024;
        const byte Whole = 0, Part = 1, Last = 2;

        public bool IsHost { get; private set; }
        public bool Active { get; private set; }
        public event Action<int> PeerConnected;
        public event Action<int> PeerDisconnected;
        public event Action<int, byte[]> Received;
        public event Action<string> Closed;

        readonly Dictionary<int, MemoryStream> partial = new Dictionary<int, MemoryStream>();

#if STEAMWORKS_NET
        HSteamListenSocket listenP2P = HSteamListenSocket.Invalid, listenIP = HSteamListenSocket.Invalid;
        HSteamNetPollGroup group = HSteamNetPollGroup.Invalid;
        HSteamNetConnection server = HSteamNetConnection.Invalid;
        bool serverUp;
        readonly List<byte[]> pending = new List<byte[]>();
        readonly Dictionary<int, HSteamNetConnection> byId = new Dictionary<int, HSteamNetConnection>();
        readonly Dictionary<HSteamNetConnection, int> idOf = new Dictionary<HSteamNetConnection, int>();
        readonly IntPtr[] inbox = new IntPtr[64];
        Callback<SteamNetConnectionStatusChangedCallback_t> statusCb;
        int nextId = IdBase;
        /// <summary>내보낸 연결의 끊김 알림은 다음 Poll 에서 (TCP 처럼 — 부른 쪽이 목록을 고치는 중일 수 있다).</summary>
        readonly List<int> kicked = new List<int>();
#endif

        /// <summary>방장: Steam 친구가 P2P 로 들어올 수 있게 연다. testIp 면 시험용 IP 포트도 연다.</summary>
        public static SteamTransport Host(bool testIp)
        {
#if STEAMWORKS_NET
            if (!Platform.Steam.Ready) throw new InvalidOperationException("steam off");
            var t = new SteamTransport { IsHost = true };
            t.statusCb = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(t.OnStatus);
            t.group = SteamNetworkingSockets.CreatePollGroup();
            t.listenP2P = SteamNetworkingSockets.CreateListenSocketP2P(0, 0, null);
            if (testIp)
            {
                var addr = new SteamNetworkingIPAddr();
                addr.Clear();
                addr.m_port = TestPort;
                t.listenIP = SteamNetworkingSockets.CreateListenSocketIP(ref addr, 0, null);
            }
            if (t.listenP2P == HSteamListenSocket.Invalid) { t.Dispose(); throw new IOException("listen p2p failed"); }
            t.Active = true;
            return t;
#else
            throw new InvalidOperationException("STEAMWORKS_NET off");
#endif
        }

        /// <summary>참가자: 방장 SteamID 로 붙는다. 연결은 비동기라 붙기 전에 보낸 메시지는 모아 두었다가 보낸다.</summary>
        public static SteamTransport Connect(ulong hostSteamId)
        {
#if STEAMWORKS_NET
            if (!Platform.Steam.Ready) throw new InvalidOperationException("steam off");
            var t = new SteamTransport { IsHost = false };
            t.statusCb = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(t.OnStatus);
            var who = new SteamNetworkingIdentity();
            who.SetSteamID(new CSteamID(hostSteamId));
            t.server = SteamNetworkingSockets.ConnectP2P(ref who, 0, 0, null);
            if (t.server == HSteamNetConnection.Invalid) { t.Dispose(); throw new IOException("connect failed"); }
            t.Active = true;
            return t;
#else
            throw new InvalidOperationException("STEAMWORKS_NET off");
#endif
        }

        /// <summary>시험용: Steam 소켓으로 IP 주소("127.0.0.1:27617")에 붙는다.</summary>
        public static SteamTransport ConnectIp(string address)
        {
#if STEAMWORKS_NET
            if (!Platform.Steam.Ready) throw new InvalidOperationException("steam off");
            var t = new SteamTransport { IsHost = false };
            t.statusCb = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(t.OnStatus);
            var addr = new SteamNetworkingIPAddr();
            addr.Clear();
            if (!addr.ParseString(address)) { t.Dispose(); throw new ArgumentException("bad address " + address); }
            t.server = SteamNetworkingSockets.ConnectByIPAddress(ref addr, 0, null);
            if (t.server == HSteamNetConnection.Invalid) { t.Dispose(); throw new IOException("connect failed"); }
            t.Active = true;
            return t;
#else
            throw new InvalidOperationException("STEAMWORKS_NET off");
#endif
        }

#if STEAMWORKS_NET
        void OnStatus(SteamNetConnectionStatusChangedCallback_t e)
        {
            var conn = e.m_hConn;
            var state = e.m_info.m_eState;
            if (IsHost)
            {
                bool mine = idOf.ContainsKey(conn);
                if (!mine && e.m_info.m_hListenSocket != listenP2P && (listenIP == HSteamListenSocket.Invalid || e.m_info.m_hListenSocket != listenIP)) return;
                switch (state)
                {
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                        if (mine || !Active) break;
                        if (SteamNetworkingSockets.AcceptConnection(conn) != EResult.k_EResultOK)
                        {
                            SteamNetworkingSockets.CloseConnection(conn, 0, "accept failed", false);
                            break;
                        }
                        int id = nextId++;
                        byId[id] = conn;
                        idOf[conn] = id;
                        SteamNetworkingSockets.SetConnectionPollGroup(conn, group);
                        break;
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                        if (mine && PeerConnected != null) PeerConnected(idOf[conn]);
                        break;
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                        if (mine) Forget(conn, true);
                        else SteamNetworkingSockets.CloseConnection(conn, 0, null, false);
                        break;
                }
            }
            else
            {
                if (conn != server) return;
                switch (state)
                {
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                        serverUp = true;
                        foreach (var p in pending) SendFramed(server, p);
                        pending.Clear();
                        break;
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
                    case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                        string why = e.m_info.m_szEndDebug;
                        SteamNetworkingSockets.CloseConnection(server, 0, null, false);
                        server = HSteamNetConnection.Invalid;
                        serverUp = false;
                        Active = false;
                        if (Closed != null) Closed(string.IsNullOrEmpty(why) ? state.ToString() : why);
                        break;
                }
            }
        }

        void Forget(HSteamNetConnection conn, bool notify)
        {
            int id;
            if (!idOf.TryGetValue(conn, out id)) return;
            idOf.Remove(conn);
            byId.Remove(id);
            partial.Remove(id);
            SteamNetworkingSockets.CloseConnection(conn, 0, null, false);
            if (notify && PeerDisconnected != null) PeerDisconnected(id);
        }

        void SendFramed(HSteamNetConnection c, byte[] data)
        {
            if (data.Length + 1 <= Chunk) { SendRaw(c, Whole, data, 0, data.Length); return; }
            for (int off = 0; off < data.Length; off += Chunk)
            {
                int n = Math.Min(Chunk, data.Length - off);
                SendRaw(c, off + n >= data.Length ? Last : Part, data, off, n);
            }
        }

        static void SendRaw(HSteamNetConnection c, byte kind, byte[] data, int off, int n)
        {
            IntPtr p = Marshal.AllocHGlobal(n + 1);
            try
            {
                Marshal.WriteByte(p, kind);
                Marshal.Copy(data, off, IntPtr.Add(p, 1), n);
                long num;
                var r = SteamNetworkingSockets.SendMessageToConnection(c, p, (uint)(n + 1), Constants.k_nSteamNetworkingSend_Reliable, out num);
                if (r != EResult.k_EResultOK) Platform.Steam.Log("send failed " + r);
            }
            finally { Marshal.FreeHGlobal(p); }
        }
#endif

        void Deliver(int id, byte[] buf)
        {
            if (buf.Length < 1) return;
            byte kind = buf[0];
            if (kind == Whole)
            {
                var data = new byte[buf.Length - 1];
                Buffer.BlockCopy(buf, 1, data, 0, data.Length);
                if (Received != null) Received(id, data);
                return;
            }
            MemoryStream ms;
            if (!partial.TryGetValue(id, out ms)) partial[id] = ms = new MemoryStream();
            ms.Write(buf, 1, buf.Length - 1);
            if (ms.Length > MaxMessage) { partial.Remove(id); return; }
            if (kind == Last)
            {
                partial.Remove(id);
                if (Received != null) Received(id, ms.ToArray());
            }
        }

        public void Send(int peer, byte[] data)
        {
#if STEAMWORKS_NET
            if (!Active || !Platform.Steam.Ready) return;
            if (IsHost)
            {
                HSteamNetConnection c;
                if (byId.TryGetValue(peer, out c)) SendFramed(c, data);
            }
            else if (serverUp) SendFramed(server, data);
            else pending.Add(data);
#endif
        }

        public void Broadcast(byte[] data)
        {
#if STEAMWORKS_NET
            if (!Active || !Platform.Steam.Ready) return;
            if (!IsHost) { Send(0, data); return; }
            foreach (var c in byId.Values) SendFramed(c, data);
#endif
        }

        public void Kick(int peer)
        {
#if STEAMWORKS_NET
            HSteamNetConnection c;
            if (!Platform.Steam.Ready || !byId.TryGetValue(peer, out c)) return;
            // 방금 보낸 작별 메시지가 나가도록 잠깐 남겨 두고 닫는다
            SteamNetworkingSockets.CloseConnection(c, 0, "kicked", true);
            idOf.Remove(c);
            byId.Remove(peer);
            partial.Remove(peer);
            kicked.Add(peer);
#endif
        }

        public void Poll()
        {
#if STEAMWORKS_NET
            if (!Active || !Platform.Steam.Ready) return;
            if (kicked.Count > 0)
            {
                var gone = kicked.ToArray();
                kicked.Clear();
                foreach (var id in gone) if (PeerDisconnected != null) PeerDisconnected(id);
            }
            for (int guard = 0; guard < 32; guard++)
            {
                int n = IsHost
                    ? SteamNetworkingSockets.ReceiveMessagesOnPollGroup(group, inbox, inbox.Length)
                    : (server != HSteamNetConnection.Invalid ? SteamNetworkingSockets.ReceiveMessagesOnConnection(server, inbox, inbox.Length) : 0);
                if (n <= 0) break;
                for (int i = 0; i < n; i++)
                {
                    var m = SteamNetworkingMessage_t.FromIntPtr(inbox[i]);
                    var buf = new byte[m.m_cbSize];
                    Marshal.Copy(m.m_pData, buf, 0, m.m_cbSize);
                    var conn = m.m_conn;
                    SteamNetworkingMessage_t.Release(inbox[i]);
                    int id = 0;
                    if (IsHost && !idOf.TryGetValue(conn, out id)) continue;
                    Deliver(id, buf);
                }
            }
#endif
        }

        public void Dispose()
        {
            Active = false;
#if STEAMWORKS_NET
            if (!Platform.Steam.Ready) return;
            foreach (var c in byId.Values) SteamNetworkingSockets.CloseConnection(c, 0, "closed", true);
            byId.Clear();
            idOf.Clear();
            if (server != HSteamNetConnection.Invalid) SteamNetworkingSockets.CloseConnection(server, 0, "closed", true);
            server = HSteamNetConnection.Invalid;
            if (listenP2P != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(listenP2P);
            if (listenIP != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(listenIP);
            listenP2P = listenIP = HSteamListenSocket.Invalid;
            if (group != HSteamNetPollGroup.Invalid) SteamNetworkingSockets.DestroyPollGroup(group);
            group = HSteamNetPollGroup.Invalid;
            if (statusCb != null) statusCb.Dispose();
            statusCb = null;
#endif
            partial.Clear();
        }
    }

    /// <summary>
    /// 방장이 여러 전송 계층(같은 네트워크 TCP + Steam 릴레이)으로 한꺼번에 받는다.
    /// 연결 번호는 계층마다 나뉘어 있어(TCP 1~, Steam 100000~) 번호만 보고 어느 계층인지 안다.
    /// </summary>
    public sealed class MultiTransport : ITransport
    {
        readonly List<ITransport> parts = new List<ITransport>();
        readonly Dictionary<int, ITransport> owner = new Dictionary<int, ITransport>();

        public bool IsHost { get { return true; } }
        public bool Active { get { foreach (var p in parts) if (p.Active) return true; return false; } }
        public event Action<int> PeerConnected;
        public event Action<int> PeerDisconnected;
        public event Action<int, byte[]> Received;
        public event Action<string> Closed;

        public MultiTransport(params ITransport[] list)
        {
            foreach (var p in list)
            {
                if (p == null) continue;
                var part = p;
                parts.Add(part);
                part.PeerConnected += id => { owner[id] = part; if (PeerConnected != null) PeerConnected(id); };
                part.PeerDisconnected += id => { owner.Remove(id); if (PeerDisconnected != null) PeerDisconnected(id); };
                // 연결 알림보다 메시지가 먼저 올 수도 있다
                part.Received += (id, data) => { owner[id] = part; if (Received != null) Received(id, data); };
                part.Closed += why => { if (Closed != null) Closed(why); };
            }
        }

        public void Send(int peer, byte[] data)
        {
            ITransport p;
            if (owner.TryGetValue(peer, out p)) p.Send(peer, data);
        }

        public void Broadcast(byte[] data) { foreach (var p in parts) p.Broadcast(data); }

        public void Kick(int peer)
        {
            ITransport p;
            if (owner.TryGetValue(peer, out p)) p.Kick(peer);
        }

        public void Poll() { foreach (var p in parts) p.Poll(); }

        public void Dispose() { foreach (var p in parts) p.Dispose(); }
    }
}
