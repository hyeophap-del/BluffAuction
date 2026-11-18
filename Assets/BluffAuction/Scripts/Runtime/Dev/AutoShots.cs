using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace BluffAuction.Dev
{
    /// <summary>
    /// 개발용 자동 촬영: 명령줄에 -autoshots &lt;폴더&gt; 가 있으면 정해진 순서로 화면을 찍어 PNG 로 남기고 끝낸다.
    /// 빌드한 게임을 사람이 보지 않아도 화면을 확인하려고 쓴다.
    /// </summary>
    public static class AutoShots
    {
        public static string Dir { get; private set; }
        public static bool Requested { get { return Dir != null; } }
        public static string Scenario { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Parse()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-autoshots") Dir = args[i + 1];
                if (args[i] == "-scenario") Scenario = args[i + 1];
            }
            if (Dir != null)
            {
                Directory.CreateDirectory(Dir);
                // 시험 중에는 경고·오류도 같은 기록에 남긴다
                Application.logMessageReceived += (msg, stack, type) =>
                {
                    if (type == LogType.Log) return;
                    Log("[" + type + "] " + msg + (type == LogType.Exception ? "\n" + stack : ""));
                };
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartStats()
        {
            if (Dir == null) return;
            var go = new GameObject("FrameStats");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<FrameStats>();
        }

        static int count;
        /// <summary>촬영 직후 몇 프레임은 화면 저장 때문에 느리므로 성능 기록에서 뺀다.</summary>
        internal static int SkipFrames;

        /// <summary>이번 프레임이 끝난 뒤 화면을 찍는다.</summary>
        public static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            if (Dir == null) yield break;
            SkipFrames = 10;
            count++;
            string path = Path.Combine(Dir, count.ToString("00") + "_" + name + ".png");
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex);
            Log("shot " + path);
        }

        public static void Log(string line)
        {
            if (Dir == null) return;
            File.AppendAllText(Path.Combine(Dir, "log.txt"), DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + "\n");
        }
    }

    /// <summary>시험 중 10초마다 프레임 시간(평균 · 느린 1% · 최악)과 메모리를 기록한다.</summary>
    sealed class FrameStats : MonoBehaviour
    {
        readonly System.Collections.Generic.List<float> ms = new System.Collections.Generic.List<float>(2048);
        float next;

        void Start()
        {
            next = Time.realtimeSinceStartup + 10f;
            AutoShots.Log("perf: " + SystemInfo.graphicsDeviceName + " · " + SystemInfo.processorType + " · " + Screen.width + "x" + Screen.height
                + " · vsync " + QualitySettings.vSyncCount + " · target " + Application.targetFrameRate);
        }

        void Update()
        {
            if (AutoShots.SkipFrames > 0) { AutoShots.SkipFrames--; return; }
            float dt = Time.unscaledDeltaTime * 1000f;
            ms.Add(dt);
            if (dt > 40f && Time.realtimeSinceStartup > 12f)
                AutoShots.Log(string.Format("perf: hitch {0:0.0} ms (frame {1}, gc {2})", dt, Time.frameCount, System.GC.CollectionCount(0)));
            if (Time.realtimeSinceStartup < next || ms.Count < 10) return;
            next = Time.realtimeSinceStartup + 10f;
            ms.Sort();
            float sum = 0f;
            foreach (var m in ms) sum += m;
            float avg = sum / ms.Count, p99 = ms[Mathf.Min(ms.Count - 1, (int)(ms.Count * 0.99f))], worst = ms[ms.Count - 1];
            long mono = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() >> 20, total = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() >> 20;
            AutoShots.Log(string.Format("perf: {0:0} fps (avg {1:0.0} ms, 1% {2:0.0} ms, worst {3:0.0} ms) · mono {4} MB · total {5} MB",
                1000f / avg, avg, p99, worst, mono, total));
            ms.Clear();
        }
    }
}
