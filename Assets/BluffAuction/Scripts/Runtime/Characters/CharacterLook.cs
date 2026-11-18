using System;
using UnityEngine;

namespace BluffAuction.Characters
{
    public enum HatStyle { None, TopHat, Bowler, Tiara, FlatCap, Captain, FoxHood, Feathered, Beret, Crown, Fedora, Flower }
    public enum HairStyle { None, Short, Bun, Wig, Tufts, Long, Slick, Bob, Ponytail, Spiky, Curly, Mohawk }
    public enum FacialHair { None, Mustache, CurlMustache, Beard, Goatee, Sideburns }
    public enum MaskStyle { Domino, CatEye, Lace, Half, Beak, Fox, EyePatch, Butterfly, None }
    public enum EyeStyle { Round, Almond, Sleepy, Sharp, Happy, Wide }
    public enum Outfit { Tailcoat, Gown, Vest, Uniform, Cloak, Sweater }
    public enum Neckwear { None, Bowtie, Necktie, Pearls, Cravat, Medal, Scarf }
    public enum Eyewear { None, Round, Monocle, Shades }
    public enum Earring { None, Gold, Pearl }

    /// <summary>
    /// 가면 손님 한 명의 생김새. AI 성격마다 하나, 플레이어는 꾸미기 화면에서 직접 고른다
    /// (저장 파일·온라인 대기실로 JSON 으로 오간다).
    /// </summary>
    [Serializable]
    public sealed class CharacterLook
    {
        public string Id = "custom";
        public Color Skin = new Color(0.94f, 0.78f, 0.66f);
        public Color Coat = new Color(0.2f, 0.1f, 0.3f);
        public Color Trim = new Color(0.85f, 0.68f, 0.3f);
        public Color Shirt = new Color(0.97f, 0.95f, 0.9f);
        public Color Pants = new Color(0.08f, 0.08f, 0.1f);
        public Color MaskColor = Color.white;
        public Color MaskTrim = new Color(0.9f, 0.72f, 0.3f);
        public Color Hair = new Color(0.2f, 0.12f, 0.08f);
        public Color HatColor = new Color(0.05f, 0.05f, 0.06f);
        public Color Accent = new Color(0.8f, 0.1f, 0.15f);
        public Color EyeColor = new Color(0.25f, 0.16f, 0.1f);
        public Color Glove = new Color(0.96f, 0.95f, 0.92f);
        public float Width = 1f;
        public float Height = 1f;
        public float Head = 1f;
        public Outfit Outfit = Outfit.Tailcoat;
        public HatStyle Hat;
        public HairStyle HairStyle = HairStyle.Short;
        public FacialHair Facial;
        public MaskStyle Mask = MaskStyle.Domino;
        public EyeStyle Eyes = EyeStyle.Round;
        public Neckwear Neck = Neckwear.Bowtie;
        public Eyewear Eyewear;
        public Earring Earrings;
        public bool MaskFeather;
        public int Voice;

        public bool Gown { get { return Outfit == Outfit.Gown; } }

        static Color C(string hex)
        {
            Color c;
            ColorUtility.TryParseHtmlString(hex, out c);
            return c;
        }

        public CharacterLook Clone() { return JsonUtility.FromJson<CharacterLook>(JsonUtility.ToJson(this)); }
        public string ToJson() { return JsonUtility.ToJson(this); }

        /// <summary>JSON 에서 읽는다. 잘못된 값은 범위 안으로 (온라인에서 받은 값도 이걸로 거른다).</summary>
        public static CharacterLook FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            CharacterLook l;
            try { l = JsonUtility.FromJson<CharacterLook>(json); }
            catch { return null; }
            if (l == null) return null;
            l.Width = Mathf.Clamp(l.Width, 0.85f, 1.3f);
            l.Height = Mathf.Clamp(l.Height, 0.9f, 1.08f);
            l.Head = Mathf.Clamp(l.Head, 0.9f, 1.12f);
            l.Outfit = Clamp(l.Outfit);
            l.Hat = Clamp(l.Hat);
            l.HairStyle = Clamp(l.HairStyle);
            l.Facial = Clamp(l.Facial);
            l.Mask = Clamp(l.Mask);
            l.Eyes = Clamp(l.Eyes);
            l.Neck = Clamp(l.Neck);
            l.Eyewear = Clamp(l.Eyewear);
            l.Earrings = Clamp(l.Earrings);
            l.Voice = Mathf.Clamp(l.Voice, 0, 8);
            if (string.IsNullOrEmpty(l.Id) || l.Id.Length > 40) l.Id = "custom";
            return l;
        }

