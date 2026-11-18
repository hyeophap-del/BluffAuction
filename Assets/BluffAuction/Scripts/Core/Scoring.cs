namespace BluffAuction.Core
{
    /// <summary>점수 계산식. 엔진(최종 점수)과 화면(지금 끝나면 벌금 얼마) · AI(입찰 한도)가 같은 식을 쓴다.</summary>
    public static class Scoring
    {
        /// <summary>처음 받은 살 돈(+대출) 가운데 아직 안 쓴 돈. 판 돈은 들어가지 않는다.</summary>
        public static int UnspentBudget(GameRules r, int spent, bool loan)
        {
            int budget = r.BuyFund + (loan ? r.LoanAmount : 0);
            int left = budget - spent;
            return left > 0 ? left : 0;
        }

        /// <summary>안 쓴 살 돈 벌금 (기본 1/2, 소수점 버림).</summary>
        public static int CashPenalty(GameRules r, int spent, bool loan)
        {
            if (r.UnspentKeepDen <= 0) return 0;
            return UnspentBudget(r, spent, loan) * (r.UnspentKeepDen - r.UnspentKeepNum) / r.UnspentKeepDen;
        }

        /// <summary>벌금에서 잃는 비율 (기본 0.5). 1 − 이 값만큼 안 쓴 돈이 점수로 남는다.</summary>
        public static double PenaltyFraction(GameRules r)
        {
            return r.UnspentKeepDen <= 0 ? 0.0 : (double)(r.UnspentKeepDen - r.UnspentKeepNum) / r.UnspentKeepDen;
        }

        public static int LoanRepay(GameRules r, bool loan)
        {
            return loan ? r.LoanAmount + r.LoanInterest : 0;
        }

        /// <summary>시작가 price 로 안 팔렸을 때의 높은 시작가 수수료.</summary>
        public static int StartFeeFor(GameRules r, int price)
        {
            if (r.HighStartFeePercent <= 0) return 0;
            int over = price - r.BuyFund;
            if (over <= 0) return 0;
            return (int)((long)over * r.HighStartFeePercent / 100);
        }

        /// <summary>주인에게 돌아간 물건 몫: 포기 1/2, 유찰품 경매에서도 안 팔림 1/3.</summary>
        public static int UnsoldKeep(GameRules r, int value, bool leftoverFailed)
        {
            if (value <= 0) return 0;
            if (leftoverFailed)
                return r.LeftoverFailKeepDen <= 0 ? 0 : value * r.LeftoverFailKeepNum / r.LeftoverFailKeepDen;
            return r.UnsoldKeepDen <= 0 ? 0 : value * r.UnsoldKeepNum / r.UnsoldKeepDen;
        }

        /// <summary>안 팔린 물건이 판매자 점수에 더하는 값 = 몫 − 수수료 (음수일 수 있음).</summary>
        public static int UnsoldNet(GameRules r, int value, bool leftoverFailed, int lastStartPrice)
        {
            return UnsoldKeep(r, value, leftoverFailed) - StartFeeFor(r, lastStartPrice);
        }
    }
}
