using System;
using System.Collections.Generic;

namespace BluffAuction.AI
{
    /// <summary>
    /// 판매자 한 사람의 시작가 습관 (시작가 / 진짜 가치). 처음에는 "보통 사람" 기본값에서 시작하고,
    /// 게임 끝 가치 공개와 내 능력 결과로 배운다. 허풍을 자주 치는 사람의 높은 시작가는 덜 믿게 된다.
    /// </summary>
    [Serializable]
    public sealed class SellerModel
    {
        public const double PriorMu = -0.8;        // ln(0.45)
        public const double PriorSigma = 0.38;
        public const double PriorN = 6;
        public const double PriorBluff = 0.15;
        public const double PriorSandbag = 0.03;
        public const double PriorW = 8;

        public double SumLog;
        public double SumLog2;
        public double N;
        public double Bluffs;
        public double Sandbags;
        public double Total;

        public double Mu { get { return (PriorMu * PriorN + SumLog) / (PriorN + N); } }

        public double Sigma
        {
            get
            {
                double mu = Mu;
                double prior = PriorN * (PriorSigma * PriorSigma + (PriorMu - mu) * (PriorMu - mu));
                double data = SumLog2 - 2 * mu * SumLog + N * mu * mu;
                double var = (prior + Math.Max(0, data)) / (PriorN + N);
                return Math.Max(0.15, Math.Sqrt(var));
            }
        }

        public double BluffWeight { get { return (PriorBluff * PriorW + Bluffs) / (PriorW + Total); } }
        public double SandbagWeight { get { return (PriorSandbag * PriorW + Sandbags) / (PriorW + Total); } }
        public double HonestWeight { get { return Math.Max(0.05, 1 - BluffWeight - SandbagWeight); } }

        public void Observe(int start, int value, double weight)
        {
            if (start <= 0 || value <= 0 || weight <= 0) return;
            double r = start / (double)value;
            Total += weight;
            if (r > 1.0) Bluffs += weight;
            else if (r < 0.08) Sandbags += weight;
            else
            {
                double l = Math.Log(r);
                SumLog += l * weight;
                SumLog2 += l * l * weight;
                N += weight;
            }
        }

        /// <summary>시작가 이하라는 것만 안다 (시세 비교에서 "시작가보다 낮다").</summary>
        public void ObserveBluff(double weight)
        {
            Total += weight;
            Bluffs += weight;
        }

        public SellerModel Clone() { return (SellerModel)MemberwiseClone(); }
    }

    /// <summary>판매자 이름 → 습관. 세션 동안 여러 판에 걸쳐 쌓인다 (AI 는 사람 플레이어의 허풍 버릇을 기억한다).</summary>
    public sealed class OpponentMemory
    {
        readonly Dictionary<string, SellerModel> models = new Dictionary<string, SellerModel>();

        public SellerModel Get(string seller)
        {
            SellerModel m;
            if (!models.TryGetValue(seller ?? "", out m)) models[seller ?? ""] = m = new SellerModel();
            return m;
        }

        public OpponentMemory Clone()
        {
            var c = new OpponentMemory();
            foreach (var kv in models) c.models[kv.Key] = kv.Value.Clone();
            return c;
        }

        public IEnumerable<KeyValuePair<string, SellerModel>> All { get { return models; } }
    }
}
