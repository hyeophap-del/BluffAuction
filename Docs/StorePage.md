# Steam 상점 페이지 초안

Steamworks의 「상점 페이지 편집」에 그대로 옮겨 쓰는 초안입니다.
한국어·영어 외에 일본어·번체·간체 중국어 초안도 아래에 있습니다. 중국어·일본어는 원어민 검수를 받은 뒤 올리세요.

그림은 `Docs/Store/`에 있습니다. 게임 안에서 다시 찍으려면 `-scenario store`를 씁니다(README 참고).

| Steamworks 칸 | 파일 | 크기 |
|---|---|---|
| Header Capsule | `header.png` / `header_ko.png` | 920×430 |
| Small Capsule | `small.png` / `small_ko.png` | 462×174 |
| Main Capsule | `main.png` / `main_ko.png` | 1232×706 |
| Vertical Capsule | `vertical.png` / `vertical_ko.png` | 748×896 |
| Library Capsule | `library_capsule.png` / `library_capsule_ko.png` | 600×900 |
| Library Hero (글자 없음) | `library_hero.png` | 3840×1240 |
| Library Logo (투명) | `library_logo.png` / `library_logo_ko.png` | 1280×720 |
| Page Background (선택) | `page_background.png` | 1438×810 |

스크린샷은 `Docs/Screenshots/`에서 1920×1080 이상인 것을 5장 이상 고릅니다.

---

## 이름

- 한국어: **허풍 경매장**
- English: **Bluff Auction**

## 짧은 설명 (300자 이내)

**한국어**
> 내 물건의 진짜 가치는 나만 안다. 잡동사니는 보물처럼, 보물은 잡동사니처럼 — 시작가 한 줄과 허풍 한마디로 상대를 속이고, 상대의 허풍은 꿰뚫어 보세요. 마이크를 켜고 친구들과, 혹은 버릇을 배우는 AI 손님들과 벌이는 1인칭 허풍 경매 파티 게임.

**English**
> Only you know what your item is really worth. Dress up junk as treasure, pass off treasure as junk, and see through everyone else's bluffs. A first-person bluffing auction party game for friends with voice chat — or for AI rivals who learn your habits.

## 자세한 설명

### 한국어

**가면 속 진실을 꿰뚫어라.**

어두운 경매장의 둥근 탁자. 손님들은 저마다 물건을 하나씩 가지고 왔고, 그 물건의 진짜 가치는 가져온 사람만 압니다.
시작가는 마음대로 정할 수 있고, 한마디 소문도 붙일 수 있어요. 녹슨 회중시계를 "왕실에서 흘러나온 물건"이라고 우겨도 됩니다.

끝났을 때 **남은 돈 + 산 물건의 진짜 가치**가 가장 큰 사람이 이깁니다. 그런데 처음 받은 구매 자금은 안 쓰면 절반이 날아가요. 그러니 결국 누군가의 물건은 사야 합니다. 누구의 말을 믿으시겠어요?

**특징**

- **허풍이 곧 전략**: 내 물건의 가치를 직접 정하고, 시작가와 소문으로 상대를 속입니다. 직접 이름·설명을 쓰고 그림 파일을 넣으면 회전판 위 금테 액자로 모두에게 보여요.
- **세 가지 능력**: 「시세 비교」로 지금 가격이 싼지 비싼지, 「간이감정」으로 가치의 범위를 몰래 알아냅니다. 「거짓 통보」는 아무것도 모르면서 아는 척하는 능력이에요. 누가 능력을 썼는지는 모두에게 보이니, 그 뒤의 입찰을 잘 지켜보세요.
- **1인칭 탁자**: 고개를 돌려 상대의 얼굴을 보고, 탁자 위의 패들·감정 도구·수첩을 직접 집어 누릅니다.
- **떠들썩한 어울리기**: 음성 채팅(누르고 말하기·켜 두기)과 문자 채팅을 쓸 수 있습니다. R 키 감정 표현에는 가운뎃손가락도 있고, 설정에서 끌 수 있어요. 빵·토마토를 던지거나, 말아 쥔 신문지로 옆자리를 때릴 수도 있습니다.
- **버릇을 배우는 AI**: 성격이 다른 AI 손님 8명이 난이도 3단계로 나옵니다. 진짜 가치를 몰래 보지 않고 시작가·소문·입찰·능력 사용만 보고 추리하며, 여러 판에 걸쳐 당신의 허풍 버릇을 기억합니다.
- **캐릭터 꾸미기**: 체형·눈·머리·수염·가면·모자·옷·장신구·목소리와 색을 바꿉니다. 온라인에서도 그대로 보여요.
- **3~8명**: 혼자서 AI와, 또는 Steam 친구 초대(포트 설정 없음)나 같은 네트워크로 친구들과 즐깁니다.
- **5개 언어**: 한국어, English, 日本語, 繁體中文, 简体中文

