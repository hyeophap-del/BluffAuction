# 개인정보처리방침 초안 — 허풍 경매장 (Bluff Auction)

> 출시 전에 할 일: `[회사 이름]`, `[연락처 이메일]`, `[시행일]`을 채우고 웹 페이지로 올린 뒤 Steamworks에 주소를 넣습니다.
> 「Steam을 통한 정보」 문단은 지금 들어간 Steamworks 기능(업적·통계·친구 목록·로비·릴레이·상태 표시)에 맞춰 두었습니다. 기능을 더하면 같이 고칩니다.
> 법률 자문이 아닙니다. 판매 지역(한국 개인정보보호법, EU GDPR 등)에 맞는지 확인을 받으세요.

시행일: [시행일]
운영자: [회사 이름] (이하 「저희」) · 문의: [연락처 이메일]

## 1. 요약

- 허풍 경매장은 **계정이 없고, 광고·분석 도구를 넣지 않았으며, 저희가 운영하는 서버도 없습니다.**
- 게임 기록과 설정은 **여러분의 컴퓨터에만** 저장됩니다.
- 온라인으로 함께 할 때는 같은 방의 다른 플레이어에게 이름·생김새·채팅·음성 등이 **직접** 전달됩니다. 저희는 그 내용을 받거나 저장하지 않습니다.

## 2. 컴퓨터에 저장되는 정보

| 무엇 | 어디 | 왜 |
|---|---|---|
| 설정(언어·소리·화면·마이크 방식·감도 등), 기록·업적, 마지막 게임 설정, 캐릭터 꾸미기, AI가 배운 상대 버릇 | `%USERPROFILE%\AppData\LocalLow\Hyeophap Games\Bluff Auction\save.json` | 다음에 켤 때 이어서 쓰기 위해 |
| 마지막으로 접속한 주소 | Windows 레지스트리 (Unity PlayerPrefs) | 접속 칸을 미리 채우기 위해 |

이 파일들은 여러분의 컴퓨터를 떠나지 않습니다. 게임을 지우거나 위 파일을 지우면 모두 사라집니다.

물건에 넣은 그림 파일은 고른 순간 읽어 줄인 사본만 메모리에 두고, 게임이 따로 저장하지 않습니다.

## 3. 온라인 플레이에서 주고받는 정보

온라인 방은 한 플레이어의 컴퓨터(방장)가 열고, 나머지가 그 컴퓨터에 접속하는 방식입니다. Steam 친구는 Steam 릴레이를 거쳐, 같은 네트워크나 IP 주소로는 직접 접속합니다.
이때 같은 방에 있는 플레이어끼리 다음 정보를 주고받습니다.

- 표시 이름과 캐릭터 생김새
- 게임 행동(등록·입찰·능력 사용 등)
- 문자 채팅
- 음성: **마이크를 켰을 때만**(누르고 말하기 V를 누르는 동안, 또는 M으로 켜 둔 동안) 보냅니다.
- 감정 표현, 던지기·때리기 같은 장난
- 직접 쓴 물건의 이름·설명·그림

LAN·IP 직접 접속에서는 방장과 접속한 플레이어가 연결을 위해 서로의 **IP 주소**를 알게 됩니다(Steam 릴레이로 들어온 플레이어끼리는 IP를 주고받지 않습니다). LAN 방 찾기는 같은 네트워크에 방 이름과 인원을 알립니다.

이 정보는 그 판 동안만 쓰이고, 게임은 다른 플레이어의 채팅·음성·그림을 기록해 저장하지 않습니다.
다만 다른 플레이어가 화면을 녹화하는 등 자기 컴퓨터에서 하는 일은 저희가 막을 수 없습니다. 모르는 사람과 할 때는 개인정보를 말하지 마세요.

**스스로 지키는 방법**
- 설정 → 음성 채팅: 마이크 방식(누르고 말하기 / 켜 두기), 마이크 시험, 다른 사람 목소리 크기
- 참가자 목록(대기실, 게임 중 Tab)의 스피커 단추: 그 사람의 음성·채팅 끄기
- 설정: 「남이 넣은 물건 그림 보기」, 「무례한 몸짓 그대로 보기」 끄기

## 4. Steam을 통한 정보

