using System;
using System.Collections.Generic;
using System.IO;
using BluffAuction.AI;
using UnityEngine;

namespace BluffAuction.Game
{
    [Serializable]
    public sealed class SettingsData
    {
        public int Language = -1;          // -1 이면 시스템 언어
        public float Master = 0.9f;
        public float Music = 0.55f;
        public float Sfx = 0.85f;
        public float Voice = 0.8f;
        public float Ambience = 0.5f;
        public bool Fullscreen = true;
        public int ResWidth;
        public int ResHeight;
        public bool VSync = true;
        public int Quality = 2;
        public bool Dof = true;
        public bool Grain = true;
        public float CameraMotion = 1f;
        public float UiScale = 1f;
        public bool Hints = true;
        public float AiSpeed = 1f;
        public bool MouseLook = true;
        public float Sensitivity = 1f;
        public bool InvertY;
        // ── 어울리기
        public float ChatVolume = 1f;       // 다른 사람 목소리 크기
        public int MicMode;                 // 0 누르고 말하기(V) · 1 켜 두기(M 으로 켜고 끔)
        public string MicDevice = "";
        public float MicThreshold = 0.02f;  // 켜 두기에서 말로 알아듣는 크기
        public bool ShowRude = true;        // 남의 가운데 손가락을 그대로 보기 (끄면 화내기로)
        public bool ShowCustomImages = true; // 남이 넣은 물건 그림 보기
    }

    [Serializable]
    public sealed class StatsData
    {
        public int Games;
        public int Wins;
        public int BestScore = int.MinValue;
        public int BestBargain;
        public List<string> Achievements = new List<string>();
    }

    [Serializable]
    public sealed class LastSetup
    {
        public string Name = "";
        public int Look;
        /// <summary>꾸미기 화면에서 만든 내 생김새.</summary>
        public Characters.CharacterLook Custom = Characters.CharacterLook.DefaultPlayer();
        public int Rivals = 3;
        public int Difficulty = 1;
        public int Items = 3;
        public int BidSeconds = 30;
        public int Extend = 5;
        public bool Blind;
        public bool Loan = true;
        public List<string> RivalIds = new List<string>();
    }

    [Serializable]
    public sealed class MemoryEntry
    {
        public string Name;
        public SellerModel Model;
    }

    /// <summary>저장 파일 (persistentDataPath/save.json). 설정·기록·마지막 게임 설정·AI 가 배운 판매자 습관.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int Version = 1;
        public SettingsData Settings = new SettingsData();
        public StatsData Stats = new StatsData();
        public LastSetup Last = new LastSetup();
        public List<MemoryEntry> Memory = new List<MemoryEntry>();
        public bool SeenTutorial;

        static SaveData current;
        static string PathOf { get { return System.IO.Path.Combine(Application.persistentDataPath, "save.json"); } }

        /// <summary>내 생김새 (꾸미기 화면에서 고친 것).</summary>
        public static Characters.CharacterLook MyLook { get { return I.Last.Custom; } }

        public static SaveData I
        {
            get
            {
                if (current == null) Load();
                return current;
            }
        }

        static void Load()
        {
            try
            {
                if (File.Exists(PathOf)) current = JsonUtility.FromJson<SaveData>(File.ReadAllText(PathOf));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] load failed: " + e.Message);
            }
            if (current == null) current = new SaveData();
            if (current.Settings == null) current.Settings = new SettingsData();
            if (current.Stats == null) current.Stats = new StatsData();
            if (current.Last == null) current.Last = new LastSetup();
            // 꾸민 생김새는 범위를 다시 확인한다 (예전 저장 파일에는 없다)
            var look = current.Last.Custom != null ? Characters.CharacterLook.FromJson(current.Last.Custom.ToJson()) : null;
            current.Last.Custom = look ?? Characters.CharacterLook.DefaultPlayer();
            if (current.Memory == null) current.Memory = new List<MemoryEntry>();
            // AI 의 기억을 되살린다
            var mem = new OpponentMemory();
            foreach (var m in current.Memory)
                if (m != null && m.Model != null && !string.IsNullOrEmpty(m.Name))
                {
                    var target = mem.Get(m.Name);
                    target.SumLog = m.Model.SumLog;
                    target.SumLog2 = m.Model.SumLog2;
                    target.N = m.Model.N;
                    target.Bluffs = m.Model.Bluffs;
                    target.Sandbags = m.Model.Sandbags;
                    target.Total = m.Model.Total;
                }
            GameSession.Memory = mem;
        }

        public static void Save()
        {
            // 자동 시험(-autoshots)은 사용자의 기록·업적·이름을 건드리지 않는다
            if (current == null || Dev.AutoShots.Requested) return;
            current.Memory.Clear();
            foreach (var kv in GameSession.Memory.All) current.Memory.Add(new MemoryEntry { Name = kv.Key, Model = kv.Value });
            try
            {
                string tmp = PathOf + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(current, true));
                if (File.Exists(PathOf)) File.Delete(PathOf);
                File.Move(tmp, PathOf);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] save failed: " + e.Message);
            }
        }
    }
}
