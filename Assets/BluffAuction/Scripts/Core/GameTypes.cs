using System;
using System.Collections.Generic;

namespace BluffAuction.Core
{
    public enum Phase
    {
        Setup,
        Registration,
        Presenting,     // 물건 소개 (외형·문구·시작가 공개, 아직 입찰 불가)
        Bidding,
        PriceDrop,      // 첫 경매 무입찰: 판매자가 유찰품 경매 시작가를 정함
        Reauction,      // 바퀴 마지막 유찰품 경매
        AuctionResult,
        Reveal,
        Results,
        Aborted
    }

    public enum ItemStatus
    {
        None,
        Pending,    // 등록됨, 경매 전
        Active,     // 경매 중
        Sold,
        Unsold,     // 주인에게 (포기 1/2, 유찰품 경매에서도 안 팔림 1/3)
        Excluded,   // 경매 전에 제외
        Deferred,   // 유찰품 경매 대기
        Returned    // 모두 동의해 일찍 끝냄 → 가치 그대로 주인에게
    }

    public enum AbilityKind
    {
        Compare = 0,
        Appraise = 1,
        Fake = 2
    }

    public enum Result
    {
        Ok,
        WrongPhase,
        NotParticipant,
        IsSeller,
        TooLow,
        Insufficient,
        TimeOver,
        NoUses,
        Invalid,
        Already,
        BelowMin,
        SumMismatch,
        Unit,
        StartRange,
        DuplicateAppearance,
        NotSeller,
        AlreadyHighest,
        NotAllowed,
        Cooldown,
        TooLong
    }

    public enum EventType
    {
        GameStarted,
        Registered,         // Seat
        LotPresented,       // Seat=판매자, Item, A=시작가, B=1이면 유찰품 경매
        BiddingOpened,      // Item, A=마감까지 ms
        BidPlaced,          // Seat, Item, A=금액, B=이전 최고 입찰자(-1 없음), C=1이면 연장
        AbilityUsed,        // Seat, Item (종류는 공개하지 않음)
        AbilityResult,      // 본인만: Seat, Item, A, B, C=종류. 시세 비교 A=기준가 B=부호 / 간이감정 A~B 범위
        NoBids,             // Seat=판매자, Item → 판매자가 시작가를 정하는 단계
        Deferred,           // Seat=판매자, Item, A=유찰품 경매 시작가, B=1이면 낮춤
        Sold,               // Seat=낙찰자, Item, A=가격, B=판매자
        Unsold,             // Seat=판매자, Item, B=1이면 유찰품 경매에서도 안 팔림
        LoanTaken,          // Seat, A=금액
        SkipChanged,        // Seat, A=1 동의, B=동의 수, C=참가자 수
        TimeSkipped,        // A=남은 ms
        EndVoteChanged,     // Seat, A=1 동의, B=동의 수, C=참가자 수
        EndedEarly,
        SellerSaid,         // Seat, A=빠른 한마디 번호(-1 직접 쓴 글), Text
        RevealStarted,      // A=공개할 물건 수
        ItemRevealed,       // Item, A=가치, B=낙찰가(없으면 0)
        Results,
        Aborted,            // A=사유
        SeatReplacedByAI    // Seat
    }

    /// <summary>엔진이 내보내는 사건. Audience 가 -1 이면 모두, 아니면 그 좌석에게만.</summary>
    [Serializable]
    public struct GameEvent
    {
        public EventType Type;
        public int Seat;
        public int Item;
        public int A;
        public int B;
        public int C;
        public string Text;
        public int Audience;
        public long TimeMs;

        public override string ToString()
        {
            return Type + " seat=" + Seat + " item=" + Item + " a=" + A + " b=" + B + " c=" + C + (Text != null ? " \"" + Text + "\"" : "");
        }
    }

    /// <summary>좌석(참가자) 정보. 이름·외형은 게임 시작 때 정하고, 나머지는 엔진이 관리한다.</summary>
    [Serializable]
    public sealed class SeatInfo
    {
        public string Name = "";
        public bool IsAI;
        public int ColorIndex;
        public int CharacterId;     // 연출용 캐릭터(가면·옷) 번호
        public string AiProfile = "";
        public string LookJson = "";  // 사람 좌석: 꾸민 생김새 (화면 쪽에서만 해석)
    }

    public sealed class SeatState
    {
        public int Index;
        public SeatInfo Info;
        public bool Active = true;
        public bool Registered;
        public int Balance;
        public int Reserved;
        public int CompareLeft;
        public int AppraiseLeft;
        public int FakeLeft;
        public bool Loan;
        public bool SkipVote;
        public bool EndVote;
        public int ItemValueSum;
        public int Score;
        public int Rank;
        public long LastSayMs = -100000;
    }

    public sealed class ItemState
    {
        public int Id;
        public int Seller;
        public int Slot;
        public ItemStatus Status;
        // ── 판매자와 엔진만 아는 값 (Presented 전에는 외형·시작가·문구도 비공개, 가치는 공개 단계까지 비공개)
        public int Value;
        public int Appearance = -1;
        public int InitialStart;
        public int Phrase = -1;
        public string CustomName = "";
        public string CustomPhrase = "";
        public bool Known;
        // ── 공개
        public bool Presented;
        public int StartPrice;
        public bool Dropped;
        public bool LeftoverFailed;
        public int Winner = -1;
        public int SalePrice;
        public bool Settled;
        public bool Revealed;
        public int BidCount;
        public int BidderMask;  // 입찰한 좌석 비트
    }

    /// <summary>등록 제출 내용 (물건 개수만큼 앞에서부터 쓴다).</summary>
    [Serializable]
    public sealed class Registration
    {
        public int[] Appearance = new int[GameRules.MaxItemsPerPlayer];
        public int[] Value = new int[GameRules.MaxItemsPerPlayer];
        public int[] Start = new int[GameRules.MaxItemsPerPlayer];
        public int[] Phrase = new int[GameRules.MaxItemsPerPlayer];
        public string[] CustomName = { "", "", "", "" };
        public string[] CustomPhrase = { "", "", "", "" };

        public Registration Clone()
        {
            var r = new Registration();
            Array.Copy(Appearance, r.Appearance, Appearance.Length);
            Array.Copy(Value, r.Value, Value.Length);
            Array.Copy(Start, r.Start, Start.Length);
            Array.Copy(Phrase, r.Phrase, Phrase.Length);
            for (int i = 0; i < GameRules.MaxItemsPerPlayer; i++)
            {
                r.CustomName[i] = CustomName[i] ?? "";
                r.CustomPhrase[i] = CustomPhrase[i] ?? "";
            }
            return r;
        }
    }

    /// <summary>능력 결과 (본인만).</summary>
    [Serializable]
    public struct AbilityRecord
    {
        public int Item;
        public AbilityKind Kind;
        public int A;
        public int B;
        public long TimeMs;
    }

    /// <summary>결과 화면용 거래 기록.</summary>
    [Serializable]
    public struct DealRecord
    {
        public int Item;
        public int Buyer;
        public int Price;
        public int Value;
        public int Profit { get { return Value - Price; } }
    }

    public static class ListUtil
    {
        public static void Shuffle<T>(IList<T> list, Rng rng)
        {
            for (int k = list.Count - 1; k > 0; k--)
            {
                int j = rng.Range(0, k + 1);
                T x = list[k];
                list[k] = list[j];
                list[j] = x;
            }
        }
    }
}
