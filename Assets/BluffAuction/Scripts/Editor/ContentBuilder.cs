using System.Collections.Generic;
using System.IO;
using BluffAuction.Art;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace BluffAuction.EditorTools
{
    /// <summary>
    /// 빌드에 넣을 자산을 만든다: 텍스처 → URP 재질 → UI 글꼴(TextCore, 한·일·번·간 대체) → UI 패널 설정 → 소리 → ArtLibrary.
    /// 재질은 매번 값만 다시 맞춘다(지우지 않음). 실행 중 모형은 코드로 만들고 이 재질을 쓴다.
    /// </summary>
    public static class ContentBuilder
    {
        public const string Root = "Assets/BluffAuction";
        public const string MatDir = Root + "/Generated/Materials";
        public const string FontDir = Root + "/Fonts";
        public const string UiDir = Root + "/UI";
        public const string LibraryPath = Root + "/Resources/ArtLibrary.asset";

        static ArtLibrary lib;

        [MenuItem("Bluff Auction/Setup/Build Content")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(Root + "/Resources");
            lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.Materials.Clear();
            lib.Textures.Clear();
            BuildMaterials();
            BuildParticles();
            BuildFonts();
            BuildPanel();
            // 소리는 SoundFactory 가 있으면 만든다 (따로 작성 중인 파일이라 이름으로 찾는다)
            var sound = typeof(ContentBuilder).Assembly.GetType("BluffAuction.EditorTools.SoundFactory");
            var build = sound != null ? sound.GetMethod("BuildAll") : null;
            if (build != null) build.Invoke(null, new object[] { lib });
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Build] content: " + lib.Materials.Count + " materials, " + lib.Clips.Count + " clips");
        }

        // ═════════════════ 재질 ═════════════════

        sealed class Spec
        {
            public string Name;
            public Color Color = Color.white;
            public float Metallic;
            public float Smoothness = 0.5f;
            public Texture2D Map;
            public Texture2D Normal;
            public float NormalScale = 1f;
            public Vector2 Tiling = Vector2.one;
            public bool SmoothFromAlpha;
            public Color Emission = Color.black;
            public Texture2D EmissionMap;
            public bool Emissive;          // 실행 중 색을 바꿔 빛나게 할 수 있게 _EMISSION 을 켜 둔다
            public bool Transparent;
            public bool Clip;
            public bool DoubleSided;
            public float ClearCoat;
        }

        static Spec S(string name, Color c, float metallic, float smooth) { return new Spec { Name = name, Color = c, Metallic = metallic, Smoothness = smooth }; }

        static Color Hex(string h)
        {
            Color c;
            ColorUtility.TryParseHtmlString(h, out c);
            return c;
        }

        static void BuildMaterials()
        {
            var floor = TextureFactory.WoodFloor();
            var floorN = TextureFactory.WoodFloorNormal();
            var mahogany = TextureFactory.Mahogany();
            var marble = TextureFactory.Marble();
            var marbleDark = TextureFactory.MarbleDark();
            var wallpaper = TextureFactory.Wallpaper();
            var velvet = TextureFactory.Velvet();
            var velvetN = TextureFactory.VelvetNormal();
            var rug = TextureFactory.Rug();
            var felt = TextureFactory.Felt();
            var leather = TextureFactory.Leather();
            var leatherN = TextureFactory.LeatherNormal();
            var smudge = TextureFactory.MetalSmudge();
            var crackle = TextureFactory.Crackle();
            var clock = TextureFactory.ClockFace();
            var globe = TextureFactory.Globe();
            var pages = TextureFactory.Pages();
            var night = TextureFactory.NightWindow();
            lib.Textures.Add(new ArtLibrary.NamedTexture { Name = "soft_dot", Texture = TextureFactory.SoftDot() });
            lib.Textures.Add(new ArtLibrary.NamedTexture { Name = "confetti", Texture = TextureFactory.Confetti() });

            var specs = new List<Spec>
            {
                // ── 홀
                new Spec { Name = "Floor", Map = floor, Normal = floorN, NormalScale = 0.6f, Tiling = new Vector2(4, 4), SmoothFromAlpha = true, Smoothness = 0.9f },
                new Spec { Name = "Wallpaper", Map = wallpaper, Tiling = new Vector2(1, 1), SmoothFromAlpha = true, Smoothness = 0.8f },
                new Spec { Name = "Mahogany", Map = mahogany, SmoothFromAlpha = true, Smoothness = 0.95f, ClearCoat = 1 },
                S("DarkWood", Hex("#2a1810"), 0f, 0.55f),
                new Spec { Name = "Marble", Map = marble, SmoothFromAlpha = true, Smoothness = 0.95f },
                new Spec { Name = "MarbleDark", Map = marbleDark, SmoothFromAlpha = true, Smoothness = 0.95f },
                S("Plaster", Hex("#d9ccb3"), 0f, 0.25f),
                S("Ceiling", Hex("#3a2a20"), 0f, 0.3f),
                new Spec { Name = "Velvet", Map = velvet, Normal = velvetN, NormalScale = 0.8f, SmoothFromAlpha = true, Smoothness = 0.9f },
                S("VelvetPlain", Hex("#5a0812"), 0f, 0.28f),
                new Spec { Name = "Felt", Map = felt, Tiling = new Vector2(3, 3), SmoothFromAlpha = true, Smoothness = 1f },
                new Spec { Name = "Rug", Map = rug, Clip = true, Smoothness = 0.1f },
                new Spec { Name = "NightWindow", Color = Color.black, Smoothness = 0.9f, Emission = Color.white * 1.3f, EmissionMap = night, Map = night },
                new Spec { Name = "Glass", Color = new Color(0.85f, 0.9f, 0.95f, 0.12f), Smoothness = 0.97f, Transparent = true },
                S("CandleWax", Hex("#f3e8cf"), 0f, 0.35f),
                new Spec { Name = "Flame", Color = Hex("#ffcf7a"), Smoothness = 0f, Emission = new Color(1f, 0.6f, 0.22f) * 9f },
                new Spec { Name = "WarmGlow", Color = Hex("#ffe2b0"), Smoothness = 0.2f, Emission = new Color(1f, 0.78f, 0.45f) * 2.2f },
                S("Black", Hex("#060606"), 0f, 0.55f),
                S("BlackGloss", Hex("#050505"), 0f, 0.9f),
                // ── 금속
                new Spec { Name = "Gold", Color = Hex("#ffcc66"), Metallic = 1f, Map = smudge, SmoothFromAlpha = true, Smoothness = 1f },
                new Spec { Name = "GoldDark", Color = Hex("#b8873a"), Metallic = 1f, Map = smudge, SmoothFromAlpha = true, Smoothness = 0.85f },
                new Spec { Name = "Brass", Color = Hex("#d9ad62"), Metallic = 1f, Map = smudge, SmoothFromAlpha = true, Smoothness = 0.92f },
                new Spec { Name = "Silver", Color = Hex("#e9eaef"), Metallic = 1f, Map = smudge, SmoothFromAlpha = true, Smoothness = 1f },
                S("Iron", Hex("#2b2b2e"), 1f, 0.45f),
                // ── 경매품
                S("Terracotta", Hex("#a5512c"), 0f, 0.32f),
                S("TerracottaDark", Hex("#3a1a10"), 0f, 0.4f),
                new Spec { Name = "Celadon", Map = crackle, SmoothFromAlpha = true, Smoothness = 0.95f },
                S("Porcelain", Hex("#f4f1ea"), 0f, 0.85f),
                S("PorcelainBlue", Hex("#2c4f9e"), 0f, 0.85f),
                new Spec { Name = "Sapphire", Color = Hex("#1c3fc8"), Smoothness = 0.98f, Emission = Hex("#1c3fc8") * 0.25f },
                new Spec { Name = "Ruby", Color = Hex("#c8102e"), Smoothness = 0.98f, Emission = Hex("#c8102e") * 0.25f },
                new Spec { Name = "Emerald", Color = Hex("#0f9e5a"), Smoothness = 0.98f, Emission = Hex("#0f9e5a") * 0.22f },
                new Spec { Name = "PinkGem", Color = Hex("#ff6fae"), Smoothness = 0.98f, Emission = Hex("#ff6fae") * 0.25f },
                new Spec { Name = "Diamond", Color = Hex("#eaf6ff"), Metallic = 0.3f, Smoothness = 1f, Emission = Hex("#cfe8ff") * 0.18f },
                S("Pearl", Hex("#f2ede3"), 0.25f, 0.9f),
                new Spec { Name = "Leather", Color = Hex("#6b3a1f"), Map = leather, Normal = leatherN, NormalScale = 0.6f, SmoothFromAlpha = true, Smoothness = 1f },
                new Spec { Name = "LeatherRed", Color = Hex("#7a1414"), Map = leather, Normal = leatherN, NormalScale = 0.6f, SmoothFromAlpha = true, Smoothness = 1f },
                new Spec { Name = "LeatherGreen", Color = Hex("#1d4a32"), Map = leather, Normal = leatherN, NormalScale = 0.6f, SmoothFromAlpha = true, Smoothness = 1f },
                new Spec { Name = "Pages", Map = pages, SmoothFromAlpha = true, Smoothness = 1f },
                new Spec { Name = "ClockFace", Map = clock, SmoothFromAlpha = true, Smoothness = 1f },
                new Spec { Name = "Globe", Map = globe, SmoothFromAlpha = true, Smoothness = 1f },
                new Spec { Name = "CrystalBall", Color = new Color(0.78f, 0.88f, 1f, 0.28f), Smoothness = 1f, Transparent = true, Emission = new Color(0.25f, 0.35f, 0.7f) * 0.4f },
                // ── 캐릭터 (실행 중 색을 바꿔 쓴다)
                new Spec { Name = "Skin", Color = Hex("#f0c8a8"), Smoothness = 0.42f },
                new Spec { Name = "Cloth", Color = Color.white, Smoothness = 0.25f, Emissive = false },
                new Spec { Name = "Satin", Color = Color.white, Smoothness = 0.7f },
                new Spec { Name = "VelvetCloth", Color = Color.white, Normal = velvetN, NormalScale = 0.5f, Smoothness = 0.35f },
                new Spec { Name = "Lacquer", Color = Color.white, Smoothness = 0.9f },
                new Spec { Name = "Hair", Color = Color.white, Smoothness = 0.55f },
                new Spec { Name = "Feather", Color = Color.white, Smoothness = 0.35f },
                S("EyeWhite", Hex("#f2f2f2"), 0f, 0.8f),
                S("Pupil", Hex("#0a0806"), 0f, 0.98f),
                new Spec { Name = "Glow", Color = Color.white, Smoothness = 0.5f, Emissive = true },
                new Spec { Name = "Paint", Color = Color.white, Smoothness = 0.5f },
            };
            for (int i = 0; i < 4; i++)
                specs.Add(new Spec { Name = "Painting" + i, Map = TextureFactory.Painting(i + 1), SmoothFromAlpha = true, Smoothness = 1f });

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var s in specs)
            {
                string path = MatDir + "/" + s.Name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader);
                    AssetDatabase.CreateAsset(m, path);
                }
                m.shader = shader;
                m.SetColor("_BaseColor", s.Color);
                m.SetTexture("_BaseMap", s.Map);
                m.SetTextureScale("_BaseMap", s.Tiling);
                m.SetFloat("_Metallic", s.Metallic);
                m.SetFloat("_Smoothness", Mathf.Clamp01(s.Smoothness));
                m.SetFloat("_SmoothnessTextureChannel", s.SmoothFromAlpha && !s.Clip && !s.Transparent ? 1f : 0f);
                m.SetTexture("_BumpMap", s.Normal);
                m.SetFloat("_BumpScale", s.NormalScale);
                bool emissive = s.Emissive || s.Emission.maxColorComponent > 0.001f || s.EmissionMap != null;
                m.SetColor("_EmissionColor", s.Emission);
                m.SetTexture("_EmissionMap", s.EmissionMap);
                if (emissive) m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetFloat("_Surface", s.Transparent ? 1f : 0f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_AlphaClip", s.Clip ? 1f : 0f);
                m.SetFloat("_Cutoff", 0.5f);
                m.SetFloat("_Cull", s.DoubleSided ? 0f : 2f);
                m.SetFloat("_ClearCoatMask", s.ClearCoat);
                m.SetFloat("_EnvironmentReflections", 1f);
                m.SetFloat("_SpecularHighlights", 1f);
                BaseShaderGUI.SetMaterialKeywords(m, LitGUI.SetMaterialKeywords);
                if (emissive) m.EnableKeyword("_EMISSION");
                else m.DisableKeyword("_EMISSION");
                EditorUtility.SetDirty(m);
                lib.Materials.Add(new ArtLibrary.NamedMaterial { Name = s.Name, Material = m });
            }
        }

        static void BuildParticles()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            lib.ParticleAdditive = ParticleMat("ParticleAdditive", shader, lib.Tex("soft_dot"), true);
            lib.ParticleConfetti = ParticleMat("ParticleConfetti", shader, lib.Tex("confetti"), false);
        }

        static Material ParticleMat(string name, Shader shader, Texture2D tex, bool additive)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);   // 2 = Additive, 0 = Alpha
            m.SetFloat("_Cull", 0f);
            BaseShaderGUI.SetMaterialKeywords(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ═════════════════ 글꼴 ═════════════════

        static FontAsset MakeFont(string ttf, string assetName, int size)
        {
            string path = FontDir + "/" + assetName + ".asset";
            var fa = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            if (fa != null) return fa;
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontDir + "/" + ttf);
            if (font == null) { Debug.LogError("[Build] font missing " + ttf); return null; }
            fa = FontAsset.CreateFontAsset(font, size, size / 10, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, path);
            if (fa.atlasTextures != null)
                foreach (var t in fa.atlasTextures)
                {
                    if (t == null) continue;
                    t.name = assetName + " Atlas";
                    AssetDatabase.AddObjectToAsset(t, fa);
                }
            if (fa.material != null)
            {
                fa.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static void BuildFonts()
        {
            var kr = MakeFont("NotoSansKR-Medium.ttf", "UI Medium", 72);
            var krb = MakeFont("NotoSansKR-Bold.ttf", "UI Bold", 72);
            var jp = MakeFont("NotoSansJP-Medium.ttf", "UI JP", 72);
            var tc = MakeFont("NotoSansTC-Medium.ttf", "UI TC", 72);
            var sc = MakeFont("NotoSansSC-Medium.ttf", "UI SC", 72);
            var fallbacks = new List<FontAsset> { jp, tc, sc };
            foreach (var f in new[] { kr, krb })
            {
                if (f == null) continue;
                f.fallbackFontAssetTable = new List<FontAsset>(fallbacks);
                EditorUtility.SetDirty(f);
            }
            // 숫자·제목용 (라틴 글자만 있으므로 한·일·중 글자는 Noto 로 대체)
            var display = MakeFont("Oswald-Bold.ttf", "Display", 90);
            var serif = MakeFont("RobotoSlab-Bold.ttf", "Serif", 90);
            foreach (var f in new[] { display, serif })
            {
                if (f == null) continue;
                f.fallbackFontAssetTable = new List<FontAsset> { krb, jp, tc, sc };
                EditorUtility.SetDirty(f);
            }
            lib.UiFont = kr;
            lib.UiFontBold = krb;
            AssetDatabase.SaveAssets();
        }

        // ═════════════════ UI 패널 ═════════════════

        static void BuildPanel()
        {
            Directory.CreateDirectory(UiDir);
            string tssPath = UiDir + "/GameTheme.tss";
            if (!File.Exists(tssPath))
            {
                File.WriteAllText(tssPath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(tssPath, ImportAssetOptions.ForceSynchronousImport);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tssPath);

            string textPath = UiDir + "/PanelText.asset";
            var text = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(textPath);
            if (text == null)
            {
                text = ScriptableObject.CreateInstance<PanelTextSettings>();
                AssetDatabase.CreateAsset(text, textPath);
            }
            var tso = new SerializedObject(text);
            var def = tso.FindProperty("m_DefaultFontAsset");
            if (def != null) def.objectReferenceValue = lib.UiFont;
            var fb = tso.FindProperty("m_FallbackFontAssets");
            if (fb != null)
            {
                var list = new List<FontAsset> { lib.UiFont };
                foreach (var f in lib.UiFont.fallbackFontAssetTable) list.Add(f);
                fb.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++) fb.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            tso.ApplyModifiedPropertiesWithoutUndo();

            string panelPath = UiDir + "/GamePanel.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, panelPath);
            }
            panel.themeStyleSheet = theme;
            panel.textSettings = text;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f;
            panel.clearColor = false;
            panel.sortingOrder = 10;
            EditorUtility.SetDirty(panel);
            lib.Panel = panel;
            // 스타일시트는 UI 그림·글꼴을 만든 뒤 다시 가져와야 참조가 붙는다
            UiTextures.BuildAll();
            AssetDatabase.ImportAsset(UiDir + "/Game.uss", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            lib.Style = AssetDatabase.LoadAssetAtPath<StyleSheet>(UiDir + "/Game.uss");
        }
    }
}
