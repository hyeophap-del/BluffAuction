using System;
using BluffAuction.Core;

namespace BluffAuction.AI
{
    /// <summary>
    /// 한 물건의 진짜 가치에 대한 확률 분포 (금액 칸 단위). 사전 분포(판매자의 등록 자금 나누기) ×
    /// 시작가 모형 × 문구 × 내 능력 결과(딱 맞는 제약)를 곱해 만든다.
    /// </summary>
    public sealed class ValueBelief
    {
        public readonly int BinSize;
        public readonly int Bins;
        public readonly double[] P;

        public ValueBelief(GameRules r)
        {
            int unit = Math.Max(1, r.MoneyUnit);
            int size = unit;
            while (r.RegistrationFund / size > 400) size += unit;
            BinSize = size;
            Bins = r.RegistrationFund / size + 1;
            P = new double[Bins];
        }

        public int ValueAt(int i) { return i * BinSize; }

        public void CopyFrom(ValueBelief o) { Array.Copy(o.P, P, Bins); }

        /// <summary>
        /// 판매자 한 명이 등록 자금 fund 를 물건 k 개에 (각 최소 min) 나눌 때 한 물건의 가치 분포.
        /// 고르게 무작위로 나눈 경우(디리클레) 와 한 물건에 몰아넣는 경우(균등)를 섞는다.
        /// </summary>
        public void SetPrior(int fund, int k, int min, double jackpotWeight)
        {
            if (k < 1) k = 1;
            double span = fund - k * (double)min;
            double maxV = fund - (k - 1) * (double)min;
            for (int i = 0; i < Bins; i++)
            {
                double v = ValueAt(i);
                if (v < min - 1e-6 || v > maxV + 1e-6) { P[i] = 0; continue; }
                double dir;
                if (k == 1 || span <= 0) dir = 1.0;
                else
                {
                    double t = (v - min) / span;
                    if (t < 0) t = 0;
                    if (t > 1) t = 1;
                    dir = k * Math.Pow(1 - t, k - 2);
                }
                P[i] = (1 - jackpotWeight) * dir + jackpotWeight * 1.0;
            }
            if (k == 1)
            {
                // 남은 물건이 하나뿐이면 가치는 거의 정해진다 (추정이므로 둘레를 조금 둔다)
                for (int i = 0; i < Bins; i++)
                {
                    double d = (ValueAt(i) - maxV) / Math.Max(1.0, fund * 0.08);
                    P[i] = Math.Exp(-d * d);
                }
            }
            Normalize();
        }

        /// <summary>
        /// 시작가 s 를 본 뒤의 갱신. 판매자 대부분은 가치보다 낮게(정직), 일부는 가치보다 높게(허풍),
        /// 아주 일부는 터무니없이 낮게(숨기기) 정한다. temper 가 작을수록 이 모형을 덜 믿는다.
        /// </summary>
        public void ApplyStartPrice(int s, double temper)
        {
            ApplyStartPrice(s, temper, null);
        }

        /// <summary>시작가 s 를 본 뒤의 갱신. seller 가 있으면 그 판매자의 습관(허풍 비율·시작가 비율)을 쓴다.</summary>
        public void ApplyStartPrice(int s, double temper, SellerModel seller)
        {
            if (s <= 0) return;
            double mu = seller != null ? seller.Mu : SellerModel.PriorMu;
            double sigma = seller != null ? seller.Sigma : SellerModel.PriorSigma;
            double wb = seller != null ? seller.BluffWeight : SellerModel.PriorBluff;
            double ws = seller != null ? seller.SandbagWeight : SellerModel.PriorSandbag;
            double wh = Math.Max(0.05, 1 - wb - ws);
            for (int i = 0; i < Bins; i++)
            {
                double v = Math.Max(ValueAt(i), 1);
                double r = s / v;
                // 시작가 s 가 나올 확률 f(s|V) = f_r(s/V) / V.
                // 정직: 시작가/가치 비율이 로그 정규 / 허풍: 가치보다 높게 / 숨기기: 가치의 8% 미만
                double z = (Math.Log(r) - mu) / sigma;
                double honest = Math.Exp(-0.5 * z * z) / (2.5066 * sigma * s);
                double bluff = r > 1.0 && r <= 2.5 ? 1.0 / (1.5 * v) : 0;
                double sandbag = r <= 0.08 ? 12.5 / v : 0;
                double l = wh * honest + wb * bluff + ws * sandbag + 0.0005 / v;
                P[i] *= Math.Pow(l, temper);
            }
            Normalize();
        }

        static double Sq(double x) { return x * x; }

        /// <summary>value &gt; threshold 쪽에 above, 이하 쪽에 below 를 곱한다 (부드러운 증거).</summary>
        public void Tilt(int threshold, double above, double below)
        {
            for (int i = 0; i < Bins; i++) P[i] *= ValueAt(i) > threshold ? above : below;
            Normalize();
        }

        /// <summary>좋은 문구면 비싼 쪽, 나쁜 문구면 싼 쪽으로 조금 기운다.</summary>
        public void ApplyPhrase(int sentiment, double gullible, double typical)
        {
            if (sentiment == 0 || gullible <= 0) return;
            for (int i = 0; i < Bins; i++)
            {
                double rel = (ValueAt(i) - typical) / Math.Max(1.0, typical);
                double f = 1 + sentiment * gullible * 0.45 * Math.Tanh(rel);
                P[i] *= Math.Max(0.05, f);
            }
            Normalize();
        }

        /// <summary>[lo, hi] 밖을 지운다 (딱 맞는 정보).</summary>
        public void Restrict(int lo, int hi)
        {
            double sum = 0;
            for (int i = 0; i < Bins; i++)
            {
                int v = ValueAt(i);
                if (v < lo || v > hi) P[i] = 0;
                sum += P[i];
            }
            if (sum <= 1e-12)
            {
                // 모형이 틀렸다: 제약 안에서 고르게
                for (int i = 0; i < Bins; i++)
                {
                    int v = ValueAt(i);
                    P[i] = v >= lo && v <= hi ? 1 : 0;
                }
            }
            Normalize();
        }

        public void Normalize()
        {
            double sum = 0;
            for (int i = 0; i < Bins; i++) sum += P[i];
            if (sum <= 1e-300)
            {
                for (int i = 0; i < Bins; i++) P[i] = 1.0 / Bins;
                return;
            }
            for (int i = 0; i < Bins; i++) P[i] /= sum;
        }

        public double Mean()
        {
            double m = 0;
            for (int i = 0; i < Bins; i++) m += P[i] * ValueAt(i);
            return m;
        }

        public double StdDev()
        {
            double m = Mean();
            double v = 0;
            for (int i = 0; i < Bins; i++)
            {
                double d = ValueAt(i) - m;
                v += P[i] * d * d;
            }
            return Math.Sqrt(v);
        }

        public double Quantile(double q)
        {
            double acc = 0;
            for (int i = 0; i < Bins; i++)
            {
                acc += P[i];
                if (acc >= q) return ValueAt(i);
            }
            return ValueAt(Bins - 1);
        }

        /// <summary>가치가 x 보다 클 확률.</summary>
        public double ProbAbove(int x)
        {
            double acc = 0;
            for (int i = 0; i < Bins; i++) if (ValueAt(i) > x) acc += P[i];
            return acc;
        }
    }
}
