namespace BluffAuction.Core
{
    /// <summary>문구·한마디의 성격 (글자는 화면 쪽 번역 표에 있고, 여기에는 AI 가 쓰는 의미만 둔다).</summary>
    public static class Content
    {
        public const int AppearanceCount = 12;
        public const int PhraseCount = 20;

        /// <summary>물건 문구의 인상: +1 좋은 이야기, -1 나쁜 이야기, 0 애매함. (VRChat판 문구 20종과 같은 순서)</summary>
        public static readonly int[] PhraseSentiment =
        {
            -1, 0, 1, 1, -1, -1, -1, 1, 1, -1,
            0, 0, 0, 1, 0, 0, 1, -1, 1, 0
        };

        // 빠른 한마디 묶음 (번호 범위)
        public const int LineHypeFirst = 0, LineHypeCount = 6;          // 판매자 자랑
        public const int LineTauntFirst = 6, LineTauntCount = 4;        // 도발
        public const int LineDoubtFirst = 10, LineDoubtCount = 3;       // 남의 물건 깎아내리기
        public const int LineWinFirst = 13, LineWinCount = 3;           // 낙찰 기쁨
        public const int LineBluffFirst = 16, LineBluffCount = 3;       // 아는 척
        public const int LineLamentFirst = 19, LineLamentCount = 2;     // 아쉬움
        public const int LineCount = 21;
    }
}
