using System.Collections.Generic;

namespace BluffAuction.Loc
{
    // 화면 문구 표. 순서: 한국어, English, 日本語, 繁體中文, 简体中文 (빈 칸은 English 로 대신)
    public static partial class L
    {
        static void Fill(Dictionary<string, string[]> t)
        {
            // ───────── 공통 ─────────
            Add(t, "title", "허풍 경매장", "Bluff Auction");
            Add(t, "title_sub", "가면 속 진실을 꿰뚫어라", "See through the masks");
            Add(t, "ok", "확인", "OK");
            Add(t, "cancel", "취소", "Cancel");
            Add(t, "back", "뒤로", "Back");
            Add(t, "close", "닫기", "Close");
            Add(t, "next", "다음", "Next");
            Add(t, "prev", "이전", "Previous");
            Add(t, "yes", "예", "Yes");
            Add(t, "no", "아니요", "No");
            Add(t, "on", "켜기", "On");
            Add(t, "off", "끄기", "Off");
            Add(t, "auto", "자동", "Auto");
            Add(t, "random", "무작위", "Random");
            Add(t, "you", "나", "You");
            Add(t, "someone", "누군가", "Someone");
            Add(t, "seconds", "{0}초", "{0}s");
            Add(t, "press_any", "아무 키나 누르세요", "Press any key");

            // ───────── 메인 메뉴 ─────────
            Add(t, "menu_play", "게임 시작", "Play");
            Add(t, "menu_play_sub", "AI 상대와 한 판", "A game against AI rivals");
            Add(t, "menu_online", "친구와 온라인", "Play with friends");
            Add(t, "menu_online_sub", "방 만들기 · 참가하기", "Host or join a room");
            Add(t, "menu_howto", "게임 방법", "How to Play");
            Add(t, "menu_settings", "설정", "Settings");
            Add(t, "menu_records", "기록", "Records");
            Add(t, "menu_quit", "종료", "Quit");
            Add(t, "menu_continue", "이어서 (같은 상대)", "Rematch (same rivals)");
            Add(t, "quit_confirm", "게임을 종료할까요?", "Quit the game?");

            // ───────── 새 게임 ─────────
            Add(t, "setup_title", "새 경매", "New Auction");
            Add(t, "setup_rivals", "상대", "Rivals");
            Add(t, "setup_rivals_count", "상대 {0}명", "{0} rivals");
            Add(t, "setup_difficulty", "난이도", "Difficulty");
            Add(t, "diff_0", "쉬움", "Easy");
            Add(t, "diff_1", "보통", "Normal");
            Add(t, "diff_2", "어려움", "Hard");
            Add(t, "diff_0_desc", "상대가 허풍에 잘 속고, 실수가 많아요", "Rivals fall for bluffs and make mistakes");
            Add(t, "diff_1_desc", "능력과 신호를 적당히 읽어요", "Rivals read signals reasonably well");
            Add(t, "diff_2_desc", "같은 판매자의 합계까지 계산하는 고수들", "Sharks who even track each seller's total");
            Add(t, "setup_you", "나", "You");
            Add(t, "setup_name", "이름", "Name");
            Add(t, "setup_look", "가면", "Mask");
            Add(t, "setup_rules", "규칙", "Rules");
            Add(t, "setup_items", "1인당 물건", "Lots per player");
            Add(t, "setup_bidtime", "경매 시간", "Bidding time");
            Add(t, "setup_extend", "마감 연장", "Last-second extension");
            Add(t, "setup_regfund", "등록 자금", "Value pool");
            Add(t, "setup_buyfund", "구매 자금", "Bidding cash");
            Add(t, "setup_compare", "시세 비교", "Price Check");
            Add(t, "setup_appraise", "간이감정", "Appraisal");
            Add(t, "setup_fake", "거짓 통보", "Fake Signal");
            Add(t, "setup_loan", "대출", "Loan");
            Add(t, "setup_blind", "블라인드 (입찰자 숨김)", "Blind bidding (hide bidders)");
            Add(t, "setup_start", "경매 시작", "Start the Auction");
            Add(t, "setup_defaults", "기본값", "Defaults");
            Add(t, "setup_estimate", "예상 {0}분", "About {0} min");
            Add(t, "setup_random_rivals", "무작위 상대", "Random rivals");

            // ───────── AI 캐릭터 ─────────
            Add(t, "ai_baron", "허풍 남작", "Baron Bluffington");
            Add(t, "ai_baron_bio", "시끄럽고 대담하다. 그의 '걸작'은 대개 잡동사니다.", "Loud and bold. His 'masterpieces' are usually junk.");
            Add(t, "ai_ledger", "장부 부인", "Madame Ledger");
            Add(t, "ai_ledger_bio", "모든 숫자를 적는다. 능력을 아끼지 않는다.", "Writes down every number. Never shy with abilities.");
            Add(t, "ai_lou", "행운의 루", "Lucky Lou");
            Add(t, "ai_lou_bio", "감으로 산다. 가끔 대박, 자주 쪽박.", "Bids on gut feeling. Sometimes a jackpot, often a bust.");
            Add(t, "ai_professor", "교수", "The Professor");
            Add(t, "ai_professor_bio", "감정이 전문. 정직하게 팔고 신중하게 산다.", "An appraiser at heart. Sells honestly, buys carefully.");
            Add(t, "ai_velvet", "벨벳 공작부인", "Duchess Velvet");
            Add(t, "ai_velvet_bio", "마지막 순간에 나타난다. 왕관에 약하다.", "Strikes at the last second. Weak for crowns.");
            Add(t, "ai_magpie", "늙은 까치", "Old Magpie");
            Add(t, "ai_magpie_bio", "마음에 든 물건은 끝까지 쫓는다. 매번 취향이 바뀐다.", "Chases whatever catches his eye. His taste changes every game.");
            Add(t, "ai_fox", "침묵의 여우", "Silent Fox");
            Add(t, "ai_fox_bio", "말이 없다. 거짓 통보로 판을 흔든다.", "Never speaks. Shakes the table with fake signals.");
            Add(t, "ai_rook", "루크 선장", "Captain Rook");
            Add(t, "ai_rook_bio", "크게 지른다. 빚도 두려워하지 않는다.", "Bids big and isn't afraid of debt.");
            Add(t, "auctioneer", "무슈 개블", "Monsieur Gavel");

            // ───────── 물건 ─────────
            string[] items =
            {
                "고대 항아리", "Ancient Amphora", "황금 시계", "Golden Mantel Clock", "은 촛대", "Silver Candelabra", "보석 반지", "Jeweled Ring",
                "명화 액자", "Old Master Painting", "청자 화병", "Celadon Vase", "골동품 지구본", "Antique Globe", "수정 구슬", "Crystal Ball",
                "왕관", "Royal Crown", "회중시계", "Pocket Watch", "도자기 인형", "Porcelain Figurine", "고서", "Antique Tomes"
            };
            for (int i = 0; i < 12; i++) Add(t, "item_" + i, items[i * 2], items[i * 2 + 1]);

            string[] phrases =
            {
                "이 물건에서는 이상한 냄새가 난다", "It smells strange",
                "알 수 없는 내용물로 가득 차 있다", "Full of something unknown",
                "왕실에서 쓰던 물건이라는 소문이 있다", "Rumored to be royal property",
                "감정사가 보자마자 감탄했다", "The appraiser gasped at first sight",
                "뒷면에 작은 금이 가 있다", "A small crack on the back",
                "가짜라는 소문이 돈다", "Rumored to be fake",
                "밤마다 작은 소리가 난다", "Makes small noises at night",
                "전 주인이 절대 팔지 않겠다던 물건", "The old owner swore never to sell it",
                "흠집 하나 없는 완벽한 상태", "Flawless condition",
                "누군가 한 번 고친 흔적이 있다", "Someone repaired it once",
                "생각보다 훨씬 가볍다", "Much lighter than it looks",
                "만지면 이상하게 따뜻하다", "Strangely warm to the touch",
                "출처를 아무도 모른다", "Nobody knows where it came from",
                "세상에 몇 개 없는 한정품", "Only a few exist in the world",
                "안에서 무언가 굴러다니는 소리가 난다", "Something rattles inside",
                "주인이 세 번이나 바뀌었다", "Changed owners three times",
                "보기만 해도 기분이 좋아진다", "Just looking at it cheers you up",
                "어딘가 불길한 기운이 느껴진다", "Feels somehow ominous",
                "유명한 수집가가 탐내던 물건", "A famous collector wanted it",
                "할머니 다락방에서 찾았다", "Found in grandma's attic"
            };
            for (int i = 0; i < 20; i++) Add(t, "phrase_" + i, phrases[i * 2], phrases[i * 2 + 1]);

            // ───────── 빠른 한마디 (Content.Line*) ─────────
            string[] lines =
            {
                "진짜 걸작이에요!", "A true masterpiece!",
                "놓치면 후회해요!", "Don't miss this one!",
                "한 푼도 아깝지 않아요!", "Worth every coin!",
                "더 올려 주세요!", "Bid higher!",
                "이 가격이면 거저예요!", "A steal at this price!",
                "할머니가 우실 거예요…", "Grandma would weep...",
                "그게 다예요?", "Is that all you've got?",
                "너무 비싸서 겁나요?", "Too rich for you?",
                "하하, 제가 더 높죠!", "Hah, outbid!",
                "꿈 깨세요.", "Keep dreaming.",
                "잡동사니 냄새가 나는데…", "Smells like junk...",
                "저라면 안 사요.", "I wouldn't touch it.",
                "너무 비싸요!", "Overpriced!",
                "내 거다!", "Mine!",
                "완전 이득!", "What a deal!",
                "제가 낙찰받았네요!", "Sold to me!",
                "가치는 제가 알죠.", "I know what it's worth.",
                "이건 믿으셔도 돼요.", "Trust me on this one.",
                "흥미롭군요… 아주 흥미로워.", "Interesting... very interesting.",
                "으, 너무 비싸.", "Ugh, too expensive.",
                "다음엔 꼭…", "Next time..."
            };
            for (int i = 0; i < 21; i++) Add(t, "line_" + i, lines[i * 2], lines[i * 2 + 1]);
            Add(t, "lines_title", "한마디", "Say something");
            Add(t, "lines_hype", "자랑", "Hype");
            Add(t, "lines_taunt", "도발", "Taunt");
            Add(t, "lines_doubt", "깎아내리기", "Doubt");
            Add(t, "lines_bluff", "아는 척", "Bluff");
            Add(t, "lines_react", "반응", "React");

            // ───────── 등록 ─────────
            Add(t, "reg_title", "물건 등록", "Register Your Lots");
            Add(t, "reg_sub", "등록 자금 {0}을 물건들에 모두 나눠 가치를 정하세요. 가치는 끝날 때까지 비밀입니다.", "Split the {0} value pool across your lots. Values stay secret until the end.");
            Add(t, "reg_lot", "{0}번 물건", "Lot {0}");
            Add(t, "reg_value", "진짜 가치", "True value");
            Add(t, "reg_start", "시작가", "Starting price");
            Add(t, "reg_phrase", "소문", "Rumor");
            Add(t, "reg_custom_name", "물건 이름 (예: 할머니의 틀니)", "Item name (e.g. Grandma's dentures)");
            Add(t, "reg_custom_phrase", "설명 · 소문 (예: 밤마다 혼자 웃는다)", "Description / rumor (e.g. laughs at night)");
            Add(t, "reg_custom", "직접 쓰기 (이름·설명·그림)", "Write my own (name, text, picture)");
            Add(t, "reg_total", "나눈 가치 {0} / {1}", "Allocated {0} / {1}");
            Add(t, "reg_left", "{0} 남음", "{0} left");
            Add(t, "reg_over", "{0} 넘침", "{0} over");
            Add(t, "reg_exact", "딱 맞아요", "Perfect");
            Add(t, "reg_balance", "자동 맞추기", "Auto-balance");
            Add(t, "reg_randomize", "전부 무작위", "Randomize all");
            Add(t, "reg_confirm", "등록 확정", "Confirm Lots");
            Add(t, "reg_confirm_again", "한 번 더 누르면 확정 (바꿀 수 없음)", "Click again to lock in (no changes after)");
            Add(t, "reg_waiting", "다른 참가자를 기다리는 중…", "Waiting for others...");
            Add(t, "reg_done", "등록 완료", "Registered");
            Add(t, "reg_pending", "등록 중", "Registering");
            Add(t, "reg_fee_warn", "시작가가 구매 자금({0})을 넘어요. 안 팔리면 넘는 만큼 점수에서 빠집니다.", "Starting price exceeds bidding cash ({0}). If unsold, the excess is deducted.");
            Add(t, "reg_hint", "시작가는 허풍을 칠 수 있는 곳이에요. 싼 물건을 비싸 보이게, 보물을 잡동사니처럼!", "The starting price is where you bluff: make junk look precious, or hide a treasure.");
            Add(t, "reg_err_sum", "가치 합계가 등록 자금과 같아야 해요", "Values must add up to the pool exactly");
            Add(t, "reg_err_min", "물건마다 가치는 {0} 이상이어야 해요", "Each lot must be worth at least {0}");
            Add(t, "reg_err_dup", "같은 물건을 두 번 고를 수 없어요", "You can't pick the same item twice");

            // ───────── 경매 ─────────
            Add(t, "lot_of", "{0} / {1}번째 경매", "Lot {0} of {1}");
            Add(t, "round", "{0}바퀴", "Round {0}");
            Add(t, "reauction", "유찰품 경매", "Second Chance");
            Add(t, "seller", "판매자", "Seller");
            Add(t, "start_price", "시작가", "Starting at");
            Add(t, "current_bid", "현재 최고가", "Current bid");
            Add(t, "no_bids_yet", "아직 입찰 없음", "No bids yet");
            Add(t, "high_bidder", "{0}님", "{0}");
            Add(t, "you_are_high", "★ 내가 최고가 ★", "★ You're winning ★");
            Add(t, "bid_button", "{0} 입찰", "Bid {0}");
            Add(t, "bid_min", "최소", "Min");
            Add(t, "bid_custom", "직접", "Custom");
            Add(t, "bid_blocked_seller", "내 물건이에요", "It's your lot");
            Add(t, "bid_blocked_high", "최고가 유지 중", "You're the top bid");
            Add(t, "bid_blocked_cash", "돈이 모자라요", "Not enough cash");
            Add(t, "bid_blocked_wait", "곧 입찰 시작", "Bidding opens soon");
            Add(t, "skip", "건너뛰기", "Skip");
            Add(t, "skip_votes", "건너뛰기 {0}/{1}", "Skip {0}/{1}");
            Add(t, "presenting", "물건 소개", "Presenting");
            Add(t, "your_lot", "내 물건이 경매에 나왔어요! 한마디로 분위기를 띄우세요.", "Your lot is on the block! Talk it up.");
            Add(t, "outbid", "최고가를 빼앗겼어요!", "You've been outbid!");
            Add(t, "extended", "연장 +{0}초", "Extended +{0}s");
            Add(t, "going_once", "하나…", "Going once...");
            Add(t, "going_twice", "둘…", "Going twice...");
            Add(t, "sold_to", "{0}님께 {1}에 낙찰!", "Sold to {0} for {1}!");
            Add(t, "sold_to_you", "{0}에 낙찰받았어요!", "You won it for {0}!");
            Add(t, "sold_yours", "내 물건이 {0}에 팔렸어요!", "Your lot sold for {0}!");
            Add(t, "unsold", "유찰 — 주인에게 돌아갑니다", "Unsold - back to its owner");
            Add(t, "deferred", "입찰 없음 — 이번 바퀴 끝에 {0}에 다시 나옵니다", "No bids - returns at {0} at the end of the round");
            Add(t, "nobid_title", "아무도 입찰하지 않았어요", "Nobody bid on your lot");
            Add(t, "nobid_sub", "유찰품 경매 시작가를 정하세요. 포기하면 가치의 1/2을 가져가고, 다시 내놓았다 또 안 팔리면 1/3만 남아요.", "Set a second-chance price. Give up to keep 1/2 of its value; if it fails again you keep only 1/3.");
            Add(t, "nobid_keep", "{0} 그대로", "Keep {0}");
            Add(t, "nobid_lower", "{0}로 낮추기", "Lower to {0}");
            Add(t, "nobid_giveup", "포기 (가치의 1/2)", "Give up (keep 1/2)");

            // ───────── 지갑 ─────────
            Add(t, "wallet", "내 돈", "My cash");
            Add(t, "available", "쓸 수 있는 돈", "Available");
            Add(t, "reserved", "묶인 돈", "On hold");
            Add(t, "spent", "쓴 돈", "Spent");
            Add(t, "penalty_now", "지금 끝나면 벌금 {0}", "Penalty if it ended now: {0}");
            Add(t, "penalty_hint", "벌금을 피하려면 {0} 더 써야 해요", "Spend {0} more to avoid the penalty");
            Add(t, "penalty_none", "벌금 없음 — 잘 쓰고 있어요", "No penalty - money well spent");
            Add(t, "loan", "대출", "Loan");
            Add(t, "loan_title", "대출 받기", "Take a loan");
            Add(t, "loan_body", "{0}을 바로 받고, 끝날 때 {1}(이자 포함)을 점수에서 뺍니다. 게임 중 한 번만, 모두에게 알려져요.", "Get {0} now; {1} (with interest) is deducted at the end. Once per game, and everyone will know.");
            Add(t, "loan_take", "대출 받기", "Take the loan");
            Add(t, "loan_taken", "대출 중", "In debt");

            // ───────── 능력 ─────────
            Add(t, "abilities", "능력", "Abilities");
            Add(t, "ab_compare", "시세 비교", "Price Check");
            Add(t, "ab_compare_desc", "지금 가격보다 진짜 가치가 높은지 낮은지 나만 알아요.", "Learn privately whether the true value is above or below the current price.");
            Add(t, "ab_appraise", "간이감정", "Appraisal");
            Add(t, "ab_appraise_desc", "진짜 가치가 들어 있는 범위를 나만 알아요 (범위 안 위치는 무작위).", "Learn privately a range that contains the true value.");
            Add(t, "ab_fake", "거짓 통보", "Fake Signal");
            Add(t, "ab_fake_desc", "아무것도 알지 못하지만, 모두에게 능력을 쓴 것처럼 보여요.", "Learn nothing, but everyone sees you 'use an ability'.");
            Add(t, "ab_left", "{0}회", "x{0}");
            Add(t, "ab_used_by", "{0}님이 능력을 사용했습니다", "{0} used an ability");
            Add(t, "ab_result_higher", "{0}일 때 → 진짜 가치가 더 높아요", "At {0} → the true value is HIGHER");
            Add(t, "ab_result_lower", "{0}일 때 → 진짜 가치가 더 낮아요", "At {0} → the true value is LOWER");
            Add(t, "ab_result_equal", "{0} → 진짜 가치와 같아요!", "{0} → exactly the true value!");
            Add(t, "ab_result_range", "진짜 가치는 {0} ~ {1} 사이", "True value is between {0} and {1}");
            Add(t, "ab_result_fake", "거짓 통보를 보냈어요 (남은 {0}회)", "Fake signal sent ({0} left)");
            Add(t, "ab_blocked", "경매 중인 남의 물건에만 쓸 수 있어요", "Only usable on others' lots during bidding");

            // ───────── 참가자 · 알림 ─────────
            Add(t, "players", "참가자", "Players");
            Add(t, "won_items", "산 물건 {0}", "Won {0}");
            Add(t, "feed_bid", "{0}님 {1} 입찰", "{0} bids {1}");
            Add(t, "feed_lot", "{0}의 {1} 경매 시작 ({2}부터)", "{0}'s {1} opens at {2}");
            Add(t, "feed_sold", "{0} → {1}님 {2}", "{0} → {1} for {2}");
            Add(t, "feed_unsold", "{0} 유찰", "{0} unsold");
            Add(t, "feed_deferred", "{0} 유찰품 경매로 ({1})", "{0} moves to second chance ({1})");
            Add(t, "feed_loan", "{0}님이 {1} 대출을 받았어요", "{0} took a {1} loan");
            Add(t, "feed_ability", "{0}님이 능력을 사용했습니다", "{0} used an ability");
            Add(t, "feed_say", "{0}: {1}", "{0}: {1}");

            // ───────── 공개 · 결과 ─────────
            Add(t, "reveal_title", "진짜 가치 공개", "The Reveal");
            Add(t, "reveal_value", "진짜 가치", "True value");
            Add(t, "reveal_paid", "낙찰가", "Paid");
            Add(t, "reveal_profit", "손익", "Profit");
            Add(t, "reveal_unsold", "안 팔림 → {0} 몫", "Unsold → owner keeps {0}");
            Add(t, "reveal_returned", "주인에게 (가치 그대로)", "Returned (full value)");
            Add(t, "results_title", "최종 순위", "Final Standings");
            Add(t, "results_rank", "{0}위", "#{0}");
            Add(t, "results_score", "점수", "Score");
            Add(t, "results_cash", "남은 돈", "Cash");
            Add(t, "results_items", "산 물건 가치", "Items");
            Add(t, "results_unsold", "돌아온 내 물건", "Unsold lots");
            Add(t, "results_penalty", "안 쓴 돈 벌금", "Unspent penalty");
            Add(t, "results_loan", "대출 상환", "Loan repaid");
            Add(t, "results_best", "최고의 거래", "Best deal");
            Add(t, "results_worst", "최악의 거래", "Worst deal");
            Add(t, "results_top", "최고 낙찰가", "Top sale");
            Add(t, "results_win", "우승!", "Victory!");
            Add(t, "results_again", "다시 하기", "Play Again");
            Add(t, "results_menu", "메인 메뉴", "Main Menu");
            Add(t, "results_memory", "상대들이 이번 판에서 당신의 허풍 버릇을 배웠습니다.", "Your rivals have learned your bluffing habits from this game.");
            Add(t, "ended_early", "모두 동의해 일찍 끝냈습니다", "Ended early by agreement");

            // ───────── 일시정지 ─────────
            Add(t, "pause", "일시정지", "Paused");
            Add(t, "resume", "계속하기", "Resume");
            Add(t, "end_now", "지금 끝내고 공개하기", "End now & reveal");
            Add(t, "end_now_confirm", "남은 물건은 주인에게 돌아가고 바로 공개합니다. 끝낼까요?", "Remaining lots return to their owners and values are revealed. End now?");
            Add(t, "to_menu", "메인 메뉴로", "Quit to menu");
            Add(t, "to_menu_confirm", "이 판을 그만두고 메인 메뉴로 갈까요?", "Abandon this game and return to the menu?");

            // ───────── 설정 ─────────
            Add(t, "set_language", "언어", "Language");
            Add(t, "set_audio", "소리", "Audio");
            Add(t, "set_master", "전체 음량", "Master volume");
            Add(t, "set_music", "음악", "Music");
            Add(t, "set_sfx", "효과음", "Sound effects");
            Add(t, "set_voice", "목소리", "Voices");
            Add(t, "set_ambience", "경매장 소음", "Ambience");
            Add(t, "set_display", "화면", "Display");
            Add(t, "set_fullscreen", "전체 화면", "Fullscreen");
            Add(t, "set_resolution", "해상도", "Resolution");
            Add(t, "set_vsync", "수직 동기화", "V-Sync");
            Add(t, "set_quality", "그래픽 품질", "Graphics quality");
            Add(t, "q_0", "낮음", "Low");
            Add(t, "q_1", "중간", "Medium");
            Add(t, "q_2", "높음", "High");
            Add(t, "set_dof", "피사계 심도", "Depth of field");
            Add(t, "set_grain", "필름 그레인", "Film grain");
            Add(t, "set_gameplay", "게임", "Gameplay");
            Add(t, "set_camera", "카메라 움직임", "Camera motion");
            Add(t, "set_ui_scale", "글자·화면 크기", "UI scale");
            Add(t, "set_hints", "도움말 표시", "Show hints");
            Add(t, "set_speed", "AI 반응 속도", "AI reaction speed");
            Add(t, "set_reset", "기본값으로", "Reset to defaults");

            // ───────── 기록 ─────────
            Add(t, "rec_games", "한 판 수", "Games played");
            Add(t, "rec_wins", "우승", "Wins");
            Add(t, "rec_best", "최고 점수", "Best score");
            Add(t, "rec_bargain", "최고의 득템", "Best bargain");
            Add(t, "rec_achievements", "업적", "Achievements");

            // ───────── 게임 방법 ─────────
            Add(t, "howto_1_t", "목표", "The Goal");
            Add(t, "howto_1", "모두가 물건을 내놓고, 서로의 물건을 경매로 삽니다. 끝났을 때 [남은 돈 + 산 물건의 진짜 가치]가 가장 큰 사람이 이깁니다. 진짜 가치는 판매자만 알고, 마지막에 공개돼요.",
                "Everyone sells lots and buys each other's at auction. When it's over, the highest [cash + true value of what you bought] wins. Only the seller knows a lot's true value until the final reveal.");
            Add(t, "howto_2_t", "등록 = 허풍", "Registering = Bluffing");
            Add(t, "howto_2", "등록 자금을 내 물건들에 나눠 진짜 가치를 정하고, 시작가와 소문을 고릅니다. 시작가는 아무렇게나 정할 수 있어요. 잡동사니를 비싸게, 보물을 싸게 보이게 만들어 보세요.",
                "Split your value pool across your lots to set their true values, then choose starting prices and rumors. Starting prices can be anything - make junk look precious, or disguise a treasure.");
            Add(t, "howto_3_t", "입찰", "Bidding");
            Add(t, "howto_3", "한 번에 한 물건씩 경매합니다. 최고가보다 높게 부르세요. 최고 입찰자는 더 올릴 수 없고, 마감 직전 입찰은 시간을 연장합니다. 판매 대금은 판매자에게 갑니다.",
                "Lots are auctioned one at a time. Outbid the current price. The top bidder can't raise their own bid, and last-second bids extend the timer. The money goes to the seller.");
            Add(t, "howto_4_t", "능력", "Abilities");
            Add(t, "howto_4", "시세 비교: 지금 가격보다 가치가 높은지 낮은지. 간이감정: 가치가 들어 있는 범위. 거짓 통보: 아무것도 몰라도 능력을 쓴 척. 누가 능력을 쓰면 모두에게 알려지니, 그 뒤 행동을 눈여겨보세요!",
                "Price Check: is the value above or below the current price? Appraisal: a range containing the value. Fake Signal: pretend you used one. Everyone sees when someone uses an ability - watch what they do next!");
            Add(t, "howto_5_t", "돈은 써야 이득", "Spend It or Lose It");
            Add(t, "howto_5", "처음 받은 구매 자금 중 끝까지 안 쓴 돈은 절반을 잃어요. 그래서 진짜 가치의 2배 가까이 내고 사도 이득일 수 있습니다. 판 돈에는 벌금이 없어요.",
                "Half of any starting cash you never spend is lost. So paying up to nearly twice an item's value can still pay off. Money from your own sales is never penalized.");
            Add(t, "howto_6_t", "유찰과 대출", "Unsold Lots & Loans");
            Add(t, "howto_6", "입찰이 없으면 판매자가 시작가를 낮춰 바퀴 끝에 다시 내놓거나 포기(가치의 1/2)합니다. 다시 내놔도 안 팔리면 1/3만 남아요. 대출은 한 번, 원금과 이자를 끝에 갚습니다.",
                "With no bids, the seller can lower the price for a second chance at the end of the round, or give up and keep 1/2 of the value. Failing twice leaves only 1/3. You may take one loan; repay it plus interest at the end.");
            Add(t, "howto_7_t", "읽고, 속이고, 이기세요", "Read, Deceive, Win");
            Add(t, "howto_7", "AI 상대는 진짜 가치를 몰래 보지 않아요. 시작가·소문·입찰·능력 사용을 보고 추리하고, 여러 판에 걸쳐 당신의 버릇을 배웁니다. 같은 허풍은 두 번 통하지 않을지도!",
                "Your AI rivals never peek at true values. They reason from starting prices, rumors, bids and ability use - and learn your habits across games. The same bluff may not work twice!");

            // ───────── 도움말 (첫 판) ─────────
            Add(t, "hint_register", "카드를 눌러 물건 모양·가치·시작가를 정하세요. 합계가 딱 맞아야 확정할 수 있어요.", "Click a card to set its item, value and starting price. The total must match exactly.");
            Add(t, "hint_bid", "스페이스나 탁자 위 패들을 눌러 입찰. 휠이나 1~4로 올릴 금액을 골라요.", "Press Space or click your paddle to bid. Scroll or press 1-4 to choose the raise.");
            Add(t, "hint_ability", "확신이 없으면 능력을 써 보세요. 결과는 나만 봅니다.", "Not sure? Use an ability - only you see the result.");
            Add(t, "hint_seller", "내 물건이 경매 중이에요. 한마디(T)로 허풍을 쳐 보세요.", "Your lot is up. Press T to talk it up.");
            Add(t, "hint_penalty", "안 쓴 돈은 절반을 잃어요. 왼쪽 아래에서 벌금을 확인하세요.", "Unspent cash loses half. Check the penalty at bottom-left.");

            // ───────── 업적 ─────────
            Add(t, "ach_first_win", "첫 우승", "First Victory");
            Add(t, "ach_first_win_d", "처음으로 1등을 했다", "Win your first auction");
            Add(t, "ach_bargain", "득템의 신", "Bargain Hunter");
            Add(t, "ach_bargain_d", "가치의 절반 이하로 물건을 샀다", "Buy a lot for under half its value");
            Add(t, "ach_bluff", "허풍의 달인", "Master Bluffer");
            Add(t, "ach_bluff_d", "가치의 3배 넘게 내 물건을 팔았다", "Sell a lot for over 3x its value");
            Add(t, "ach_collector", "수집가", "Collector");
            Add(t, "ach_collector_d", "한 판에 물건 4개 이상을 샀다", "Win 4 or more lots in one game");
            Add(t, "ach_debt", "빚쟁이 우승", "Leveraged");
            Add(t, "ach_debt_d", "대출을 받고 우승했다", "Win a game after taking a loan");
            Add(t, "ach_hard", "상어 사냥꾼", "Shark Hunter");
            Add(t, "ach_hard_d", "어려움 난이도에서 우승했다", "Win on Hard difficulty");
            Add(t, "ach_full", "만석", "Full House");
            Add(t, "ach_full_d", "8명 경매에서 우승했다", "Win an 8-player auction");
            Add(t, "ach_spender", "깔끔한 지출", "Not a Coin Wasted");
            Add(t, "ach_spender_d", "벌금 없이 게임을 마쳤다", "Finish with no unspent penalty");
            Add(t, "ach_unlocked", "업적 달성!", "Achievement unlocked!");

            // ───────── 온라인 ─────────
            Add(t, "net_title", "친구와 온라인", "Play Online");
            Add(t, "net_host", "방 만들기", "Host a Room");
            Add(t, "net_host_sub", "친구를 초대하고 빈자리는 AI로 채워요", "Invite friends and fill empty seats with AI");
            Add(t, "net_join", "방 참가하기", "Join a Room");
            Add(t, "net_join_sub", "Steam 친구의 방이나 같은 네트워크의 방을 고르거나, 방장의 주소를 입력하세요.", "Pick a Steam friend's room or a room on your network, or enter the host's address.");
            Add(t, "net_rooms", "같은 네트워크의 방", "Rooms on your network");
            Add(t, "net_no_rooms", "방을 찾는 중…", "Searching for rooms...");
            Add(t, "net_address", "주소", "Address");
            Add(t, "net_connect", "연결", "Connect");
            Add(t, "net_connecting", "연결하는 중…", "Connecting...");
            Add(t, "net_connect_failed", "연결하지 못했어요: {0}", "Could not connect: {0}");
            Add(t, "net_host_failed", "방을 만들지 못했어요: {0}", "Could not host: {0}");
            Add(t, "net_lobby", "대기실", "Lobby");
            Add(t, "net_host_addr", "친구가 들어올 주소: {0}", "Friends join at: {0}");
            Add(t, "net_port_hint", "인터넷으로 하려면 공유기에서 TCP {0} 포트를 열어 주세요.", "For internet play, forward TCP port {0} on your router.");
            Add(t, "net_add_ai", "AI 추가", "Add AI");
            Add(t, "net_remove", "빼기", "Remove");
            Add(t, "net_ready", "준비", "Ready");
            Add(t, "net_not_ready", "준비 전", "Not ready");
            Add(t, "net_host_tag", "방장", "Host");
            Add(t, "net_start", "게임 시작", "Start Game");
            Add(t, "net_leave", "나가기", "Leave");
            Add(t, "net_need_players", "최소 {0}명이 필요해요 (AI를 추가하세요)", "Need at least {0} players (add AI)");
            Add(t, "net_wait_ready", "모두 준비하기를 기다리는 중", "Waiting for everyone to be ready");
            Add(t, "net_player_joined", "{0}님이 들어왔어요", "{0} joined");
            Add(t, "net_player_left", "{0}님이 나갔어요", "{0} left");
            Add(t, "net_left_ai", "{0}님이 나가서 AI가 자리를 이어받습니다", "{0} left - an AI takes over");
            Add(t, "net_closed", "방과의 연결이 끊겼어요", "Disconnected from the room");
            Add(t, "net_cmd_refused", "방장이 거절했어요", "Refused by the host");
            Add(t, "net_wait_host", "방장이 대기실로 돌아가기를 기다리는 중…", "Waiting for the host...");
            Add(t, "net_back_lobby", "대기실로", "Back to Lobby");
            Add(t, "net_rules_by_host", "규칙은 방장이 정해요", "The host sets the rules");
            // ── Steam
            Add(t, "steam_on", "Steam: {0}", "Steam: {0}");
            Add(t, "steam_off", "Steam에 연결되지 않았어요. 같은 네트워크와 IP 접속만 쓸 수 있어요.", "Not connected to Steam. Only LAN and direct IP are available.");
            Add(t, "steam_friends", "Steam 친구의 방", "Friends' rooms on Steam");
            Add(t, "steam_no_friends", "지금 방을 연 친구가 없어요", "No friends are hosting right now");
            Add(t, "steam_join", "참가", "Join");
            Add(t, "steam_invite", "Steam 친구 초대", "Invite Steam friends");
            Add(t, "steam_invite_title", "친구 초대", "Invite friends");
            Add(t, "steam_invite_send", "초대", "Invite");
            Add(t, "steam_invite_sent", "초대함 ✓", "Invited ✓");
            Add(t, "steam_invite_overlay", "Steam 오버레이로 초대", "Invite with the Steam overlay");
            Add(t, "steam_invite_none", "Steam 친구가 없어요", "No Steam friends found");
            Add(t, "steam_online", "온라인", "Online");
            Add(t, "steam_offline", "오프라인", "Offline");
            Add(t, "steam_ingame", "허풍 경매장 하는 중", "Playing Bluff Auction");
            Add(t, "steam_lobby_wait", "Steam 방을 여는 중…", "Opening Steam room...");
            Add(t, "steam_lobby_ready", "Steam 친구가 들어올 수 있어요", "Steam friends can join");
            Add(t, "steam_lobby_failed", "Steam 방을 열지 못했어요", "Could not open a Steam room");
            Add(t, "steam_lobby_hint", "Steam 친구는 초대하면 포트 설정 없이 들어와요. 같은 네트워크 친구는 위 주소로(인터넷 IP 접속은 TCP {0} 포트 열기).", "Steam friends join by invite, no port forwarding needed. LAN friends use the address above (direct IP over the internet needs TCP port {0}).");
            Add(t, "steam_invite_confirm", "초대받은 방으로 갈까요?", "Join the room you were invited to?");
            Add(t, "steam_invite_confirm_body", "지금 하던 게임이나 방에서는 나가게 돼요.", "You will leave your current game or room.");
            // ── Steam 친구 목록에 보이는 상태
            Add(t, "rp_menu", "메뉴", "In the menu");
            Add(t, "rp_solo", "AI와 경매 중", "Bidding against AI");
            Add(t, "rp_lobby", "대기실 ({0}/{1})", "In a lobby ({0}/{1})");
            Add(t, "rp_online", "온라인 경매 중 · {0}명", "Online auction · {0} players");

            // ───────── 1인칭 · 탁자 위 물건 ─────────
            Add(t, "fp_keys_bid", "Space · 패들 클릭: 입찰   휠: 올릴 금액", "Space / click paddle: bid   Wheel: raise");
            Add(t, "fp_keys_tools", "Q W E · 도구 클릭: 능력", "Q W E / click tools: abilities");
            Add(t, "fp_keys_look", "우클릭: 가까이 보기", "RMB: lean in");
            Add(t, "fp_keys_tab", "Tab: 커서 · 자세히", "Tab: cursor & details");
            Add(t, "fp_wheel", "휠로 올릴 금액 바꾸기", "Scroll to change the raise");
            Add(t, "tk_my_lots", "내 물건 (나만 봄)", "My lots (private)");
            Add(t, "tk_your_lot", "내 물건 · 진짜 가치 {0}", "Your lot · true value {0}");
            Add(t, "tk_register_hint", "물건을 등록하는 중", "Registering lots");
            Add(t, "tk_loan_hint", "대출 가능 {0}", "Loan available: {0}");
            Add(t, "tk_on_block", "지금 경매 중", "On the block now");
            Add(t, "tk_sold_to", "{0}님께 {1}에 팔림", "Sold to {0} for {1}");
            Add(t, "set_mouse_look", "마우스로 둘러보기", "Mouse look");
            Add(t, "set_sensitivity", "마우스 감도", "Mouse sensitivity");
            Add(t, "set_invert", "위아래 반전", "Invert Y axis");
            Add(t, "look_0", "황금", "Gold");
            Add(t, "look_1", "은빛", "Silver");
            Add(t, "look_2", "루비", "Ruby");
            Add(t, "look_3", "에메랄드", "Emerald");
            Add(t, "name_placeholder", "플레이어", "Player");
            Add(t, "fp_seller", "내 물건 경매 중 · T: 한마디", "Your lot is up · T: say something");

            // ── 채팅 · 음성
            Add(t, "chat_placeholder", "할 말을 쓰고 Enter (Esc 닫기)", "Type and press Enter (Esc to close)");
            Add(t, "chat_hint", "Enter 채팅 · M 마이크 켜기/끄기 · V 누르고 말하기 · R 감정 표현 · F 물건·무기 집기", "Enter chat · M mic on/off · V push to talk · R emotes · F grab items & weapons");
            Add(t, "chat_open", "채팅하기 (Enter)", "Chat (Enter)");
            Add(t, "mic_on", "마이크 켜짐", "Mic on");
            Add(t, "mic_off", "마이크 꺼짐", "Mic off");
            Add(t, "mic_ptt", "말하는 중…", "Talking…");
            // ── 감정 표현 (R)
            Add(t, "emote_title", "감정 표현\n마우스로 고르고 R 을 놓기", "Emotes\nAim with the mouse, release R");
            Add(t, "emote_wave", "손 흔들기", "Wave");
            Add(t, "emote_thumbsup", "엄지 척", "Thumbs up");
            Add(t, "emote_clap", "박수", "Clap");
            Add(t, "emote_peace", "브이", "Peace");
            Add(t, "emote_mock", "비웃기", "Mock");
            Add(t, "emote_facepalm", "얼굴 감싸기", "Facepalm");
            Add(t, "emote_angry", "화내기", "Angry");
            Add(t, "emote_middlefinger", "가운데 손가락", "Middle finger");
            Add(t, "feed_emote", "{0}: {1}", "{0}: {1}");
            Add(t, "feed_emote_to", "{0} → {1}: {2}", "{0} → {1}: {2}");
            Add(t, "feed_hit", "{0}님이 {1}님을 맞혔어요!", "{0} hit {1}!");
            // ── 던지기
            Add(t, "throw_0", "빵", "Bread roll");
            Add(t, "throw_1", "사과", "Apple");
            Add(t, "throw_2", "구겨진 종이", "Paper ball");
            Add(t, "throw_3", "토마토", "Tomato");
            Add(t, "throw_4", "코르크 마개", "Cork");
            Add(t, "throw_pick", "F · 클릭: 집기", "F / click: pick up");
            Add(t, "throw_hint", "클릭 · F: 가리킨 사람에게 던지기", "Click / F: throw at whoever you aim at");
            // ── 근접 무기
            Add(t, "melee_0", "말아 쥔 신문", "Rolled newspaper");
            Add(t, "melee_pick", "F · 클릭: 들기 (옆자리까지 닿아요)", "F / click: pick up (reaches your neighbours)");
            Add(t, "melee_hint", "조준점을 옆자리 사람에게 맞추고 클릭 · F: 내려놓기", "Put the crosshair on a neighbour and click · F: put down");
            Add(t, "melee_hint_target", "클릭: {0}님 때리기 · F: 내려놓기", "Click: bonk {0} · F: put down");
            Add(t, "throw_hint_target", "클릭 · F: {0}님에게 던지기", "Click / F: throw at {0}");
            Add(t, "melee_far", "너무 멀어요 — 옆자리 사람만 닿아요", "Too far — only your neighbours are in reach");
            Add(t, "feed_bonk", "{0}님이 {1}님을 {2} 때렸어요!", "{0} bonked {1} {2}!");
            Add(t, "melee_with_0", "신문지로", "with a rolled newspaper");
            // ── 능력 사용 표시
            Add(t, "ab_strip", "이 물건에 능력을 쓴 사람", "Abilities used on this lot");
            Add(t, "ab_total", "능력 {0}회", "{0} abilities");
            Add(t, "bid_strip", "최고 입찰", "Top bid");
            Add(t, "seller_strip", "판매자", "Seller");
            Add(t, "seller_me", "나 (내 물건)", "You (your lot)");
            Add(t, "np_seller", "판매", "Seller");
            Add(t, "seller_meta", "판매 중", "Selling");
            Add(t, "bid_meta", "입찰 {0}", "bid {0}");
            // ── 직접 쓴 물건
            Add(t, "reg_custom_title", "직접 쓴 물건", "Your own item");
            Add(t, "reg_image_pick", "그림 넣기…", "Add picture…");
            Add(t, "reg_image_remove", "그림 빼기", "Remove picture");
            Add(t, "reg_image_note", "PNG·JPG. 경매 때 회전판 위 액자로 모두에게 보여요.", "PNG/JPG. Shown to everyone in a frame on the turntable.");
            Add(t, "reg_image_filter", "그림 파일", "Images");
            Add(t, "reg_image_failed", "그림을 읽지 못했어요", "Couldn't read that picture");
            Add(t, "reg_image_too_big", "20MB 이하만", "20 MB max");
            // ── 설정: 어울리기
            Add(t, "set_social", "음성 채팅 · 어울리기", "Voice chat & social");
            Add(t, "set_mic_mode", "마이크 방식", "Mic mode");
            Add(t, "set_mic_ptt", "누르고 말하기 (V)", "Push to talk (V)");
            Add(t, "set_mic_open", "켜 두기 (M 으로 켜고 끔)", "Open mic (toggle with M)");
            Add(t, "set_mic_device", "마이크", "Microphone");
            Add(t, "set_mic_test", "마이크 시험 (내 목소리 듣기)", "Mic test (hear yourself)");
            Add(t, "set_mic_default", "기본 장치", "Default device");
            Add(t, "set_mic_threshold", "말소리 감지 민감도", "Voice detection sensitivity");
            Add(t, "set_chat_volume", "다른 사람 목소리", "Other players' voices");
            Add(t, "set_show_rude", "무례한 몸짓 그대로 보기", "Show rude gestures");
            Add(t, "set_show_images", "남이 넣은 물건 그림 보기", "Show others' item pictures");
            Add(t, "set_mic_none", "마이크를 찾지 못했어요. 윈도우 설정 → 개인 정보 → 마이크에서 데스크톱 앱 접근을 켜 주세요.", "No microphone found. Allow desktop apps in Windows Settings → Privacy → Microphone.");

            // ── 캐릭터 꾸미기
            Add(t, "cz_open", "꾸미기", "Customize");
            Add(t, "cz_title", "캐릭터 꾸미기", "Customize Character");
            Add(t, "cz_random", "무작위", "Randomize");
            Add(t, "cz_reset", "처음으로", "Reset");
            Add(t, "cz_presets", "빠른 시작", "Quick start");
            Add(t, "cz_face", "얼굴 보기", "Face");
            Add(t, "cz_full", "전신 보기", "Full body");
            Add(t, "cz_try_emote", "몸짓 해 보기", "Try a gesture");
            Add(t, "cz_done", "저장하고 돌아가기", "Save and return");
            Add(t, "cz_saved", "생김새를 저장했어요", "Look saved");
            Add(t, "cz_discard", "바꾼 것을 버릴까요?", "Discard your changes?");
            Add(t, "cz_discard_yes", "버리기", "Discard");
            Add(t, "cz_cat_body", "체형·피부", "Body & skin");
            Add(t, "cz_cat_eyes", "눈", "Eyes");
            Add(t, "cz_cat_hair", "머리·수염", "Hair & beard");
            Add(t, "cz_cat_mask", "가면", "Mask");
            Add(t, "cz_cat_hat", "모자", "Hat");
            Add(t, "cz_cat_outfit", "옷", "Outfit");
            Add(t, "cz_cat_extras", "장신구", "Accessories");
            Add(t, "cz_cat_voice", "목소리", "Voice");
            Add(t, "cz_width", "체격", "Build");
            Add(t, "cz_width_0", "마름", "Slim");
            Add(t, "cz_width_1", "보통", "Average");
            Add(t, "cz_width_2", "듬직", "Sturdy");
            Add(t, "cz_width_3", "둥글둥글", "Round");
            Add(t, "cz_height", "키", "Height");
            Add(t, "cz_height_0", "작음", "Short");
            Add(t, "cz_height_1", "보통", "Average");
            Add(t, "cz_height_2", "큼", "Tall");
            Add(t, "cz_head", "머리 크기", "Head size");
            Add(t, "cz_headsize_0", "작게", "Small");
            Add(t, "cz_headsize_1", "보통", "Average");
            Add(t, "cz_headsize_2", "크게", "Large");
            Add(t, "cz_skin", "피부색", "Skin tone");
            Add(t, "cz_eyes", "눈 모양", "Eye shape");
            Add(t, "cz_eye_color", "눈동자 색", "Eye color");
            Add(t, "cz_hair", "머리 모양", "Hairstyle");
            Add(t, "cz_hair_color", "머리 색", "Hair color");
            Add(t, "cz_facial", "수염", "Facial hair");
            Add(t, "cz_mask", "가면 모양", "Mask style");
            Add(t, "cz_mask_color", "가면 색", "Mask color");
            Add(t, "cz_mask_trim", "가면 테두리", "Mask trim");
            Add(t, "cz_feather", "깃털 장식", "Feather");
            Add(t, "cz_hat", "모자", "Hat");
            Add(t, "cz_hat_color", "모자 색", "Hat color");
            Add(t, "cz_outfit", "옷 종류", "Outfit");
            Add(t, "cz_coat", "겉옷 색", "Coat color");
            Add(t, "cz_shirt", "셔츠 색", "Shirt color");
            Add(t, "cz_trim", "장식 색", "Trim color");
            Add(t, "cz_pants", "바지 색", "Trousers color");
            Add(t, "cz_gloves", "장갑 색", "Glove color");
            Add(t, "cz_neck", "목 장식", "Neckwear");
            Add(t, "cz_accent", "목 장식 색", "Neckwear color");
            Add(t, "cz_eyewear", "안경", "Eyewear");
            Add(t, "cz_earrings", "귀걸이", "Earrings");
            Add(t, "cz_voice", "목소리", "Voice");
            Add(t, "cz_voice_n", "목소리 {0}", "Voice {0}");
            Add(t, "cz_eyestyle_round", "동그란 눈", "Round");
            Add(t, "cz_eyestyle_almond", "아몬드 눈", "Almond");
            Add(t, "cz_eyestyle_sleepy", "졸린 눈", "Sleepy");
            Add(t, "cz_eyestyle_sharp", "날카로운 눈", "Sharp");
            Add(t, "cz_eyestyle_happy", "웃는 눈", "Smiling");
            Add(t, "cz_eyestyle_wide", "놀란 눈", "Wide");
            Add(t, "cz_hairstyle_none", "민머리", "Bald");
            Add(t, "cz_hairstyle_short", "짧은 머리", "Short");
            Add(t, "cz_hairstyle_bun", "올림머리", "Bun");
            Add(t, "cz_hairstyle_wig", "곱슬 가발", "Powdered wig");
            Add(t, "cz_hairstyle_tufts", "옆머리만", "Side tufts");
            Add(t, "cz_hairstyle_long", "긴 머리", "Long");
            Add(t, "cz_hairstyle_slick", "넘긴 머리", "Slicked back");
            Add(t, "cz_hairstyle_bob", "단발", "Bob");
            Add(t, "cz_hairstyle_ponytail", "포니테일", "Ponytail");
            Add(t, "cz_hairstyle_spiky", "뾰족 머리", "Spiky");
            Add(t, "cz_hairstyle_curly", "뽀글 머리", "Curly");
            Add(t, "cz_hairstyle_mohawk", "모히칸", "Mohawk");
            Add(t, "cz_facialhair_none", "없음", "None");
            Add(t, "cz_facialhair_mustache", "콧수염", "Mustache");
            Add(t, "cz_facialhair_curlmustache", "말린 콧수염", "Curled mustache");
            Add(t, "cz_facialhair_beard", "턱수염", "Beard");
            Add(t, "cz_facialhair_goatee", "염소수염", "Goatee");
            Add(t, "cz_facialhair_sideburns", "구레나룻", "Sideburns");
            Add(t, "cz_maskstyle_domino", "도미노", "Domino");
            Add(t, "cz_maskstyle_cateye", "고양이 눈", "Cat-eye");
            Add(t, "cz_maskstyle_lace", "레이스", "Lace");
            Add(t, "cz_maskstyle_half", "반가면", "Half mask");
            Add(t, "cz_maskstyle_beak", "부리", "Beak");
            Add(t, "cz_maskstyle_fox", "여우", "Fox");
            Add(t, "cz_maskstyle_eyepatch", "안대", "Eye patch");
            Add(t, "cz_maskstyle_butterfly", "나비", "Butterfly");
            Add(t, "cz_maskstyle_none", "가면 없음", "No mask");
            Add(t, "cz_hatstyle_none", "없음", "None");
            Add(t, "cz_hatstyle_tophat", "실크햇", "Top hat");
            Add(t, "cz_hatstyle_bowler", "중산모", "Bowler");
            Add(t, "cz_hatstyle_tiara", "티아라", "Tiara");
            Add(t, "cz_hatstyle_flatcap", "헌팅캡", "Flat cap");
            Add(t, "cz_hatstyle_captain", "선장 모자", "Captain's cap");
            Add(t, "cz_hatstyle_foxhood", "여우 두건", "Fox hood");
            Add(t, "cz_hatstyle_feathered", "깃털 모자", "Feathered hat");
            Add(t, "cz_hatstyle_beret", "베레모", "Beret");
            Add(t, "cz_hatstyle_crown", "왕관", "Crown");
            Add(t, "cz_hatstyle_fedora", "페도라", "Fedora");
            Add(t, "cz_hatstyle_flower", "머리꽃", "Hair flower");
            Add(t, "cz_outfit_tailcoat", "연미복", "Tailcoat");
            Add(t, "cz_outfit_gown", "드레스", "Gown");
            Add(t, "cz_outfit_vest", "조끼", "Waistcoat");
            Add(t, "cz_outfit_uniform", "제복", "Uniform");
            Add(t, "cz_outfit_cloak", "망토", "Cloak");
            Add(t, "cz_outfit_sweater", "스웨터", "Sweater");
            Add(t, "cz_neckwear_none", "없음", "None");
            Add(t, "cz_neckwear_bowtie", "나비넥타이", "Bow tie");
            Add(t, "cz_neckwear_necktie", "넥타이", "Necktie");
            Add(t, "cz_neckwear_pearls", "진주 목걸이", "Pearls");
            Add(t, "cz_neckwear_cravat", "크라바트", "Cravat");
            Add(t, "cz_neckwear_medal", "훈장", "Medal");
            Add(t, "cz_neckwear_scarf", "목도리", "Scarf");
            Add(t, "cz_eyewear_none", "없음", "None");
            Add(t, "cz_eyewear_round", "동그란 안경", "Round glasses");
            Add(t, "cz_eyewear_monocle", "외알 안경", "Monocle");
            Add(t, "cz_eyewear_shades", "선글라스", "Sunglasses");
            Add(t, "cz_earring_none", "없음", "None");
            Add(t, "cz_earring_gold", "금 귀고리", "Gold hoops");
            Add(t, "cz_earring_pearl", "진주 귀걸이", "Pearl drops");
        }
    }
}
