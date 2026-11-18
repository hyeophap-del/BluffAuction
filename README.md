# 허풍 경매장 (Bluff Auction)

각자 가져온 물건의 진짜 가치는 본인만 알고, 시작가와 한마디로 허풍을 쳐서 서로 비싸게 팔고 싸게 사는 게임입니다.

- 엔진: Unity 6000.5.1f1, URP 17.5 (Forward+, HDR, SSAO)
- 3~8명: AI 손님 8명(성격 8가지 × 난이도 3단계), 온라인(호스트가 판정)
  - Steam 친구: 방장이 방을 만들면 친구 전용 Steam 로비가 열림. 초대(게임 안 친구 목록·Steam 오버레이), 친구 목록 「게임 참가」, 온라인 화면의 「Steam 친구의 방」으로 들어옴. 메시지는 Steam 릴레이(포트 열기 없음)
  - 같은 네트워크(LAN 찾기)·IP 직접 접속(TCP 27615). 한 방에서 Steam 친구와 LAN 친구를 같이 받음
- Steam: 업적·통계(판 수·승리), 친구 목록 상태(메뉴/AI와 경매/대기실 n/8/온라인 경매), Steam 이름을 기본 이름으로
- 1인칭 시점(라이어스 바처럼): 마우스로 고개를 돌려 상대를 보고, 탁자 위 카드·장부·수첩·능력 도구·패들을 직접 보고 누름
- 어울리기:
  - 채팅(대기실 포함)과 음성 채팅(누르고 말하기 V / 켜 두기 M, 말하는 사람 머리에서 3D 소리, 입이 움직임)
  - 감정 표현 바퀴(R): 손 흔들기·엄지·박수·브이·비웃기·얼굴 감싸기·화내기·가운데 손가락. 무례한 몸짓은 설정에서 끌 수 있음
  - 탁자 위 물건 던지기(빵·사과·종이 뭉치·토마토·코르크)와 근접 무기(말아 쥔 신문). 둘 다 조준점 아래 사람이 대상이며, 겨누면 조준점이 바뀌고 아래에 「클릭: ○○님 때리기」가 뜸. 신문은 바로 옆자리까지
  - 맞으면: 토마토는 씨가 보이는 주황 과즙, 사과는 과육 조각 (만화풍, 피처럼 보이지 않게)
  - 사람마다 음성·채팅 끄기(커서 모드 참가자 목록·대기실), 설정의 마이크 시험(내 목소리 듣기)
  - AI 손님도 채팅·몸짓·맞은 것에 반응하고 되갚음
- 지금 물건의 판매자(파란 가격표 「판매」), 최고 입찰(★ 금액), 이 물건에 능력을 쓴 사람(×횟수)을 가격 띠 아래와 이름표에 계속 표시
- 캐릭터 꾸미기: 체형·피부·눈·머리·수염·가면·모자·옷 6종·목 장식·안경·귀걸이·목소리와 색. 온라인에서도 그대로 보임
- 직접 쓴 물건: 이름·설명에 그림 파일(PNG/JPG)을 넣으면 경매 때 회전판 위 금테 액자로 모두에게 보임
- 한국어 / English / 日本語 / 繁體中文 / 简体中文
- 모델·텍스처·소리·음악은 모두 코드로 생성(외부 아트 자산 없음, 글꼴만 외부)

## 조작

| 동작 | 키보드·마우스 | 패드 |
|---|---|---|
| 둘러보기 | 마우스 이동 | 오른쪽 스틱 |
| 입찰 | Space · 패들 클릭 | A |
| 올릴 금액 | 마우스 휠 · 1~4 | 십자키 ←→ |
| 능력 (시세 비교 · 간이감정 · 거짓 통보) | Q W E · 탁자 위 도구 클릭 | X · Y · (도구를 보고 RB) |
| 가리킨 물건 누르기 | 왼쪽 클릭 | RB |
| 가까이 보기 | 오른쪽 버튼 누르고 있기 | LT |
| 한마디 (빠른 문구) | T | — |
| 채팅 | Enter | — |
| 마이크 켜기/끄기 · 누르고 말하기 | M · V | — |
| 감정 표현 바퀴 | R 누른 채 마우스로 고르고 놓기 (1~8) | — |
| 물건·무기 집기 / 던지기·내려놓기 | F | — |
| 던지기 · 무기 휘두르기 | 왼쪽 클릭 (가리킨 사람에게) | — |
| 커서 모드 (자세한 패널) | Tab | — |
| 일시정지 | Esc | Start |

마이크는 M 이나 V 를 쓸 때만 열리고, 자동 시험(`-autoshots`)에서는 진짜 마이크 대신 시험용 소리를 씁니다.

## 빌드

```bash
powershell -ExecutionPolicy Bypass -File Tools/build.ps1
```

`Builds/Windows/BluffAuction.exe`가 만들어집니다. 빌드 과정에서 다음을 모두 다시 만듭니다.

- 프로젝트 설정(ProjectSetup)
- 재질·파티클·글꼴·UI 텍스처·소리(ContentBuilder)
- 씬(BuildScript)

라이선스 고지(`Licenses/`, `ThirdPartyNotices.txt`)도 실행 파일 옆에 복사됩니다.

빌드 없이 컴파일 오류만 빠르게 볼 때는 다음을 씁니다.

```bash
powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1
```

## 자동 시험

```bash
Builds/Windows/BluffAuction.exe -screen-fullscreen 0 -autoshots <폴더> -scenario play
```

