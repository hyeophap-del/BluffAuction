using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace BluffAuction.Net
{
    /// <summary>
    /// 메시지 전송 계층. 방장(Host)은 여러 연결을 받고, 참가자(Client)는 방장 하나에 붙는다.
    /// 메시지는 바이트 묶음(길이 접두어). 받은 메시지는 Poll() 을 부른 스레드(메인)에서 이벤트로 낸다.
    /// TCP(같은 네트워크·포트 열기) 구현이며, Steam 릴레이 구현도 같은 인터페이스를 쓴다.
    /// </summary>
    public interface ITransport : IDisposable
    {
        bool IsHost { get; }
        bool Active { get; }
        event Action<int> PeerConnected;
        event Action<int> PeerDisconnected;
        event Action<int, byte[]> Received;
        /// <summary>참가자: 방장과의 연결이 끊김 (이유).</summary>
        event Action<string> Closed;
        void Send(int peer, byte[] data);
        void Broadcast(byte[] data);
        void Kick(int peer);
        void Poll();
    }

    public sealed class TcpTransport : ITransport
    {
        public const int DefaultPort = 27615;
        const int MaxMessage = 4 * 1024 * 1024;

        sealed class Conn
        {
            public int Id;
            public TcpClient Client;
            public NetworkStream Stream;
            public Thread Reader;
            public readonly object WriteLock = new object();
            public volatile bool Dead;
        }

        public bool IsHost { get; private set; }
        public bool Active { get; private set; }
        public event Action<int> PeerConnected;
        public event Action<int> PeerDisconnected;
        public event Action<int, byte[]> Received;
        public event Action<string> Closed;

        TcpListener listener;
        Thread acceptThread;
        readonly Dictionary<int, Conn> conns = new Dictionary<int, Conn>();
        readonly ConcurrentQueue<Action> inbox = new ConcurrentQueue<Action>();
        int nextId = 1;
        volatile bool stopping;

        public static TcpTransport Host(int port)
        {
            var t = new TcpTransport { IsHost = true };
            t.listener = new TcpListener(IPAddress.Any, port);
            t.listener.Start();
            t.Active = true;
            t.acceptThread = new Thread(t.AcceptLoop) { IsBackground = true, Name = "net-accept" };
            t.acceptThread.Start();
            return t;
        }

        /// <summary>방장에 연결한다 (막히면 예외). 방장의 연결 번호는 0.</summary>
        public static TcpTransport Connect(string host, int port, int timeoutMs = 5000)
        {
            var t = new TcpTransport { IsHost = false };
            var client = new TcpClient { NoDelay = true };
            var ar = client.BeginConnect(host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(timeoutMs))
            {
                client.Close();
                throw new TimeoutException("connect timeout");
            }
            client.EndConnect(ar);
            t.Active = true;
            t.Add(0, client);
            return t;
        }

        void AcceptLoop()
        {
            while (!stopping)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); }
                catch { if (stopping) return; continue; }
                c.NoDelay = true;
                int id;
                lock (conns) id = nextId++;
                Add(id, c);
                inbox.Enqueue(() => { if (PeerConnected != null) PeerConnected(id); });
            }
        }

        void Add(int id, TcpClient c)
        {
            var conn = new Conn { Id = id, Client = c, Stream = c.GetStream() };
            lock (conns) conns[id] = conn;
            conn.Reader = new Thread(() => ReadLoop(conn)) { IsBackground = true, Name = "net-read-" + id };
            conn.Reader.Start();
        }

        void ReadLoop(Conn c)
        {
            var header = new byte[4];
            try
            {
                while (!stopping && !c.Dead)
                {
                    ReadExact(c.Stream, header, 4);
                    int len = BitConverter.ToInt32(header, 0);
                    if (len < 0 || len > MaxMessage) throw new IOException("bad length " + len);
                    var data = new byte[len];
                    ReadExact(c.Stream, data, len);
                    int id = c.Id;
                    inbox.Enqueue(() => { if (Received != null) Received(id, data); });
                }
            }
            catch (Exception e)
            {
                if (!stopping) Drop(c, e.Message);
            }
        }

        static void ReadExact(Stream s, byte[] buf, int len)
        {
            int got = 0;
            while (got < len)
            {
                int n = s.Read(buf, got, len - got);
                if (n <= 0) throw new IOException("closed");
                got += n;
            }
        }

        void Drop(Conn c, string why)
        {
            if (c.Dead) return;
            c.Dead = true;
            try { c.Client.Close(); } catch { }
            lock (conns) conns.Remove(c.Id);
            int id = c.Id;
            if (IsHost) inbox.Enqueue(() => { if (PeerDisconnected != null) PeerDisconnected(id); });
            else inbox.Enqueue(() => { Active = false; if (Closed != null) Closed(why); });
        }

        public void Send(int peer, byte[] data)
        {
            Conn c;
            lock (conns) conns.TryGetValue(peer, out c);
            if (c == null || c.Dead) return;
            try
            {
                lock (c.WriteLock)
                {
                    var len = BitConverter.GetBytes(data.Length);
                    c.Stream.Write(len, 0, 4);
                    c.Stream.Write(data, 0, data.Length);
                }
            }
            catch (Exception e) { Drop(c, e.Message); }
        }

        public void Broadcast(byte[] data)
        {
            List<int> ids;
            lock (conns) ids = new List<int>(conns.Keys);
            foreach (var id in ids) Send(id, data);
        }

        public void Kick(int peer)
        {
            Conn c;
            lock (conns) conns.TryGetValue(peer, out c);
            if (c != null) Drop(c, "kicked");
        }

        public void Poll()
        {
            Action a;
            int guard = 0;
            while (guard++ < 2000 && inbox.TryDequeue(out a)) a();
        }

        public void Dispose()
        {
            stopping = true;
            Active = false;
            try { if (listener != null) listener.Stop(); } catch { }
            List<Conn> all;
            lock (conns) all = new List<Conn>(conns.Values);
            foreach (var c in all) { try { c.Client.Close(); } catch { } }
            lock (conns) conns.Clear();
        }

        /// <summary>이 컴퓨터의 내부망 IPv4 주소들 (방 만들기 화면에 보여 준다).</summary>
        public static List<string> LocalAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip)) list.Add(ip.ToString());
            }
            catch { }
            return list;
        }

        public static byte[] Utf8(string s) { return Encoding.UTF8.GetBytes(s); }
        public static string Utf8(byte[] b) { return Encoding.UTF8.GetString(b); }
    }
}
