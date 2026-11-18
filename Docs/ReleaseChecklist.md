# 출시까지 해야 할 일

지금 상태: **0.11.0 시험판** (`Release/BluffAuction-0.11.0-win64.zip`).
Steam 연동은 Valve 시험 앱 Spacewar(480)로 돕니다. 아래에서 ☐ 는 직접 하셔야 하는 일, ☑ 는 끝난 일입니다.

## 1. 지금 바로: 친구와 시험판 돌리기

- ☐ zip 을 친구 2~4명에게 보내고, **Steam 초대로 한 판** 해 보기. 계정 하나로는 시험할 수 없는 부분입니다.
  - 방장이 「방 만들기」를 누르고 대기실에서 「Steam 친구 초대」를 보냅니다. 친구는 초대를 수락합니다.
  - 확인할 것: 들어와지는지, 음성이 들리는지, 채팅(한글), 던지기·신문지, 끝까지 결과가 같은지.
  - 문제가 있으면 각자 `Player.log` 를 받아 주세요. 위치는 zip 안 README.txt 에 있습니다.
- ☐ 직접 손으로 확인하기
  - 실제 키보드로 한글 채팅 치기
  - 마우스 감도
  - 패드로 한 판
  - 마이크 시험(설정 → 마이크 시험)
- ☐ 사양이 낮은 PC(내장 그래픽 노트북 등)에서 실행해 보기. `Docs/StorePage.md` 사양표의 「최소」 칸을 이 결과로 정합니다.

## 2. Steamworks 가입과 앱 등록

- ☐ Steamworks 파트너 가입: 신원·세금·은행 정보를 입력합니다. 승인까지 며칠 걸릴 수 있습니다.
- ☐ Steam Direct 앱 등록비 100달러를 내고 **AppID** 를 받습니다.
  - 같이 생기는 **Depot ID**(보통 AppID+1)도 적어 둡니다.
- ☐ `Assets/BluffAuction/Scripts/Runtime/Platform/Steam.cs` 의 `AppId = 480` 을 받은 번호로 바꾸고 다시 빌드합니다.
  - 빌드는 `Tools/package.ps1` 로 합니다.
  - 번호를 바꾸면 빌드가 `steam_appid.txt` 를 빼고, Steam 밖에서 켜면 Steam 으로 다시 띄웁니다.
- ☐ Steamworks 에 업적 8개와 통계 2개를 만들고 **Publish** 합니다.
  - 이름표는 `Docs/SteamworksSetup.md` 에 있습니다.
  - ☑ 업적 아이콘은 `Docs/Store/achievements/` 에 있습니다: 달성/미달성, 256px 과 64px.
- ☐ (선택) Steam 클라우드 저장
  - Steamworks → Steam Cloud → Auto-Cloud 를 켭니다.
  - 루트 `WinAppDataLocalLow`, 하위 경로 `Hyeophap Games/Bluff Auction`, 파일 `save.json` 을 등록합니다. 코드는 고칠 필요가 없습니다.

## 3. 상점 페이지

- ☑ 설명문: `Docs/StorePage.md` (한국어·영어·일본어·번체·간체)
  - ☐ 일본어·중국어는 원어민 검수를 받습니다.
- ☑ 캡슐·라이브러리 그림: `Docs/Store/` (영어·한국어)
- ☑ 스크린샷 후보: `Docs/Screenshots/`
  - ☐ 1920×1080 이상으로 5장 이상 고릅니다.
- ☐ 트레일러(30초~1분)
  - 게임을 켜고 OBS 등으로 한 판을 녹화해 편집합니다.
  - 추천 장면: 허풍 문구 → 입찰 경쟁 → 능력 → 토마토·신문지 → 진짜 가치 공개.
- ☐ 가격과 할인 정책, 출시 국가를 정합니다.
- ☐ 태그, 장르, 지원 언어 표를 입력합니다. `Docs/StorePage.md` 에 후보가 있습니다.

## 4. 법무·등급

- ☐ `Docs/PrivacyPolicy.md` 의 `[회사 이름]`·`[연락처 이메일]`·`[시행일]` 을 채워 웹에 올리고, 주소를 Steamworks 에 넣습니다.
- ☐ IARC 연령 등급 설문을 작성합니다. 무례한 손짓, 가벼운 만화 폭력, 사용자 간 음성·채팅·그림이 있고 도박은 없습니다(`Docs/StorePage.md` 끝).
- ☐ (선택) 코드 서명 인증서를 받습니다. Steam 밖에서 받은 사람에게 뜨는 SmartScreen 경고를 줄입니다. Steam 으로 받으면 필요 없습니다.

## 5. 출시 순서 (Valve 일정)

1. ☐ 상점 페이지를 검토 요청 → 「출시 예정(Coming Soon)」으로 공개합니다. **출시일 최소 2주 전**에 공개해야 합니다.
2. ☐ 빌드를 올립니다: `Tools/SteamPipe/upload.ps1 -AppId … -DepotId … -SteamCmd … -User …`
   - steamcmd 는 Steamworks SDK 에 들어 있습니다.
   - Steamworks → SteamPipe → Builds 에서 기본 브랜치에 겁니다.
3. ☐ 빌드 검토를 요청합니다. Valve 검토는 보통 며칠 걸립니다.
4. ☐ 출시 버튼을 누릅니다.

## 6. 출시 뒤

- ☐ 첫 주에는 리뷰·토론 게시판의 버그를 확인합니다. 고친 빌드는 다시 `Tools/package.ps1` → `upload.ps1` 로 올립니다.
- ☐ 버전을 올립니다(`ProjectSetup.Version`). 온라인은 같은 버전끼리만 붙으므로, 고칠 때마다 올려야 섞이지 않습니다.