| scenario | 내용 |
|---|---|
| `quick` | 초상화·메뉴 화면 촬영 |
| `tour` | 모형·구도 확인용 촬영 여행 |
| `play` | AI 대신 한 판 끝까지 자동 진행 + 1인칭 둘러보기 촬영 |
| `models` | 손님 전신·얼굴·감정 표현·손 모양·경매품을 촬영장에서 찍기 |
| `custom` | 꾸미기 화면의 부위마다 찍기 |
| `social` | 채팅·감정 표현·던지기·근접 무기·능력/입찰 표시·직접 넣은 그림 |
| `table` | 1인칭 탁자 위 도구·던질 물건 가까이 + 맞는 효과 |
| `store` | Steam 캡슐·라이브러리 그림 (`-storelang en\|ko`, `-storeposes <파일>`로 구도 바꾸기) → `<폴더>/store/` |
| `nethost` / `netjoin` | 같은 PC에서 두 개를 띄워 온라인 한 판. 둘 다 `-steamnet`을 붙이면 Steam 소켓 전송 계층으로(같은 계정끼리는 P2P가 안 돼 127.0.0.1 IP로 붙음) |
| `achicons` | Steam 업적 아이콘(달성·미달성, 256·64px) → `<폴더>/achievements/` |
| `steam` | Steam 연동 확인: 초기화·업적 왕복(켰다가 되돌림, 이미 딴 업적은 그대로)·친구 목록·Steam 로비·초대 창 (Steam이 켜져 있어야 함) |

결과로 `<폴더>`에 스크린샷과 `log.txt`(이벤트·경고·오류)가 남습니다.
자동 시험은 Steam 업적·통계를 올리지 않습니다(`steam` 시나리오의 왕복 확인만 예외).
`log.txt`에는 10초마다 프레임 시간(평균·느린 1%·최악)과 메모리도 `perf:` 줄로 기록됩니다.
GTX 1080 · 1600×900 · 고품질에서 한 판 내내 120 fps(수직 동기 상한)를 유지했습니다.

규칙 엔진·AI 균형 시험(엔진 없이 수천 판)은 다음으로 돌립니다.

```bash
powershell -ExecutionPolicy Bypass -File Tools/Sim/run-sim.ps1
```

## 구조

```
Assets/BluffAuction/Scripts/
  Core/        규칙 엔진 (Unity 참조 없음): 명령 → 상태 → 이벤트, 좌석별로 비밀을 가린 GameView
  Core/AI/     AI: 가치 추정(사전분포+시작가·소문·능력 결과), 성격, 상대 허풍 버릇 기억
  Runtime/Art  절차적 메시(MeshKit)·모델·재질 라이브러리
  Runtime/World    경매장·조명·후처리·카메라(시네마틱 + 1인칭)
  Runtime/Characters  손님 모형·몸짓·초상화 촬영
  Runtime/Table    1인칭 탁자 위 물건(카드·장부·수첩·도구·패들·가격판), 가리키기·누르기
  Runtime/Game     세션(싱글·호스트·클라이언트), 연출(Presenter), 저장, 업적
  Runtime/UI       UI Toolkit 화면 (코드로 구성, UI/Game.uss)
  Runtime/Net      전송 계층(TCP · Steam 소켓 · 둘을 묶는 MultiTransport), 방(NetRoom), LAN 찾기(UDP 27616), 게임 포트 TCP 27615
  Runtime/Loc      다국어 문자열
  Runtime/Platform Steam 연동 (Steam: 초기화·업적·통계·상태·초대 / SteamLobby: 로비·친구 목록)
  Editor/      설정·자산 생성·빌드
Packages/com.rlabrecque.steamworks.net   Steamworks.NET 2025.164.1 (내장 패키지, STEAMWORKS_NET 정의는 ProjectSetup이 넣음)
```

저장 파일은 `%USERPROFILE%/AppData/LocalLow/Hyeophap Games/Bluff Auction/save.json`입니다. 설정·기록·업적과 AI가 배운 상대 버릇이 들어 있습니다.

## 배포

```bash
powershell -ExecutionPolicy Bypass -File Tools/package.ps1
```

빌드한 뒤 `Release/BluffAuction-<버전>-win64.zip`을 만듭니다(플레이어용 `README.txt` 포함, `*_DoNotShip` 없음).
Steam에 올릴 때는 `Tools/SteamPipe/upload.ps1`을 씁니다(앱 등록 뒤).

## 스팀 출시 전 남은 일

`Docs/ReleaseChecklist.md`에 순서대로 정리했습니다.

- 친구와 시험판 돌리기(두 계정 Steam 초대)
- Steamworks 가입과 AppID 등록
- 업적·통계 등록(아이콘은 `Docs/Store/achievements/`)
- 상점 페이지와 트레일러
- 개인정보처리방침 게시, IARC 등급
- 출시 예정 페이지(출시 2주 전) → 빌드 업로드·검토 → 출시

Steamworks 쪽 세부 설정은 `Docs/SteamworksSetup.md`에 있습니다.
화면 비율은 16:9·21:9·4:3·16:10을 자동 촬영으로 확인했습니다(UI는 높이 기준으로 늘어남).

## 라이선스

외부 것은 글꼴과 Steamworks.NET뿐이며 `Docs/ThirdPartyNotices.txt`에 정리했습니다.

| 구성 요소 | 라이선스 |
|---|---|
| Noto Sans CJK | OFL 1.1 |
| Oswald | OFL 1.1 |
| Roboto Slab | Apache 2.0 |
| Steamworks.NET 2025.164.1 | MIT |
| steam_api64.dll (Steamworks SDK) | Steamworks SDK 약관에 따른 재배포 |
