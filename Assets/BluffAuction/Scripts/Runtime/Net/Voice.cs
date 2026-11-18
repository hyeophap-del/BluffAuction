using System;
using System.Collections.Generic;
using UnityEngine;

namespace BluffAuction.Net
{
    /// <summary>
    /// 목소리 압축 (IMA ADPCM, 16kHz 모노, 4비트): 20ms(320 표본) 한 조각이 163바이트.
    /// 조각마다 머리(예측값·단계)가 있어 하나를 잃어도 다음 조각은 바로 풀린다.
    /// </summary>
    public static class Adpcm
    {
        public const int Rate = 16000;
        public const int FrameSamples = 320;
        public const int FrameBytes = 3 + FrameSamples / 2;

        static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
        static readonly int[] StepTable =
        {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
            157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552,
            1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487,
            12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
        };

        public struct State { public int Predictor; public int Index; }

        /// <summary>표본 320개를 조각 하나로 (state 는 다음 조각으로 이어진다).</summary>
        public static void Encode(float[] pcm, int offset, byte[] dst, int dstOffset, ref State st)
        {
            dst[dstOffset] = (byte)(st.Predictor & 0xff);
            dst[dstOffset + 1] = (byte)((st.Predictor >> 8) & 0xff);
            dst[dstOffset + 2] = (byte)st.Index;
            int pred = st.Predictor, index = st.Index;
            for (int i = 0; i < FrameSamples; i++)
            {
                int sample = Mathf.Clamp((int)(pcm[offset + i] * 32767f), -32768, 32767);
                int step = StepTable[index];
                int diff = sample - pred;
                int code = 0;
                if (diff < 0) { code = 8; diff = -diff; }
                int delta = step >> 3;
                if (diff >= step) { code |= 4; diff -= step; delta += step; }
                step >>= 1;
                if (diff >= step) { code |= 2; diff -= step; delta += step; }
                step >>= 1;
                if (diff >= step) { code |= 1; delta += step; }
                pred += (code & 8) != 0 ? -delta : delta;
                pred = Mathf.Clamp(pred, -32768, 32767);
                index = Mathf.Clamp(index + IndexTable[code], 0, 88);
                int b = dstOffset + 3 + (i >> 1);
                if ((i & 1) == 0) dst[b] = (byte)code;
                else dst[b] |= (byte)(code << 4);
            }
            st.Predictor = pred;
            st.Index = index;
        }

        public static void Decode(byte[] src, int offset, float[] pcm, int pcmOffset)
        {
            int pred = (short)(src[offset] | (src[offset + 1] << 8));
            int index = Mathf.Clamp(src[offset + 2], 0, 88);
            for (int i = 0; i < FrameSamples; i++)
            {
                int b = src[offset + 3 + (i >> 1)];
                int code = (i & 1) == 0 ? b & 0x0f : (b >> 4) & 0x0f;
                int step = StepTable[index];
                int delta = step >> 3;
                if ((code & 4) != 0) delta += step;
                if ((code & 2) != 0) delta += step >> 1;
                if ((code & 1) != 0) delta += step >> 2;
                pred += (code & 8) != 0 ? -delta : delta;
                pred = Mathf.Clamp(pred, -32768, 32767);
                index = Mathf.Clamp(index + IndexTable[code], 0, 88);
                pcm[pcmOffset + i] = pred / 32768f;
            }
        }
    }

    public enum MicMode { PushToTalk = 0, Open = 1 }

    /// <summary>
    /// 마이크 받기: 20ms 조각마다 크기를 재고, 말할 때(누르고 말하기 또는 켜 둔 마이크 + 소리 감지)만 압축해 내보낸다.
    /// 자동 시험에서는 진짜 마이크 대신 시험용 소리를 만든다 (방 소리를 녹음하지 않게).
    /// </summary>
    public sealed class VoiceCapture : MonoBehaviour
    {
        public static VoiceCapture I { get; private set; }
        /// <summary>압축된 조각 묶음 (보통 2조각 = 40ms).</summary>
        public event Action<byte[], int> Packet;
        public bool MicOn;          // 켜 둔 마이크 (M)
        public bool PushHeld;       // 누르고 말하기 (V)
        public float Threshold = 0.02f;
        public float Level { get; private set; }
        public bool Transmitting { get; private set; }
        public bool HasDevice { get; private set; }
        public bool Synthetic;      // 시험용 소리
        public string Device;