        static T Clamp<T>(T v) where T : struct
        {
            return Enum.IsDefined(typeof(T), v) ? v : default(T);
        }

        /// <summary>초상화 캐시 열쇠 (생김새가 같으면 같은 값).</summary>
        public string Key { get { return Id + "#" + ToJson().GetHashCode().ToString("x"); } }

        // ───────── 꾸미기에서 고르는 색 ─────────

        public static readonly Color[] SkinTones =
        {
            C("#f8dcc8"), C("#f1c9ae"), C("#e8b996"), C("#d9a882"), C("#c98f6a"), C("#a86e4c"), C("#87533a"), C("#5e3a28")
        };

        public static readonly Color[] HairColors =
        {
            C("#141012"), C("#3a2416"), C("#5a3a22"), C("#8a5a32"), C("#b5651d"), C("#d9a441"), C("#efd9a0"),
            C("#e6e3dc"), C("#9a9aa2"), C("#b8243a"), C("#5a2a8a"), C("#2a5aa8"), C("#2f8a5a"), C("#e07ab0")
        };

        public static readonly Color[] ClothColors =
        {
            C("#141016"), C("#2a2c34"), C("#4a4c56"), C("#f4efe2"), C("#4b1f6e"), C("#6a0f1f"), C("#8e0f22"), C("#c41e2a"),
            C("#c8601e"), C("#e3b448"), C("#1f4a3a"), C("#0f4a3a"), C("#2f6f3a"), C("#1b2a4e"), C("#2a3a6a"), C("#3a7fb0"),
            C("#6a4a2e"), C("#8a6a4a"), C("#e07ab0"), C("#7a3aa0")
        };

        public static readonly Color[] MetalColors =
        {
            C("#e7c35a"), C("#c9a24a"), C("#d8dbe6"), C("#b87333"), C("#141414"), C("#f6f3ee"), C("#c41e2a"), C("#3a7fb0")
        };

        public static readonly Color[] EyeColors =
        {
            C("#3a2416"), C("#6a4a2e"), C("#2a5aa8"), C("#4aa0c8"), C("#2f8a5a"), C("#8a8a92"), C("#b8243a"), C("#7a3aa0"), C("#d9a441")
        };

        /// <summary>새 플레이어의 기본 생김새.</summary>
        public static CharacterLook DefaultPlayer()
        {
            return new CharacterLook
            {
                Id = "custom", Coat = C("#1a1a1f"), Trim = C("#e7c35a"), MaskColor = C("#e7c35a"), MaskTrim = C("#1a1a1f"),
                Hat = HatStyle.TopHat, Mask = MaskStyle.Domino, Eyes = EyeStyle.Round, EyeColor = C("#3a2416"), Voice = 0
            };
        }

