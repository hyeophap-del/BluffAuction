using System;

namespace BluffAuction.Core
{
    /// <summary>
    /// 한 판의 규칙. -1 인 항목은 인원수에 맞춘 자동값이며 <see cref="Resolve"/> 가 실제 값으로 바꾼다.
    /// VRChat 월드판(허풍 경매장)의 규칙을 그대로 따르고, PC판에 맞게 시간 기본값만 줄였다.
    /// </summary>
    [Serializable]
    public sealed class GameRules
    {
        public const int MaxSeats = 8;
        public const int MaxItemsPerPlayer = 4;
        public const int MinPlayers = 3;
        public const int MaxItems = MaxSeats * MaxItemsPerPlayer;

        // 물건·자금
        public int ItemsPerPlayer = 3;
        public int RegistrationFund = 20000;
        public int BuyFund = -1;
        public int MoneyUnit = 100;
        public int MinItemValue = 500;
        public int MinStartPrice = 100;
        public int MaxStartPrice = 1000000;
        public int MinBidIncrement = 100;
        public int MaxBid = 100000000;

        // 자동 구매 자금: 3명 8,000G 에서 한 명마다 1,000G, 등록 자금에 비례, 500G 단위
        public int AutoBuyBase = 8000;
        public int AutoBuyBasePlayers = 3;
        public int AutoBuyPerPlayer = 1000;
        public int BuyFundStep = 500;
        public int DefaultRegistrationFund = 20000;

        // 능력 (-1 자동: 3~5명 3·2·1, 6명 이상 4·3·2)
        public int CompareUses = -1;
        public int AppraiseUses = -1;
        public int FakeUses = -1;
        public int AppraisalWidthMin = 600;
        public int AppraisalWidthMax = 2500;

        public bool BlindMode;

        // 대출 (게임 중 한 번, 끝날 때 원금 + 이자를 점수에서 뺀다)
        public int LoanAmount = 10000;
        public int LoanInterest = 3000;

        // 점수: 처음 받은 살 돈(+대출) 가운데 안 쓴 돈은 Keep 비율만 점수 (1/2)
        public int UnspentKeepNum = 1;
        public int UnspentKeepDen = 2;
        // 판매자가 포기한 물건 1/2, 유찰품 경매에서도 안 팔린 물건 1/3
        public int UnsoldKeepNum = 1;
        public int UnsoldKeepDen = 2;
        public int LeftoverFailKeepNum = 1;
        public int LeftoverFailKeepDen = 3;
        // 안 팔린 물건의 시작가가 구매 자금을 넘는 만큼의 % 를 판매자 점수에서 뺀다 (일부러 유찰시키는 꼼수 방지)
        public int HighStartFeePercent = 100;

        // 시간
        public int RegistrationSeconds = 0;      // 0 이면 제한 없음 (온라인에서는 늦은 사람을 자동 등록)
        public float PresentSeconds = 3.2f;      // 물건 소개 (입찰 전)
        public int BidSeconds = 30;
        public int ExtendSeconds = 5;
        public int MaxExtensions = 0;
        public int SkipToSeconds = 3;
        public int PriceDropSeconds = 15;
        public int ReauctionSeconds = 20;
        public float ResultSeconds = 3.6f;
        public float RevealIntervalSeconds = 2.0f;

        public bool AllowDuplicateAppearance;
        public int AppearanceCount = 12;
        public int PhraseCount = 20;
        public int CustomNameMaxLength = 20;
        public int CustomPhraseMaxLength = 60;
        public int SayMaxLength = 40;
        public int SayCooldownMs = 1500;

        public GameRules Clone()
        {
            return (GameRules)MemberwiseClone();
        }

        public int AutoBuyFund(int players, int regFund)
        {
            int extra = Math.Max(0, players - AutoBuyBasePlayers);
            double v = AutoBuyBase + extra * AutoBuyPerPlayer;
            if (DefaultRegistrationFund > 0 && regFund > 0) v = v * regFund / DefaultRegistrationFund;
            int step = BuyFundStep > 0 ? BuyFundStep : 500;
            int r = ((int)Math.Round(v) + step / 2) / step * step;
            return r < step ? step : r;
        }

        public static int AutoCompare(int n) { return n >= 6 ? 4 : 3; }
        public static int AutoAppraise(int n) { return n >= 6 ? 3 : 2; }
        public static int AutoFake(int n) { return n >= 6 ? 2 : 1; }

        /// <summary>자동 항목을 인원수로 채운 이번 판의 규칙.</summary>
        public GameRules Resolve(int players)
        {
            var r = Clone();
            r.ItemsPerPlayer = Clamp(ItemsPerPlayer, 1, MaxItemsPerPlayer);
            if (r.BuyFund < 0) r.BuyFund = AutoBuyFund(players, RegistrationFund);
            if (r.CompareUses < 0) r.CompareUses = AutoCompare(players);
            if (r.AppraiseUses < 0) r.AppraiseUses = AutoAppraise(players);
            if (r.FakeUses < 0) r.FakeUses = AutoFake(players);
            if (r.LoanAmount < 0) r.LoanAmount = 0;
            return r;
        }

        /// <summary>등록할 수 있는 최소 가치 (금액 단위로 올림).</summary>
        public int EffectiveMinValue
        {
            get { return (MinItemValue + MoneyUnit - 1) / MoneyUnit * MoneyUnit; }
        }

        /// <summary>규칙이 맞지 않으면 이유, 맞으면 빈 문자열.</summary>
        public string Validate(int players)
        {
            if (players < 1 || players > MaxSeats) return "players";
            if (MoneyUnit <= 0) return "unit";
            if (RegistrationFund <= 0 || RegistrationFund % MoneyUnit != 0) return "regfund";
            if (EffectiveMinValue * ItemsPerPlayer > RegistrationFund) return "regfund-small";
            if (MinStartPrice <= 0 || MinStartPrice % MoneyUnit != 0) return "minstart";
            if (MinBidIncrement <= 0) return "increment";
            if (AppraisalWidthMin <= 0 || AppraisalWidthMax < AppraisalWidthMin) return "appraisal";
            if (UnspentKeepDen <= 0 || UnsoldKeepDen <= 0 || LeftoverFailKeepDen <= 0) return "ratio";
            if (BidSeconds <= 0 || PriceDropSeconds <= 0 || ReauctionSeconds <= 0) return "time";
            if (AppearanceCount < 1 || (!AllowDuplicateAppearance && AppearanceCount < ItemsPerPlayer)) return "appearance";
            return "";
        }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}
