using System;
using System.IO;
using BluffAuction.Boot;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BluffAuction.EditorTools
{
    /// <summary>
    /// 배치 모드 빌드 진입점:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod BluffAuction.EditorTools.BuildScript.BuildWindows -logFile Logs/build.log
    /// 설정 → 자산 → 씬 → Windows 64비트 빌드 (Builds/Windows/BluffAuction.exe).
    /// </summary>
    public static class BuildScript
    {
        public const string ScenePath = "Assets/BluffAuction/Scenes/Main.unity";
        public const string OutDir = "Builds/Windows";
        public const string ExeName = "BluffAuction.exe";

        [MenuItem("Bluff Auction/Build/Windows")]
        public static void BuildWindows()
        {
            try
            {
                PrepareAll();
                var opts = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = Path.Combine(OutDir, ExeName),
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = HasArg("-devbuild") ? BuildOptions.Development : BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(opts);
                var s = report.summary;
                Debug.Log("[Build] result " + s.result + " size " + (s.totalSize / (1024 * 1024)) + "MB time " + s.totalTime + " errors " + s.totalErrors);
                if (s.result != BuildResult.Succeeded) Fail("build " + s.result);
                else
                {
                    CopyNotices();
                    StripDoNotShip();
                    SteamAppIdFile();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Fail(e.Message);
            }
        }

        /// <summary>빌드 없이 설정·자산·씬만 (컴파일 확인용).</summary>
        public static void PrepareOnly()
        {
            try { PrepareAll(); }
            catch (Exception e)
            {
                Debug.LogException(e);
                Fail(e.Message);
            }
        }

        static void PrepareAll()
        {
            ProjectSetup.Apply();
            ContentBuilder.BuildAll();
            BuildScene();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Bluff Auction/Setup/Build Scene")]
        public static void BuildScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Bootstrap");
            go.AddComponent<Bootstrap>();
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        /// <summary>글꼴 라이선스·제3자 고지를 실행 파일 옆에 둔다 (OFL·Apache 가 배포물에 사본을 요구한다).</summary>
        static void CopyNotices()
        {
            const string fonts = "Assets/BluffAuction/Fonts/";
            string lic = Path.Combine(OutDir, "Licenses");
            Directory.CreateDirectory(lic);
            File.Copy(fonts + "OFL.txt", Path.Combine(lic, "NotoSansCJK-OFL.txt"), true);
            File.Copy(fonts + "Oswald-OFL.txt", Path.Combine(lic, "Oswald-OFL.txt"), true);
            File.Copy(fonts + "RobotoSlab-LICENSE.txt", Path.Combine(lic, "RobotoSlab-LICENSE.txt"), true);
            const string steamworks = "Packages/com.rlabrecque.steamworks.net/LICENSE.md";
            if (File.Exists(steamworks)) File.Copy(steamworks, Path.Combine(lic, "SteamworksNET-LICENSE.txt"), true);
            File.Copy("Docs/ThirdPartyNotices.txt", Path.Combine(OutDir, "ThirdPartyNotices.txt"), true);
            // 플레이어용 안내 (실행 · 조작 · 온라인 · 문제 보고)
            if (File.Exists("Docs/PlayerReadme.txt")) File.Copy("Docs/PlayerReadme.txt", Path.Combine(OutDir, "README.txt"), true);
        }

        /// <summary>Burst 디버그 정보 폴더는 배포하면 안 된다 (Steam 에 올릴 폴더를 그대로 쓸 수 있게 지운다).</summary>
        static void StripDoNotShip()
        {
            foreach (var d in Directory.GetDirectories(OutDir, "*_DoNotShip"))
            {
                Directory.Delete(d, true);
                Debug.Log("[Build] removed " + Path.GetFileName(d));
            }
        }

        /// <summary>
        /// 시험 앱(480)일 때만 실행 파일 옆에 steam_appid.txt 를 둔다 (Steam 밖에서 켜도 Steam API 가 붙게).
        /// 정식 앱 번호로 바꾸면 지운다: Steam 이 띄우면 필요 없고, 밖에서 켜면 Steam 으로 다시 띄운다(RestartAppIfNecessary).
        /// </summary>
        static void SteamAppIdFile()
        {
            string path = Path.Combine(OutDir, "steam_appid.txt");
            if (BluffAuction.Platform.Steam.IsTestApp) File.WriteAllText(path, BluffAuction.Platform.Steam.AppId.ToString());
            else if (File.Exists(path)) File.Delete(path);
        }

        static bool HasArg(string a)
        {
            foreach (var x in Environment.GetCommandLineArgs()) if (x == a) return true;
            return false;
        }

        static void Fail(string why)
        {
            Debug.LogError("[Build] FAILED: " + why);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