        /// <summary>AI 성격 순서 (AiProfiles.All 의 CharacterId) 와 같다.</summary>
        public static readonly CharacterLook[] Guests =
        {
            // 0 허풍 남작: 둥근 몸, 보라 연미복, 금 도미노 가면 + 외알 안경, 실크햇, 말린 콧수염
            new CharacterLook { Id = "baron", Coat = C("#4b1f6e"), Trim = C("#e0b24a"), Shirt = C("#f4efe2"), Pants = C("#1d1426"), MaskColor = C("#e7c35a"), MaskTrim = C("#7a4a12"),
                Hair = C("#3a2416"), HatColor = C("#141016"), Accent = C("#c0263a"), EyeColor = C("#3a2416"), Width = 1.24f, Height = 1.0f, Hat = HatStyle.TopHat, HairStyle = HairStyle.Short,
                Facial = FacialHair.CurlMustache, Mask = MaskStyle.Domino, Eyewear = Eyewear.Monocle, Eyes = EyeStyle.Wide, Neck = Neckwear.Bowtie, Voice = 0 },
            // 1 장부 부인: 날씬, 짙은 초록 드레스, 검은 레이스 가면, 올림머리, 진주 목걸이, 안경
            new CharacterLook { Id = "ledger", Skin = C("#f1d2bd"), Coat = C("#1f4a3a"), Trim = C("#c9a24a"), Shirt = C("#dfe8e2"), Pants = C("#173528"), MaskColor = C("#141414"), MaskTrim = C("#3c3c3c"),
                Hair = C("#5a3a22"), Accent = C("#d7d0c0"), EyeColor = C("#2f8a5a"), Glove = C("#1f4a3a"), Width = 0.9f, Height = 1.03f, Hat = HatStyle.None, HairStyle = HairStyle.Bun, Mask = MaskStyle.Lace,
                Outfit = Outfit.Gown, Neck = Neckwear.Pearls, Eyewear = Eyewear.Round, Eyes = EyeStyle.Almond, Earrings = Earring.Pearl, Voice = 1 },
            // 2 행운의 루: 주황 재킷, 빨간 도미노 가면, 납작 모자
            new CharacterLook { Id = "lou", Skin = C("#e8b996"), Coat = C("#c8601e"), Trim = C("#f2c14e"), Shirt = C("#f7f2dc"), Pants = C("#3a2a1a"), MaskColor = C("#c41e2a"), MaskTrim = C("#f2c14e"),
                Hair = C("#b5651d"), HatColor = C("#6b4a2a"), Accent = C("#2f6f3a"), EyeColor = C("#6a4a2e"), Width = 1.0f, Height = 0.97f, Hat = HatStyle.FlatCap, HairStyle = HairStyle.Short,
                Mask = MaskStyle.Domino, Outfit = Outfit.Vest, Eyes = EyeStyle.Happy, Neck = Neckwear.Bowtie, Voice = 2 },
            // 3 교수: 갈색 트위드, 흰 수염, 흰 반가면, 옆머리, 둥근 안경
            new CharacterLook { Id = "professor", Skin = C("#efc9ae"), Coat = C("#6a4a2e"), Trim = C("#c9a76a"), Shirt = C("#f3efe6"), Pants = C("#3c2c1e"), MaskColor = C("#f3f0e8"), MaskTrim = C("#b89a5a"),
                Hair = C("#e6e3dc"), Accent = C("#7a1f1f"), EyeColor = C("#2a5aa8"), Width = 1.05f, Height = 0.98f, Hat = HatStyle.None, HairStyle = HairStyle.Tufts, Facial = FacialHair.Beard, Mask = MaskStyle.Half,
                Eyewear = Eyewear.Round, Eyes = EyeStyle.Sleepy, Neck = Neckwear.Necktie, Voice = 3 },
            // 4 벨벳 공작부인: 진홍 드레스, 깃털 흰 고양이눈 가면, 티아라, 긴 머리
            new CharacterLook { Id = "velvet", Skin = C("#f5d9c8"), Coat = C("#8e0f22"), Trim = C("#e7c35a"), Shirt = C("#f6e6e0"), Pants = C("#5a0a16"), MaskColor = C("#f6f3ee"), MaskTrim = C("#e7c35a"),
                Hair = C("#2a1a14"), Accent = C("#f0f0f0"), EyeColor = C("#7a3aa0"), Glove = C("#f6f3ee"), Width = 0.92f, Height = 1.04f, Hat = HatStyle.Tiara, HairStyle = HairStyle.Long, Mask = MaskStyle.CatEye,
                Outfit = Outfit.Gown, MaskFeather = true, Neck = Neckwear.Pearls, Eyes = EyeStyle.Almond, Earrings = Earring.Gold, Voice = 4 },
            // 5 늙은 까치: 검정·흰 깃털 망토, 부리 가면, 곱슬 가발
            new CharacterLook { Id = "magpie", Skin = C("#e3c0a4"), Coat = C("#16161c"), Trim = C("#e8e8ec"), Shirt = C("#ffffff"), Pants = C("#1a1a20"), MaskColor = C("#22222a"), MaskTrim = C("#4aa0c8"),
                Hair = C("#c9c9cc"), HatColor = C("#0e0e12"), Accent = C("#3a7fb0"), EyeColor = C("#4aa0c8"), Width = 0.98f, Height = 0.92f, Hat = HatStyle.Feathered, HairStyle = HairStyle.Wig, Mask = MaskStyle.Beak,
                Outfit = Outfit.Cloak, Eyes = EyeStyle.Sharp, Neck = Neckwear.Cravat, Voice = 5 },
            // 6 침묵의 여우: 회색 스웨터, 여우 가면, 귀 달린 두건
            new CharacterLook { Id = "fox", Skin = C("#efcdb4"), Coat = C("#4a4c56"), Trim = C("#b8bcc8"), Shirt = C("#20222a"), Pants = C("#2a2c34"), MaskColor = C("#e07a2a"), MaskTrim = C("#f6efe4"),
                Hair = C("#d9732a"), HatColor = C("#3a3c46"), Accent = C("#e07a2a"), EyeColor = C("#d9a441"), Glove = C("#2a2c34"), Width = 0.94f, Height = 1.0f, Hat = HatStyle.FoxHood, HairStyle = HairStyle.None,
                Mask = MaskStyle.Fox, Outfit = Outfit.Sweater, Eyes = EyeStyle.Sharp, Neck = Neckwear.None, Voice = 6 },
            // 7 루크 선장: 남색 제복과 금 견장, 선장 모자, 한쪽 눈 가리개, 수염
            new CharacterLook { Id = "rook", Skin = C("#d9a882"), Coat = C("#1b2a4e"), Trim = C("#e3b448"), Shirt = C("#f2efe6"), Pants = C("#141c34"), MaskColor = C("#101010"), MaskTrim = C("#e3b448"),
                Hair = C("#2a1e16"), HatColor = C("#151d36"), Accent = C("#e3b448"), EyeColor = C("#6a4a2e"), Width = 1.15f, Height = 1.02f, Hat = HatStyle.Captain, HairStyle = HairStyle.Short, Facial = FacialHair.Beard,
                Mask = MaskStyle.EyePatch, Outfit = Outfit.Uniform, Eyes = EyeStyle.Sharp, Neck = Neckwear.Medal, Voice = 7 },
        };

