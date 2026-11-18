using System;
using System.Collections.Generic;

namespace BluffAuction.AI
{
    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    /// <summary>AI 상대의 성격. 값은 0~1 사이가 기본이며, 판매(등록·허풍)와 구매(입찰·능력) 성향을 함께 정한다.</summary>
    [Serializable]
    public sealed class AiProfile
    {
        public string Id = "";
        public int CharacterId;

        // ── 판매자
        public float StartRatioMin = 0.25f;     // 시작가 = 가치 × 이 범위
        public float StartRatioMax = 0.65f;
        public float BluffChance = 0.15f;       // 싼 물건에 높은 시작가 / 비싼 물건에 낮은 시작가
        public float Concentration = 0.3f;      // 0 고르게, 1 한 물건에 몰기
        public float PhraseHonesty = 0.6f;      // 문구를 가치에 맞게 고를 확률
        public float Chattiness = 0.5f;

        // ── 구매자
        public float Greed = 0.85f;             // 이론상 한도에 얼마나 붙어 부르는지
        public float Caution = 0.3f;            // 낮은 쪽 추정에 두는 무게
        public float Impulse = 0.1f;            // 가끔 충동적으로 더 부름
        public float JumpBid = 0.2f;            // 최소가보다 크게 올려 부르기
        public float Snipe = 0.2f;              // 마감 직전에 부르기
        public float AbilityRate = 0.5f;
        public float FakeRate = 0.3f;
        public float Herding = 0.4f;            // 남의 입찰·능력 사용을 신호로 믿음
        public float Gullible = 0.2f;           // 문구를 믿음
        public int FavoriteAppearance = -1;     // 좋아하는 외형 (-2 이면 게임마다 무작위)
        public float FavoriteBonus = 0.25f;
        public float ReactMin = 0.7f;
        public float ReactMax = 2.4f;
        public float LoanChance = 0.15f;
        public float Skill = 0.7f;              // 추론 솜씨 (시작가 모형·같은 판매자 합계·남의 능력 신호)

        public AiProfile Clone() { return (AiProfile)MemberwiseClone(); }
    }