Steam에서 구매·실행하면 Valve가 자체 방침에 따라 정보를 처리합니다(https://store.steampowered.com/privacy_agreement/).
Steam이 켜져 있으면 게임은 Steamworks로 다음을 합니다. 이 정보는 Steam(Valve)과 여러분의 컴퓨터 사이에서만 오가고 저희에게 오지 않습니다.

- Steam ID와 Steam 이름 읽기: 이름 칸이 비어 있으면 Steam 이름을 표시 이름으로 씁니다.
- 친구 목록 읽기: 초대 창과 「Steam 친구의 방」 목록을 보여 주기 위해 컴퓨터 안에서만 씁니다.
- 업적과 통계(판 수·승리 수)를 Steam 계정에 기록합니다.
- 친구 목록에 보이는 상태(예: 「대기실 3/8」)를 설정하고, 방을 만들면 친구 전용 Steam 로비(방 이름·인원·게임 버전)를 엽니다.
- Steam 친구와의 게임 메시지는 Steam 릴레이 서버를 거칩니다.

## 5. 엔진(Unity)

게임은 Unity 엔진으로 만들었습니다. 빌드에서 Unity 분석·클라우드 진단·충돌 보고를 껐고, 하드웨어 통계 전송도 끄도록 설정했습니다(엔진 라이선스가 허용하는 범위). 자세한 내용은 Unity 방침을 참고하세요(https://unity.com/legal/privacy-policy).

## 6. 어린이

게임은 사용자 간 음성·문자 채팅이 있으므로 13세 미만(지역에 따라 그 이상)은 보호자의 지도 아래 쓰기를 권합니다. 저희는 나이를 포함해 어떤 개인정보도 수집하지 않습니다.

## 7. 바뀔 때

이 방침이 바뀌면 이 페이지와 Steam 소식으로 알립니다.

---

# Privacy Policy (draft) — Bluff Auction

Effective date: [Effective date] · Operator: [Company name] ("we") · Contact: [Contact email]

## 1. Summary

- Bluff Auction has **no accounts, no advertising or analytics SDKs, and no servers run by us.**
- Your progress and settings are stored **only on your computer**.
- In online games, your name, look, chat, voice and similar data go **directly** to the other players in your room. We never receive or store them.

## 2. Data stored on your computer

| What | Where | Why |
|---|---|---|
| Settings, records and achievements, last game setup, character look, opponent habits learned by the AI | `%USERPROFILE%\AppData\LocalLow\Hyeophap Games\Bluff Auction\save.json` | To continue where you left off |
| Last address you joined | Windows registry (Unity PlayerPrefs) | To pre-fill the join field |

These files never leave your computer. Deleting them (or the game) removes everything.
Pictures you attach to your items are read once, downscaled and kept in memory only; the game does not save them.

## 3. Data exchanged in online play

One player's computer hosts the room and the others connect to it: Steam friends through the Steam relay network, others directly over LAN or an IP address. Players in the same room exchange:

- display name and character appearance
- game actions (registering, bidding, abilities)
- text chat
- voice — **only while your microphone is on** (holding push-to-talk V, or while toggled on with M)
- emotes and pranks (throwing, bonking)
- names, descriptions and pictures of custom items

With LAN or direct IP connections, the host and the players who join learn each other's **IP addresses** in order to connect (players who join through the Steam relay do not exchange IP addresses). LAN discovery announces the room name and player count on your local network.

This data is used only for that game session; the game does not record or store other players' chat, voice or pictures. We cannot control what other players do on their own computers (for example, screen recording), so please don't share personal information with strangers.

**Your controls**
- Settings → Voice chat: microphone mode, mic test, other players' voice volume
- The speaker button in the player list (lobby, or Tab in game): mute a player's voice and chat
- Settings: turn off "Show others' item pictures" and "Show rude gestures"

## 4. Steam

When you buy or launch the game through Steam, Valve processes data under its own policy (https://store.steampowered.com/privacy_agreement/).
While Steam is running, the game uses Steamworks to do the following. This data travels only between Steam (Valve) and your computer; it never reaches us.

- Read your Steam ID and Steam name: if the name field is empty, your Steam name is used as your display name.
- Read your friends list, locally, to show the invite window and the "Friends' rooms on Steam" list.
- Record achievements and stats (games played, wins) on your Steam account.
- Set the status your friends see (for example "In a lobby (3/8)") and, when you host, open a friends-only Steam lobby (room name, player count, game version).
- Route game messages with Steam friends through Steam's relay servers.

## 5. Engine (Unity)

The game is built with Unity. Unity Analytics, Cloud Diagnostics and crash reporting are disabled, and hardware-statistics submission is turned off to the extent the engine licence allows. See Unity's policy at https://unity.com/legal/privacy-policy.

## 6. Children

Because the game includes voice and text chat between players, we recommend parental guidance for children under 13 (or the age required in your region). We do not collect any personal data, including age.

## 7. Changes

We will announce changes to this policy on this page and through Steam news.
