using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BluffAuction.EditorTools
{
    /// <summary>
    /// 프로젝트 설정을 코드로 맞춘다: URP 파이프라인 자산(고·중·저 품질), 렌더러(SSAO), 플레이어 설정(이름·해상도·색 공간).
    /// 배치 모드 빌드(BuildScript)가 매번 부르므로 이미 맞는 값은 건드리지 않는다.
    /// </summary>
    public static class ProjectSetup
    {
        public const string SettingsDir = "Assets/BluffAuction/Settings";
        public const string CompanyName = "Hyeophap Games";
        public const string ProductName = "Bluff Auction";
        public const string Version = "0.11.0";

        [MenuItem("Bluff Auction/Setup/Apply Project Settings")]
        public static void Apply()
        {
            Directory.CreateDirectory(SettingsDir);
            var renderer = EnsureRenderer();
            var high = EnsurePipeline("URP-High", renderer, 1.0f, 4, 4096, 2, true);
            var medium = EnsurePipeline("URP-Medium", renderer, 1.0f, 2, 2048, 2, true);
            var low = EnsurePipeline("URP-Low", renderer, 0.8f, 1, 1024, 1, false);

            GraphicsSettings.defaultRenderPipeline = high;
            SetQualityLevels(new[] { "Low", "Medium", "High" }, new RenderPipelineAsset[] { low, medium, high });

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            // 하드웨어 통계를 Unity 에 보내지 않는다 (개인정보처리방침). 이 버전에는 공개 API 가 없어 저장된 속성을 직접 고친다.
            // 라이선스가 막으면 Unity 가 무시한다.
            SetPlayerFlag("submitAnalytics", false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.forceSingleInstance = false;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            // Steamworks.NET (Packages/com.rlabrecque.steamworks.net) 을 켠다. 패키지의 RedistInstall 도 넣지만
            // 배치 빌드에서는 지연 호출이 늦을 수 있어 여기서 확실히 넣는다.
            AddDefine(NamedBuildTarget.Standalone, "STEAMWORKS_NET");
            AssetDatabase.SaveAssets();
        }

        static void AddDefine(NamedBuildTarget target, string symbol)
        {
            var cur = PlayerSettings.GetScriptingDefineSymbols(target);
            var list = new System.Collections.Generic.List<string>(cur.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
            if (list.Contains(symbol)) return;
            list.Add(symbol);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
        }

        static UniversalRendererData EnsureRenderer()
        {
            string path = SettingsDir + "/URP-Renderer.asset";
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(data, path);
                // 기본 후처리 자료(셰이더·LUT)
                var so0 = new SerializedObject(data);
                var ppGuid = AssetDatabase.FindAssets("PostProcessData t:PostProcessData");
                foreach (var g in ppGuid)
                {
                    var pp = AssetDatabase.LoadAssetAtPath<PostProcessData>(AssetDatabase.GUIDToAssetPath(g));
                    if (pp != null)
                    {
                        var prop = so0.FindProperty("postProcessData");
                        if (prop != null) prop.objectReferenceValue = pp;
                        break;
                    }
                }
                so0.ApplyModifiedPropertiesWithoutUndo();
            }
            var so = new SerializedObject(data);
            SetEnum(so, "m_RenderingMode", 2);   // Forward+ (조명이 많은 실내)
            SetEnum(so, "m_DepthPrimingMode", 0);
            so.ApplyModifiedPropertiesWithoutUndo();

            bool hasSsao = false;
            foreach (var f in data.rendererFeatures) if (f is ScreenSpaceAmbientOcclusion) hasSsao = true;
            if (!hasSsao)
            {
                var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ssao, data);
                data.rendererFeatures.Add(ssao);
                var fso = new SerializedObject(ssao);
                var settings = fso.FindProperty("m_Settings");
                if (settings != null)
                {
                    Set(settings, "Intensity", 1.6f);
                    Set(settings, "Radius", 0.3f);
                    Set(settings, "DirectLightingStrength", 0.3f);
                    Set(settings, "Falloff", 40f);
                }
                fso.ApplyModifiedPropertiesWithoutUndo();
                // 렌더러 기능 목록의 직렬화 맵도 맞춘다
                var dso = new SerializedObject(data);
                var map = dso.FindProperty("m_RendererFeatureMap");
                if (map != null)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out string _, out long localId);
                    map.arraySize = data.rendererFeatures.Count;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                }
                dso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
            return data;
        }

        static UniversalRenderPipelineAsset EnsurePipeline(string name, UniversalRendererData renderer, float renderScale, int msaa, int shadowRes, int cascades, bool softShadows)
        {
            string path = SettingsDir + "/" + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.supportsHDR = true;
            asset.msaaSampleCount = msaa;
            asset.renderScale = renderScale;
            asset.supportsCameraDepthTexture = true;
            asset.supportsCameraOpaqueTexture = true;
            asset.mainLightShadowmapResolution = shadowRes;
            asset.maxAdditionalLightsCount = 8;
            asset.additionalLightsShadowmapResolution = shadowRes;
            asset.shadowDistance = 30f;
            asset.shadowCascadeCount = cascades;
            asset.useSRPBatcher = true;
            asset.colorGradingLutSize = 32;
            // 읽기 전용 속성은 직렬화 필드로
            var so = new SerializedObject(asset);
            SetEnum(so, "m_MainLightRenderingMode", 1);          // PerPixel
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetEnum(so, "m_AdditionalLightsRenderingMode", 1);   // PerPixel
            SetBool(so, "m_AdditionalLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", softShadows);
            SetBool(so, "m_ReflectionProbeBlending", true);
            SetBool(so, "m_ReflectionProbeBoxProjection", true);
            SetEnum(so, "m_ColorGradingMode", 1);     // HDR 색 보정
            SetEnum(so, "m_SoftShadowQuality", softShadows ? 2 : 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void SetQualityLevels(string[] names, RenderPipelineAsset[] pipelines)
        {
            var qm = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qm == null || qm.Length == 0) return;
            var so = new SerializedObject(qm[0]);
            var levels = so.FindProperty("m_QualitySettings");
            levels.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++)
            {
                var lv = levels.GetArrayElementAtIndex(i);
                lv.FindPropertyRelative("name").stringValue = names[i];
                var rp = lv.FindPropertyRelative("customRenderPipeline");
                if (rp != null) rp.objectReferenceValue = pipelines[i];
                var vs = lv.FindPropertyRelative("vSyncCount");
                if (vs != null) vs.intValue = 1;
                var af = lv.FindPropertyRelative("anisotropicTextures");
                if (af != null) af.intValue = 2;
                var lod = lv.FindPropertyRelative("lodBias");
                if (lod != null) lod.floatValue = 2f;
            }
            so.FindProperty("m_CurrentQuality").intValue = names.Length - 1;
            // 플랫폼별 기본 품질도 '높음'으로
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform != null)
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    var second = perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second");
                    if (second != null) second.intValue = names.Length - 1;
                }
            so.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel(names.Length - 1, true);
        }

        static void SetEnum(SerializedObject so, string prop, int v)
        {
            // 열거형도 실제 값으로 (선언 순서와 값이 다른 열거형이 있다)
            var p = so.FindProperty(prop);
            if (p == null) return;
            p.intValue = v;
        }

        static void SetBool(SerializedObject so, string prop, bool v)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.boolValue = v;
        }

        static void Set(SerializedProperty parent, string rel, float v)
        {
            var p = parent.FindPropertyRelative(rel);
            if (p != null) p.floatValue = v;
        }

        /// <summary>PlayerSettings 에 공개 API 가 없는 값 (ProjectSettings.asset 의 속성 이름으로).</summary>
        static void SetPlayerFlag(string prop, bool v)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning("[Setup] no player setting " + prop); return; }
            if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v;
            else p.intValue = v ? 1 : 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