### English

**See through the masks.**

A round table in a dim auction hall. Every guest brought something to sell — and only the seller knows what it's really worth.
Set any starting price you like and attach a rumour. That rusty pocket watch? "Smuggled out of the royal vault," of course.

When the hammer falls for the last time, the richest guest wins: **cash left + the true value of everything you bought**. But half of your unspent buying fund is lost at the end, so sooner or later you'll have to buy something. Whose story will you believe?

**Features**

- **Bluffing is the strategy**: Decide what your items are worth, then mislead everyone with prices and rumours. Write your own item name and description, add a picture, and it appears in a gilded frame on the turntable for the whole table to see.
- **Three abilities**: *Price Check* privately tells you whether the true value is above or below the current bid. *Appraisal* reveals a range. *Fake Signal* tells you nothing but looks exactly like the real thing. Everyone sees who used an ability, so watch the bids that follow.
- **A first-person table**: Turn your head to read your rivals' faces, and reach for the paddle, the appraisal tools and your notebook right on the table.
- **Rowdy social play**: Voice chat that comes from each speaker's seat, with moving mouths (push-to-talk or open mic), plus text chat. An emote wheel includes a middle finger, which you can turn off in Settings. Throw bread rolls and tomatoes, or bonk your neighbour with a rolled newspaper.
- **AI that learns your tells**: 8 AI rivals with distinct personalities and 3 difficulty levels. They never peek at true values; they reason from prices, rumours, bids and ability use, and remember your bluffing habits across games.
- **Character customisation**: Body, eyes, hair, beard, masks, hats, outfits, accessories, voice and colours. Your look carries over to online games.
- **3–8 players**: Play solo against AI, or invite Steam friends (no port forwarding) or play over LAN.
- **5 languages**: Korean, English, Japanese, Traditional Chinese, Simplified Chinese.

### 日本語 (초안 — 원어민 검수 필요)

**短い説明**
> 自分の品の本当の価値は自分だけが知っている。ガラクタを宝物のように、宝物をガラクタのように——開始価格とひと言のハッタリで相手をだまし、相手のハッタリを見抜こう。ボイスチャットで友達と、あるいはクセを覚えるAIと遊ぶ一人称ブラフ・オークションのパーティーゲーム。

**ゲーム紹介**

**仮面の下の真実を見抜け。**

薄暗いオークション会場の丸テーブル。客はそれぞれ品物を持ち込み、その本当の価値を知っているのは持ち込んだ本人だけ。開始価格は自由に決められ、ひと言の噂も添えられます。

最後に **残ったお金 + 落札した品の本当の価値** がいちばん多い人の勝ち。ただし最初にもらった購入資金を使い残すと半分を失います。結局だれかの品を買うしかありません。だれの言葉を信じますか？

- **ハッタリこそ戦略**: 自分の品の価値を決め、開始価格と噂で相手をだまします。名前・説明を自分で書き、画像を入れると回転台の額縁に表示されます。
- **3つの能力**: 「相場チェック」「簡易鑑定」で本当の価値をこっそり探り、「フェイント」で知っているふりを。能力を使った人は全員に見えます。
- **一人称のテーブル**: 顔を向けて相手の表情を読み、パドルや道具を直接手に取ります。
- **にぎやかな交流**: ボイスチャット・テキストチャット・エモート（中指は設定でオフ可）。パンやトマトを投げたり、丸めた新聞で隣の人を叩いたり。
- **クセを覚えるAI**: 性格の違う8人のAI、難易度3段階。本当の価値を盗み見ず、価格・噂・入札・能力の使い方から推理し、あなたのハッタリのクセを覚えます。
- **キャラクターカスタマイズ** / **3〜8人**（Steamフレンド招待・LAN） / **5言語**

### 繁體中文 (초안 — 원어민 검수 필요)

**簡短說明**
> 只有你知道自己物品的真正價值。把破爛說成寶物、把寶物裝成破爛——用起標價和一句吹牛騙過對手，再看穿別人的吹牛。可與好友語音同樂，或挑戰會記住你習慣的 AI 的第一人稱吹牛拍賣派對遊戲。

**遊戲介紹**

**看穿面具下的真相。**

昏暗拍賣會場的圓桌旁，每位客人都帶來一件物品，而真正的價值只有帶來的人知道。起標價隨你定，還能附上一句傳聞。

結束時 **剩餘的錢 + 買下物品的真正價值** 最多的人獲勝。但一開始拿到的購買資金沒花完會損失一半，所以終究得買下別人的東西。你要相信誰？