        /// <summary>꾸미기 화면의 빠른 시작 (온라인에서도 그대로 남에게 보임).</summary>
        public static readonly CharacterLook[] Players =
        {
            new CharacterLook { Id = "p_gold", Coat = C("#1a1a1f"), Trim = C("#e7c35a"), MaskColor = C("#e7c35a"), MaskTrim = C("#1a1a1f"), Hat = HatStyle.TopHat, Mask = MaskStyle.Domino, Voice = 0 },
            new CharacterLook { Id = "p_silver", Skin = C("#f3d3bf"), Coat = C("#2a3a6a"), Trim = C("#d8dbe6"), MaskColor = C("#d8dbe6"), MaskTrim = C("#2a3a6a"), Glove = C("#d8dbe6"), HairStyle = HairStyle.Long, Hat = HatStyle.Tiara,
                Mask = MaskStyle.CatEye, Outfit = Outfit.Gown, Neck = Neckwear.Pearls, Eyes = EyeStyle.Almond, EyeColor = C("#2a5aa8"), Earrings = Earring.Pearl, Voice = 4 },
            new CharacterLook { Id = "p_ruby", Skin = C("#e0b08e"), Coat = C("#6a0f1f"), Trim = C("#f2c14e"), MaskColor = C("#141414"), MaskTrim = C("#c41e2a"), Hat = HatStyle.Bowler, Mask = MaskStyle.Half,
                Facial = FacialHair.Mustache, Eyes = EyeStyle.Sharp, Neck = Neckwear.Necktie, Voice = 7 },
            new CharacterLook { Id = "p_emerald", Skin = C("#c98f6a"), Coat = C("#0f4a3a"), Trim = C("#e0c070"), MaskColor = C("#f3f0e8"), MaskTrim = C("#0f4a3a"), HairStyle = HairStyle.Bun, Mask = MaskStyle.Butterfly,
                Outfit = Outfit.Gown, Neck = Neckwear.None, Eyes = EyeStyle.Happy, EyeColor = C("#2f8a5a"), Earrings = Earring.Gold, Voice = 1 },
        };

        /// <summary>경매사 (무슈 개블).</summary>
        public static readonly CharacterLook Auctioneer = new CharacterLook
        {
            Id = "auctioneer", Skin = C("#f2cdb0"), Coat = C("#7a0c18"), Trim = C("#f0c75a"), Shirt = C("#fbf8f0"), Pants = C("#1a0a0e"),
            MaskColor = C("#fbf8f0"), MaskTrim = C("#f0c75a"), Hair = C("#f4f2ee"), Width = 1.05f, Height = 1.1f, HairStyle = HairStyle.Wig,
            Facial = FacialHair.CurlMustache, Mask = MaskStyle.Half, Outfit = Outfit.Uniform, Neck = Neckwear.Bowtie, Eyes = EyeStyle.Happy, EyeColor = C("#3a2416"), Voice = 8
        };
    }
}