        AudioClip clip;
        string openDevice;
        int lastPos;
        readonly float[] chunk = new float[Adpcm.Rate];
        readonly List<float> pending = new List<float>(4096);
        readonly float[] frame = new float[Adpcm.FrameSamples];
        Adpcm.State state;
        float hangover;
        readonly byte[] packetBuf = new byte[Adpcm.FrameBytes * 2];
        int packetFrames;
        float synthT;

        public static VoiceCapture Create(Transform parent)
        {
            var go = new GameObject("VoiceCapture");
            go.transform.SetParent(parent, false);
            I = go.AddComponent<VoiceCapture>();
            return I;
        }

        public static string[] Devices { get { try { return Microphone.devices; } catch { return new string[0]; } } }

        bool Wanted { get { return MicOn || PushHeld; } }

        void Open()
        {
            if (Synthetic) { HasDevice = true; return; }
            try
            {
                var devs = Microphone.devices;
                HasDevice = devs.Length > 0;
                if (!HasDevice) return;
                string dev = !string.IsNullOrEmpty(Device) && Array.IndexOf(devs, Device) >= 0 ? Device : null;
                clip = Microphone.Start(dev, true, 1, Adpcm.Rate);
                openDevice = dev;
                lastPos = 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Voice] mic failed: " + e.Message);
                HasDevice = false;
            }
        }

        void Close()
        {
            if (clip != null)
            {
                try { Microphone.End(openDevice); } catch { }
                clip = null;
            }
            pending.Clear();
        }

        void Update()
        {
            // 쓸 때만 마이크를 연다 (켜 두지 않으면 녹음하지 않는다)
            if (Wanted && clip == null && !Synthetic) Open();
            if (!Wanted && clip != null && hangover <= 0) Close();
            if (Synthetic && Wanted) HasDevice = true;
            ReadSamples();
            while (pending.Count >= Adpcm.FrameSamples)
            {
                pending.CopyTo(0, frame, 0, Adpcm.FrameSamples);
                pending.RemoveRange(0, Adpcm.FrameSamples);
                ProcessFrame();
            }
            if (!Wanted && hangover <= 0) { Level = Mathf.MoveTowards(Level, 0, Time.unscaledDeltaTime * 4f); Transmitting = false; }
        }

        void ReadSamples()
        {
            if (Synthetic)
            {
                if (!Wanted) return;
                // 시험용: 사람 목소리처럼 높낮이가 바뀌는 웅얼거림
                int n = Mathf.RoundToInt(Time.unscaledDeltaTime * Adpcm.Rate);
                for (int i = 0; i < n; i++)
                {
                    synthT += 1f / Adpcm.Rate;
                    float f = 160f + 50f * Mathf.Sin(synthT * 3f);
                    float env = Mathf.Clamp01(Mathf.Sin(synthT * 5.3f) * 0.8f + 0.4f);
                    pending.Add(Mathf.Sin(synthT * f * Mathf.PI * 2f) * 0.25f * env);
                }
                return;
            }
            if (clip == null) return;
            int pos = Microphone.GetPosition(openDevice);
            if (pos < 0 || pos == lastPos) return;
            int len = clip.samples;
            int count = (pos - lastPos + len) % len;
            if (count <= 0 || count > len) { lastPos = pos; return; }
            if (lastPos + count <= len)
            {
                var buf = new float[count];
                clip.GetData(buf, lastPos);
                pending.AddRange(buf);
            }
            else
            {
                int first = len - lastPos;
                var a = new float[first];
                clip.GetData(a, lastPos);
                pending.AddRange(a);
                var b = new float[count - first];
                clip.GetData(b, 0);
                pending.AddRange(b);
            }
            lastPos = pos;
        }

