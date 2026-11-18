using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace BluffAuction.Net
{
    /// <summary>
    /// 같은 네트워크의 방 찾기: 방장은 1초마다 UDP 방송으로 "BLUFF|버전|TCP포트|정보" 를 알리고,
    /// 참가 화면은 그 방송을 듣고 방 목록을 만든다.
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        public const int Port = 27616;
        const string Tag = "BLUFF";

        public sealed class Room
        {
            public string Address;
            public int Port;
            public string Info;
            public float SeenAt;
        }

        UdpClient udp;
        int tcpPort;
        Func<string> info;
        float nextSend;
        readonly bool advertise;
        public readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();

        LanDiscovery(bool advertise) { this.advertise = advertise; }

        public static LanDiscovery Advertise(int tcpPort, Func<string> info)
        {
            var d = new LanDiscovery(true) { tcpPort = tcpPort, info = info };
            try
            {
                d.udp = new UdpClient();
                d.udp.EnableBroadcast = true;
            }
            catch (Exception e) { Debug.LogWarning("[LAN] advertise failed: " + e.Message); }
            return d;
        }

        public static LanDiscovery Listen()
        {
            var d = new LanDiscovery(false);
            try
            {
                d.udp = new UdpClient();
                d.udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                d.udp.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
                d.udp.Client.Blocking = false;
            }
            catch (Exception e) { Debug.LogWarning("[LAN] listen failed: " + e.Message); d.udp = null; }
            return d;
        }

        public void Tick()
        {
            if (udp == null) return;
            if (advertise)
            {
                if (Time.unscaledTime < nextSend) return;
                nextSend = Time.unscaledTime + 1f;
                try
                {
                    var msg = Encoding.UTF8.GetBytes(Tag + "|" + Wire.GameVersion + "|" + tcpPort + "|" + (info != null ? info() : ""));
                    udp.Send(msg, msg.Length, new IPEndPoint(IPAddress.Broadcast, Port));
                }
                catch { }
                return;
            }
            try
            {
                while (udp.Available > 0)
                {
                    var from = new IPEndPoint(IPAddress.Any, 0);
                    var data = udp.Receive(ref from);
                    var parts = Encoding.UTF8.GetString(data).Split(new[] { '|' }, 4);
                    if (parts.Length < 4 || parts[0] != Tag || parts[1] != Wire.GameVersion) continue;
                    int port;
                    if (!int.TryParse(parts[2], out port)) continue;
                    string key = from.Address + ":" + port;
                    Room r;
                    if (!Rooms.TryGetValue(key, out r)) Rooms[key] = r = new Room { Address = from.Address.ToString(), Port = port };
                    r.Info = parts[3];
                    r.SeenAt = Time.unscaledTime;
                }
            }
            catch { }
            // 5초 넘게 소식 없는 방은 지운다
            var stale = new List<string>();
            foreach (var kv in Rooms) if (Time.unscaledTime - kv.Value.SeenAt > 5f) stale.Add(kv.Key);
            foreach (var k in stale) Rooms.Remove(k);
        }

        public void Dispose()
        {
            try { if (udp != null) udp.Close(); } catch { }
            udp = null;
        }
    }
}
