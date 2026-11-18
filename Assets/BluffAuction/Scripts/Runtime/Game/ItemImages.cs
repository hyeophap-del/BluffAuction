using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BluffAuction.Game
{
    /// <summary>
    /// 직접 쓴 물건에 넣은 그림 (물건 번호 → 그림). 한 판 동안만 둔다.
    /// 파일은 긴 변 512px 이하 JPG 로 줄여 주고받는다.
    /// </summary>
    public static class ItemImages
    {
        public const int MaxSide = 512;
        public const long MaxFileBytes = 20L * 1024 * 1024;
        static readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        static readonly Dictionary<int, byte[]> raw = new Dictionary<int, byte[]>();
        public static event Action<int> Changed;

        public static void Store(int item, byte[] jpg)
        {
            if (jpg == null || jpg.Length == 0) return;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "ItemImage_" + item, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            if (!t.LoadImage(jpg) || t.width < 4 || t.height < 4 || t.width > 4096 || t.height > 4096)
            {
                UnityEngine.Object.Destroy(t);
                return;
            }
            Texture2D old;
            if (textures.TryGetValue(item, out old) && old != null) UnityEngine.Object.Destroy(old);
            textures[item] = t;
            raw[item] = jpg;
            if (Changed != null) Changed(item);
        }

        /// <summary>보여 줄 그림 (남의 그림 보기를 끄면 내 것만).</summary>
        public static Texture2D Get(int item, int viewer)
        {
            Texture2D t;
            if (!textures.TryGetValue(item, out t) || t == null) return null;
            bool mine = Core.AuctionEngine.SellerOf(item) == viewer;
            return mine || SaveData.I.Settings.ShowCustomImages ? t : null;
        }

        public static bool Has(int item) { return textures.ContainsKey(item); }

        public static void Clear()
        {
            foreach (var t in textures.Values) if (t != null) UnityEngine.Object.Destroy(t);
            textures.Clear();
            raw.Clear();
        }

        /// <summary>그림 파일을 읽어 긴 변 512px 이하 JPG 로 줄인다. 실패하면 null 과 까닭.</summary>
        public static byte[] Prepare(string path, out Texture2D preview, out string error)
        {
            preview = null;
            error = null;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) { error = "missing"; return null; }
                if (info.Length > MaxFileBytes) { error = "too_big"; return null; }
                var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!src.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(src); error = "format"; return null; }
                float k = Mathf.Min(1f, MaxSide / (float)Mathf.Max(src.width, src.height));
                int w = Mathf.Max(4, Mathf.RoundToInt(src.width * k)), h = Mathf.Max(4, Mathf.RoundToInt(src.height * k));
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(src, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var small = new Texture2D(w, h, TextureFormat.RGB24, true);
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                small.Apply(true, false);
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(src);
                var jpg = small.EncodeToJPG(82);
                preview = small;
                return jpg;
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
        }
    }
}