        void ProcessFrame()
        {
            float sum = 0;
            for (int i = 0; i < frame.Length; i++) sum += frame[i] * frame[i];
            float rms = Mathf.Sqrt(sum / frame.Length);
            Level = Mathf.Max(rms * 4f, Level * 0.85f);
            bool voice = PushHeld || (MicOn && rms > Threshold);
            if (voice) hangover = 0.35f;
            else hangover -= Adpcm.FrameSamples / (float)Adpcm.Rate;
            Transmitting = hangover > 0 && Wanted;
            if (!Transmitting)
            {
                state = default(Adpcm.State);
                packetFrames = 0;
                return;
            }
            Adpcm.Encode(frame, 0, packetBuf, packetFrames * Adpcm.FrameBytes, ref state);
            packetFrames++;
            if (packetFrames >= 2)
            {
                if (Packet != null) Packet(packetBuf, packetFrames);
                packetFrames = 0;
            }
        }

        void OnDisable() { Close(); }
    }

    /// <summary>
    /// 한 사람의 목소리 틀기: 받은 조각을 풀어 작은 완충(약 60ms)을 두고 3D 소리로 낸다.
    /// 오래 밀리면 앞을 버려 늦어지지 않게 한다. 오디오 스레드가 읽으므로 잠근다.
    /// </summary>
    public sealed class VoicePlayer : MonoBehaviour
    {
        const int Capacity = Adpcm.Rate; // 1초
        readonly float[] ring = new float[Capacity];
        int read, write, count;
        bool playing;
        readonly object gate = new object();
        readonly float[] decoded = new float[Adpcm.FrameSamples];
        AudioSource src;
        public float Level { get; private set; }
        float lastPacket = -10f;
        public bool Talking { get { return Time.unscaledTime - lastPacket < 0.3f; } }

        public static VoicePlayer Create(Transform parent, string name)
        {
            var go = new GameObject("Voice_" + name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<VoicePlayer>();
            p.src = go.AddComponent<AudioSource>();
            p.src.clip = AudioClip.Create("voice_" + name, Adpcm.Rate, 1, Adpcm.Rate, true, p.OnRead);
            p.src.loop = true;
            p.src.spatialBlend = 0.8f;
            p.src.minDistance = 1.2f;
            p.src.maxDistance = 9f;
            p.src.rolloffMode = AudioRolloffMode.Linear;
            p.src.dopplerLevel = 0f;
            p.src.Play();
            return p;
        }

        public float Volume { set { if (src != null) src.volume = value; } }

        public void Push(byte[] data, int offset, int frames)
        {
            lastPacket = Time.unscaledTime;
            float peak = 0;
            for (int f = 0; f < frames; f++)
            {
                if (offset + (f + 1) * Adpcm.FrameBytes > data.Length) break;
                Adpcm.Decode(data, offset + f * Adpcm.FrameBytes, decoded, 0);
                float sum = 0;
                for (int i = 0; i < decoded.Length; i++) sum += decoded[i] * decoded[i];
                peak = Mathf.Max(peak, Mathf.Sqrt(sum / decoded.Length));
                lock (gate)
                {
                    // 너무 밀렸으면 (0.3초 넘게) 앞을 버린다
                    if (count + decoded.Length > Adpcm.Rate * 3 / 10)
                    {
                        int drop = count + decoded.Length - Adpcm.Rate / 10;
                        read = (read + drop) % Capacity;
                        count -= drop;
                    }
                    for (int i = 0; i < decoded.Length; i++)
                    {
                        ring[write] = decoded[i];
                        write = (write + 1) % Capacity;
                    }
                    count += decoded.Length;
                }
            }
            Level = Mathf.Max(Level, Mathf.Clamp01(peak * 5f));
        }

        void OnRead(float[] data)
        {
            lock (gate)
            {
                if (!playing && count >= Adpcm.FrameSamples * 3) playing = true;
                for (int i = 0; i < data.Length; i++)
                {
                    if (playing && count > 0)
                    {
                        data[i] = ring[read];
                        read = (read + 1) % Capacity;
                        count--;
                    }
                    else
                    {
                        data[i] = 0f;
                        playing = false;
                    }
                }
            }
        }

        void Update()
        {
            Level = Mathf.MoveTowards(Level, 0f, Time.unscaledDeltaTime * 3f);
        }
    }
}