    public static class AiProfiles
    {
        public static readonly AiProfile[] All =
        {
            new AiProfile
            {
                Id = "baron", CharacterId = 0,
                StartRatioMin = 0.35f, StartRatioMax = 0.9f, BluffChance = 0.45f, Concentration = 0.55f, PhraseHonesty = 0.25f, Chattiness = 0.95f,
                Greed = 0.95f, Caution = 0.12f, Impulse = 0.2f, JumpBid = 0.4f, Snipe = 0.1f, AbilityRate = 0.3f, FakeRate = 0.85f,
                Herding = 0.3f, Gullible = 0.15f, ReactMin = 0.5f, ReactMax = 1.8f, LoanChance = 0.35f, Skill = 0.6f
            },
            new AiProfile
            {
                Id = "ledger", CharacterId = 1,
                StartRatioMin = 0.3f, StartRatioMax = 0.6f, BluffChance = 0.05f, Concentration = 0.15f, PhraseHonesty = 0.85f, Chattiness = 0.35f,
                Greed = 0.82f, Caution = 0.5f, Impulse = 0.02f, JumpBid = 0.1f, Snipe = 0.25f, AbilityRate = 0.95f, FakeRate = 0.1f,
                Herding = 0.25f, Gullible = 0.05f, ReactMin = 1.0f, ReactMax = 2.8f, LoanChance = 0.05f, Skill = 0.95f
            },
            new AiProfile
            {
                Id = "lou", CharacterId = 2,
                StartRatioMin = 0.1f, StartRatioMax = 1.1f, BluffChance = 0.25f, Concentration = 0.7f, PhraseHonesty = 0.5f, Chattiness = 0.8f,
                Greed = 1.0f, Caution = 0.0f, Impulse = 0.4f, JumpBid = 0.35f, Snipe = 0.0f, AbilityRate = 0.3f, FakeRate = 0.4f,
                Herding = 0.55f, Gullible = 0.65f, ReactMin = 0.4f, ReactMax = 1.6f, LoanChance = 0.5f, Skill = 0.35f
            },
            new AiProfile
            {
                Id = "professor", CharacterId = 3,
                StartRatioMin = 0.4f, StartRatioMax = 0.7f, BluffChance = 0.03f, Concentration = 0.25f, PhraseHonesty = 0.95f, Chattiness = 0.45f,
                Greed = 0.85f, Caution = 0.4f, Impulse = 0.03f, JumpBid = 0.15f, Snipe = 0.15f, AbilityRate = 1.0f, FakeRate = 0.05f,
                Herding = 0.1f, Gullible = 0.1f, ReactMin = 1.2f, ReactMax = 3.0f, LoanChance = 0.05f, Skill = 0.9f
            },
            new AiProfile
            {
                Id = "velvet", CharacterId = 4,
                StartRatioMin = 0.3f, StartRatioMax = 0.75f, BluffChance = 0.25f, Concentration = 0.35f, PhraseHonesty = 0.4f, Chattiness = 0.6f,
                Greed = 0.9f, Caution = 0.25f, Impulse = 0.08f, JumpBid = 0.25f, Snipe = 0.85f, AbilityRate = 0.55f, FakeRate = 0.45f,
                Herding = 0.35f, Gullible = 0.2f, FavoriteAppearance = 8, FavoriteBonus = 0.3f, ReactMin = 0.6f, ReactMax = 2.0f, LoanChance = 0.15f, Skill = 0.75f
            },
            new AiProfile
            {
                Id = "magpie", CharacterId = 5,
                StartRatioMin = 0.25f, StartRatioMax = 0.6f, BluffChance = 0.15f, Concentration = 0.3f, PhraseHonesty = 0.6f, Chattiness = 0.55f,
                Greed = 0.9f, Caution = 0.2f, Impulse = 0.15f, JumpBid = 0.2f, Snipe = 0.2f, AbilityRate = 0.45f, FakeRate = 0.25f,
                Herding = 0.45f, Gullible = 0.45f, FavoriteAppearance = -2, FavoriteBonus = 0.7f, ReactMin = 0.8f, ReactMax = 2.4f, LoanChance = 0.25f, Skill = 0.6f
            },
            new AiProfile
            {
                Id = "fox", CharacterId = 6,
                StartRatioMin = 0.2f, StartRatioMax = 0.8f, BluffChance = 0.35f, Concentration = 0.4f, PhraseHonesty = 0.3f, Chattiness = 0.08f,
                Greed = 0.88f, Caution = 0.3f, Impulse = 0.05f, JumpBid = 0.1f, Snipe = 0.55f, AbilityRate = 0.6f, FakeRate = 0.9f,
                Herding = 0.6f, Gullible = 0.1f, ReactMin = 0.9f, ReactMax = 2.6f, LoanChance = 0.1f, Skill = 0.85f
            },
            new AiProfile
            {
                Id = "rook", CharacterId = 7,
                StartRatioMin = 0.3f, StartRatioMax = 0.7f, BluffChance = 0.2f, Concentration = 0.45f, PhraseHonesty = 0.5f, Chattiness = 0.75f,
                Greed = 0.95f, Caution = 0.1f, Impulse = 0.25f, JumpBid = 0.6f, Snipe = 0.05f, AbilityRate = 0.4f, FakeRate = 0.3f,
                Herding = 0.3f, Gullible = 0.25f, ReactMin = 0.4f, ReactMax = 1.5f, LoanChance = 0.55f, Skill = 0.65f
            },
        };

        public static AiProfile Get(string id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return All[0];
        }

        /// <summary>난이도에 맞춰 솜씨와 실수를 조정한 사본.</summary>
        public static AiProfile ForDifficulty(AiProfile p, Difficulty d)
        {
            var c = p.Clone();
            switch (d)
            {
                case Difficulty.Easy:
                    c.Skill = Math.Min(c.Skill, 0.35f) * 0.8f;
                    c.Greed *= 0.85f;
                    c.AbilityRate *= 0.6f;
                    c.Impulse = Math.Min(1f, c.Impulse + 0.15f);
                    c.Gullible = Math.Min(1f, c.Gullible + 0.3f);
                    break;
                case Difficulty.Hard:
                    c.Skill = Math.Min(1f, c.Skill * 0.5f + 0.55f);
                    c.Impulse *= 0.5f;
                    c.Gullible *= 0.4f;
                    c.AbilityRate = Math.Min(1f, c.AbilityRate + 0.2f);
                    break;
            }
            return c;
        }

        public static List<string> Ids()
        {
            var l = new List<string>();
            foreach (var p in All) l.Add(p.Id);
            return l;
        }
    }
}
