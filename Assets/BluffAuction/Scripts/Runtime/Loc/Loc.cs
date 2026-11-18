using System;
using System.Collections.Generic;
using UnityEngine;

namespace BluffAuction.Loc
{
    public enum Lang
    {
        Korean = 0,
        English = 1,
        Japanese = 2,
        ChineseTraditional = 3,
        ChineseSimplified = 4
    }

    /// <summary>
    /// 화면 문구. 키 → [한국어, English, 日本語, 繁體中文, 简体中文]. 빈 칸은 English → 한국어 순서로 대신 쓴다.
    /// 문구 표는 Strings.cs (부분 클래스) 에 있다.
    /// </summary>
    public static partial class L
    {
        public static Lang Current = Lang.English;
        public static event Action Changed;
        static Dictionary<string, string[]> table;

        public static readonly string[] LanguageNames = { "한국어", "English", "日本語", "繁體中文", "简体中文" };

        static void Ensure()
        {
            if (table != null) return;
            table = new Dictionary<string, string[]>();
            Fill(table);
            FillAsia(table);
        }

        public static void Set(Lang lang)
        {
            if (Current == lang) return;
            Current = lang;
            if (Changed != null) Changed();
        }

        /// <summary>시스템 언어로 처음 언어를 고른다.</summary>
        public static Lang FromSystem()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Korean: return Lang.Korean;
                case SystemLanguage.Japanese: return Lang.Japanese;
                case SystemLanguage.ChineseTraditional: return Lang.ChineseTraditional;
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.Chinese: return Lang.ChineseSimplified;
                default: return Lang.English;
            }
        }

        public static bool Has(string key)
        {
            Ensure();
            return table.ContainsKey(key);
        }

        public static string T(string key)
        {
            Ensure();
            string[] row;
            if (!table.TryGetValue(key, out row)) return key;
            int i = (int)Current;
            if (i < row.Length && !string.IsNullOrEmpty(row[i])) return row[i];
            if (row.Length > 1 && !string.IsNullOrEmpty(row[1])) return row[1];
            return row.Length > 0 ? row[0] : key;
        }

        public static string T(string key, params object[] args)
        {
            string f = T(key);
            try { return string.Format(f, args); }
            catch (FormatException) { return f; }
        }

        /// <summary>금액 표기: 1,234G</summary>
        public static string G(int amount) { return amount.ToString("#,0") + "G"; }

        public static string G(long amount) { return amount.ToString("#,0") + "G"; }

        /// <summary>부호 붙인 금액 (+1,200G / −800G)</summary>
        public static string Signed(int amount) { return (amount >= 0 ? "+" : "−") + Math.Abs(amount).ToString("#,0") + "G"; }

        static void Add(Dictionary<string, string[]> t, string key, string ko, string en, string ja = null, string zht = null, string zhs = null)
        {
            t[key] = new[] { ko, en, ja, zht, zhs };
        }
    }
}