- **吹牛就是戰略**：自訂物品價值，用起標價和傳聞誤導對手；可以自己寫名稱與說明並放入圖片，顯示在轉盤上的畫框裡。
- **三種能力**：「比價」「簡易鑑定」偷偷探查真正價值，「假動作」則什麼都不知道卻裝作知道。誰用了能力，大家都看得到。
- **第一人稱牌桌**：轉頭觀察對手表情，直接拿起球拍和道具。
- **熱鬧互動**：語音、文字聊天、表情動作（中指可在設定中關閉），還能丟麵包和番茄、用捲起的報紙敲鄰座。
- **會學習的 AI**：8 種個性、3 種難度；不偷看真實價值，而是從價格、傳聞、出價與能力使用推理，並記住你的吹牛習慣。
- **角色自訂** / **3–8 人**（Steam 好友邀請、區域網路） / **5 種語言**

### 简体中文 (초안 — 원어민 검수 필요)

**简短说明**
> 只有你知道自己物品的真正价值。把破烂说成宝物、把宝物装成破烂——用起拍价和一句吹牛骗过对手，再看穿别人的吹牛。可与好友语音同乐，或挑战会记住你习惯的 AI 的第一人称吹牛拍卖派对游戏。

**游戏介绍**

**看穿面具下的真相。**

昏暗拍卖会场的圆桌旁，每位客人都带来一件物品，而真正的价值只有带来的人知道。起拍价随你定，还能附上一句传闻。

结束时 **剩余的钱 + 买下物品的真正价值** 最多的人获胜。但一开始拿到的购买资金没花完会损失一半，所以终究得买下别人的东西。你要相信谁？

- **吹牛就是战略**：自定物品价值，用起拍价和传闻误导对手；可以自己写名称与说明并放入图片，显示在转盘上的画框里。
- **三种能力**：「比价」「简易鉴定」偷偷探查真正价值，「假动作」则什么都不知道却装作知道。谁用了能力，大家都看得到。
- **第一人称牌桌**：转头观察对手表情，直接拿起拍牌和道具。
- **热闹互动**：语音、文字聊天、表情动作（中指可在设置中关闭），还能扔面包和番茄、用卷起的报纸敲邻座。
- **会学习的 AI**：8 种个性、3 种难度；不偷看真实价值，而是从价格、传闻、出价与能力使用推理，并记住你的吹牛习惯。
- **角色自定义** / **3–8 人**（Steam 好友邀请、局域网） / **5 种语言**

## 지원 언어 (Steamworks 표)

| 언어 | 인터페이스 | 음성 | 자막 |
|---|---|---|---|
| 한국어 | ✔ | — | — |
| English | ✔ | — | — |
| 日本語 | ✔ | — | — |
| 繁體中文 | ✔ | — | — |
| 简体中文 | ✔ | — | — |

캐릭터 목소리는 말소리가 아닌 효과음이라 「음성」은 비워 둡니다.

## 태그 후보

Party Game · Bluffing · Social Deduction · Multiplayer · Online PvP · LAN · First-Person · Funny · Casual · Strategy · Trading · Character Customization · Stylized · Board Game-like

Steam이 고르게 하는 「플레이어 수·기능」 칸: 싱글 플레이어, 온라인 PvP, LAN PvP, 컨트롤러 일부 지원.

## 시스템 요구 사항

아래 「권장」은 실제로 시험한 컴퓨터입니다. 「최소」는 아직 낮은 사양에서 시험하지 않았으니, 출시 전에 저품질 설정으로 꼭 확인하세요.

| | 최소 (확인 필요) | 권장 (시험함) |
|---|---|---|
| 운영체제 | Windows 10 64비트 | Windows 10/11 64비트 |
| 프로세서 | 4코어 2.5GHz 이상 | Intel Core i5-12400F |
| 메모리 | 4 GB RAM | 8 GB RAM |
| 그래픽 | DirectX 11, VRAM 2 GB (GTX 960 급) | NVIDIA GeForce GTX 1080 |
| 저장 공간 | 1 GB | 1 GB |
| 네트워크 | 온라인 대전 시 | 온라인 대전 시 |
| 기타 | 마이크(음성 채팅, 선택) | 마이크(음성 채팅, 선택) |

시험 결과: GTX 1080 · 1600×900 · 고품질에서 한 판 내내 120 fps(수직 동기 상한), 메모리 약 245 MB.

## 성인 콘텐츠·등급 설문에 적을 것

- 무례한 손짓(가운뎃손가락 감정 표현). 설정의 「무례한 몸짓 그대로 보기」로 끌 수 있습니다.
- 만화 같은 가벼운 폭력: 음식을 던지고 신문지로 때립니다. 피·부상 표현은 없습니다.
- 사용자 간 소통: 음성·문자 채팅, 직접 쓴 물건 이름·설명·그림. 사람마다 음성·채팅을 끌 수 있고, 남이 넣은 그림은 설정에서 숨길 수 있습니다.
- 도박 아님: 게임 안 돈은 현실 돈과 바꿀 수 없고, 구매 아이템·뽑기가 없습니다.
- 온라인: Steam 친구 초대(Steam 릴레이)와 LAN·IP 직접 접속을 지원합니다.
