# Steamworks 설정 순서

게임 쪽 연동 코드는 들어가 있습니다. 앱을 등록한 뒤 Steamworks 웹사이트와 프로젝트에서 아래를 차례로 하면 됩니다.
지금 빌드는 Valve 시험 앱 **Spacewar(480)** 로 돕니다.

## 1. 앱 등록과 앱 번호 바꾸기

1. Steamworks 파트너에 가입하고 앱을 등록합니다(Steam Direct 수수료). 받은 **AppID**를 적어 둡니다.
2. `Assets/BluffAuction/Scripts/Runtime/Platform/Steam.cs`의 `AppId = 480`을 받은 번호로 바꿉니다.
3. 다시 빌드합니다. 바꾼 뒤에는 이렇게 동작합니다.
   - 빌드 폴더에 `steam_appid.txt`를 두지 않습니다.
   - Steam 밖에서 exe를 바로 켜면 Steam으로 다시 띄웁니다(`RestartAppIfNecessary`).
   - 개발 중 Steam 밖에서 시험하려면 빌드 폴더에 `steam_appid.txt`(내용: 앱 번호)를 직접 두세요.
4. 프로젝트 루트의 `steam_appid.txt`(에디터용, Steamworks.NET이 480으로 만듦)도 새 번호로 고칩니다.

## 2. 업적 (Stats & Achievements → Achievements)

API 이름은 정확히 아래와 같아야 합니다. 64×64 아이콘(달성/미달성 두 장)이 필요합니다.

| API 이름 | 한국어 이름 · 설명 | English name · description |
|---|---|---|
| `ACH_FIRST_WIN` | 첫 우승 · 처음으로 1등을 했다 | First Victory · Win your first auction |
| `ACH_BARGAIN` | 득템의 신 · 가치의 절반 이하로 물건을 샀다 | Bargain Hunter · Buy a lot for under half its value |
| `ACH_BLUFF` | 허풍의 달인 · 가치의 3배 넘게 내 물건을 팔았다 | Master Bluffer · Sell a lot for over 3x its value |
| `ACH_COLLECTOR` | 수집가 · 한 판에 물건 4개 이상을 샀다 | Collector · Win 4 or more lots in one game |
| `ACH_DEBT` | 빚쟁이 우승 · 대출을 받고 우승했다 | Leveraged · Win a game after taking a loan |
| `ACH_HARD` | 상어 사냥꾼 · 어려움 난이도에서 우승했다 | Shark Hunter · Win on Hard difficulty |
| `ACH_FULL` | 만석 · 8명 경매에서 우승했다 | Full House · Win an 8-player auction |
| `ACH_SPENDER` | 깔끔한 지출 · 벌금 없이 게임을 마쳤다 | Not a Coin Wasted · Finish with no unspent penalty |

일본어·중국어 이름은 `Runtime/Loc/StringsAsia.cs`의 `ach_*` 줄에 있습니다.
게임 안에서 이미 딴 업적은 Steam에 처음 붙을 때 한꺼번에 올라갑니다.

## 3. 통계 (Stats & Achievements → Stats)

| API 이름 | 종류 | 설명 |
|---|---|---|
| `games` | INT, 증가만 | 끝까지 마친 판 수 (한 판 끝날 때 Steam 값에 +1) |
| `wins` | INT, 증가만 | 1등 횟수 (+1) |

업적·통계를 다 만들었으면 **Publish**를 눌러야 적용됩니다.

## 4. 친구 목록 상태 (Rich Presence)

게임은 `status` 키에 글을 넣습니다(예: 「대기실 (3/8)」). 지역화 토큰 파일 없이도 친구 목록의 「게임 정보 보기」에 보입니다.
친구 목록에 바로 보이게 하려면 Steamworks의 Rich Presence 지역화 파일에 `#Status` 토큰을 만들고, 코드에서 `steam_display`를 설정하는 작업을 더 하면 됩니다(선택).

`connect` 키에는 `+connect_lobby <로비>`를 넣습니다. 그래서 친구 목록의 「게임 참가」로 방에 바로 들어옵니다.

## 5. 네트워크

- 별도 설정은 필요 없습니다. Steam Networking Sockets(릴레이)와 친구 전용 로비만 씁니다.
- 꼭 직접 해 볼 것: Steam 계정 두 개(두 PC)로 아래를 해 봅니다.
  - 방 만들기 → 「Steam 친구 초대」 → 친구가 수락 → 대기실 → 한 판
  - 게임이 꺼진 상태에서 초대를 수락했을 때(+connect_lobby)
  - 친구 목록의 「게임 참가」
  - 한 계정으로는 P2P 연결을 할 수 없어 자동 시험이 이 부분을 대신하지 못합니다.

## 6. 빌드 올리기 (SteamPipe)

1. Steamworks SDK의 `tools/ContentBuilder`를 받습니다.
2. Windows 64비트 depot을 하나 만들고, 그 depot에 `Builds/Windows/` 폴더 내용을 올립니다.
   - `steam_appid.txt`가 없어야 합니다(앱 번호를 바꾸면 빌드가 알아서 뺍니다).
   - `*_DoNotShip` 폴더는 빌드 끝에 지워집니다.
3. 실행 옵션: `BluffAuction.exe`, 운영체제 Windows, 64비트.
4. `steamcmd`나 SteamPipe GUI로 업로드한 뒤 기본 브랜치에 설정합니다.

## 7. 확인

```bash
Builds/Windows/BluffAuction.exe -screen-fullscreen 0 -autoshots <폴더> -scenario steam
```

`<폴더>/log.txt`에 다음이 남습니다.
- 초기화 결과
- 업적 왕복: 켰다가 되돌립니다. 이미 딴 업적은 건드리지 않습니다.
- 친구 수
- Steam 로비 번호와 데이터

정식 앱 번호에서는 시험 매핑이 꺼집니다. 그래서 `ACH_WIN_ONE_GAME` 대신 위 표의 업적 이름으로 확인해야 합니다.
