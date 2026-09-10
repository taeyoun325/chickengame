# DEVELOPMENT_LOG

PHASE 별 작업 기록. 확인하지 못한 것은 `UNKNOWN` 으로 남긴다.

---

## PHASE 0 — 프로젝트 분석

**2026-09-11 / 커밋 `0c314c3` 기준**

### 변경 내용

분석만 수행. 기능 수정 없음.

### 수정 파일

- `Docs/CURRENT_STATE.md` (신규)
- `Docs/DEVELOPMENT_LOG.md` (신규)

### 테스트 결과

- 컴파일 오류 0 (Roslyn 직접 컴파일 + Unity 배치 빌드)
- 셀프테스트 31/31 통과 (`-mode local -selftest`)
- Windows 64-bit 빌드 성공 (212 MB)
- 실행 화면 캡처로 육안 확인

### 발견된 문제

1. 손님이 NavMesh 를 쓰지 않고 직선 보간으로 이동한다 — 가구를 통과한다
2. 주문 목록을 4096 바이트 문자열로 동기화한다 — 주문이 많아질 때 한계 **UNKNOWN**
3. 승리 시 "달성 시간" 기록이 없다 — 지시서의 최종 목표인데 미구현
4. 동시 접근(같은 치킨/튀김기/주문) 테스트를 한 적이 없다 — **UNKNOWN**
5. 실제 2 대 이상 PC 로 멀티플레이를 붙여본 적이 없다 — **UNKNOWN**
6. 게임 씬이 비어 있고 가게 전체가 코드로 생성된다 — 에디터에서 맵을 눈으로 편집할 수 없다

### 해결한 문제

PHASE 0 이전 작업에서 해결된 것 (이번 세션):

1. Unity 6 내장 폰트 이름 변경으로 인한 `ArgumentException` — `UiFont` 로 일원화
2. 소품·캐릭터가 부모 배율만큼 작아지던 크기 계산 오류
3. 회전한 장식이 절반 크기로 나오던 오류
4. 손님 색 변경의 매 프레임 할당

### 남은 문제

위 "발견된 문제" 전부. PHASE 1 이후로 넘긴다.

### 완료 조건 점검

| 조건 | 상태 |
| --- | --- |
| 컴파일 오류 없음 | 통과 |
| 기존 핵심 기능 정상 | 통과 (셀프테스트 31/31) |
| 새 기능 정상 | 해당 없음 (분석 단계) |
| 네트워크 구조 정상 | 구조는 서버 권한으로 확인. 동시성은 **UNKNOWN** |
| null/reference 문제 없음 | 실행 로그 예외 0 건 |
| 중복 시스템 없음 | 통과 |
| 테스트 완료 | 통과 |
| 문서 기록 완료 | 통과 |

**PHASE 0 완료.** PHASE 1 로 이동한다.

---

## PHASE 1 — 핵심 게임 구조

### STEP A — 조사 결과

지시서가 요구한 `GameManager` / `DaySystem` / `RevenueSystem` / `ReputationSystem` 은
**이미 존재한다.** 이름만 다르다. 새로 만들면 중복 시스템이 되므로 만들지 않았다.

| 지시서 | 실제 구현 |
| --- | --- |
| GameManager | `GameFlow` (상태 기계) + `RestaurantGame` (경기 상태) |
| DaySystem | `RestaurantGame.dayTimer` / `dayLength` 600초, `EndDay()`, `StartNextDay()` |
| RevenueSystem | `RestaurantGame.Progress` — revenue, spending, 콤보, `TargetRevenue` 10,000,000 |
| ReputationSystem | `RestaurantGame.ChangeReputation()`, 0 이면 `FinishGame(false)` |

### 상태 기계 대응

| 지시서 상태 | 실제 |
| --- | --- |
| Opening | `GameState.Title` |
| Running | `GameState.Playing` |
| Rush | **상태가 아니라 이벤트다.** `RandomEventSystem` 의 러시아워가 `Playing` 위에 겹친다 |
| Closing | `GameState.Settlement` (하루 결산) |
| Victory | `GameState.Victory` |
| Shutdown | `GameState.Defeat` (평판 0) |

Rush 를 별도 상태로 승격하지 않았다. 러시아워는 영업 중에 겹쳐 일어나는 일이고,
상태로 만들면 영업 로직을 두 벌 유지해야 한다. 이벤트로 두는 편이 맞다.

### 서버 권한 경계

| 결정 주체 | 항목 |
| --- | --- |
| **서버만** | 주문 생성·완료·만료, 손님 생성·상태, 치킨 상태, 튀김기, 매출, 평판, DAY, 업그레이드, 랜덤 이벤트, 사고, 승패 |
| 클라이언트 | 입력 전송(`Request*`), 화면 그리기, 자기 아바타 이동(NetworkTransform Owner 권한) |

클라이언트가 매출·주문 완료·DAY 를 직접 바꾸는 경로는 없다. 확인함.
`RestaurantGame.Update()` 는 `KitchenNetwork.IsHostSide` 가 아니면
`UpdateHudFromNetwork()` 만 하고 즉시 반환한다.

### STEP C — 구현 (달성 시간)

지시서의 최종 목표는 "₩10,000,000 을 얼마나 빨리 달성하는가" 인데 **시간 기록이 없었다.**

- `RestaurantGame.playSeconds` — 영업 중에만 쌓인다. 일시정지·결산 화면은 제외된다.
  DAY 마다 되감기는 `dayTimer` 와 별개다.
- HUD 우상단에 `mm:ss` 로 표시
- 승리 화면에 `달성 시간` 과 최고 기록 비교 (`첫 기록입니다` / `최고 기록 경신!` / 기존 기록)
- `SaveData.bestClearSeconds`, `SaveData.playSeconds` 추가.
  `SaveProgress()` 가 SaveData 를 새로 만들어 덮어쓰므로 최고 기록을 이어받게 고쳤다
- 타이틀 화면에 최고 달성 시간 표시

### 수정 파일

- `Assets/Scripts/RestaurantGame.cs` — playSeconds, Clock(), HUD, `-startrevenue`
- `Assets/Scripts/RestaurantGame.Progress.cs` — 승리 보고, 기록 저장/이어받기, `AddRevenueForTest`
- `Assets/Scripts/SaveSystem.cs` — bestClearSeconds, playSeconds
- `Assets/Scripts/GameFlow.cs` — 타이틀 기록 줄
- `Assets/Scripts/SelfTest.cs` — 승리 경로 검증 3 항목

### STEP E — 테스트 결과

정상 플레이로 ₩10,000,000 에 도달하려면 수백 DAY 가 걸려 자동 검증이 불가능했다.
기존 `-startday` 와 같은 자리에 `-startrevenue` 를 넣고, 셀프테스트가
`AddRevenueForTest()` 로 승리 경로를 실제로 밟도록 했다.

- 컴파일 오류 0
- 셀프테스트 **34/34 통과** (기존 31 + 승리 3)
  - `영업 시간이 흐른다 (00:30)`
  - `목표 매출을 넘기면 승리한다 (현재 Victory)`
  - `달성 시간이 기록으로 남는다 (00:30)`
- 실행 캡처로 HUD 시계 `00:11` 확인
- 빌드 성공 (212 MB)

### 발견된 문제

`SaveProgress()` 가 SaveData 를 통째로 새로 만들어 저장하므로, 그대로 두면
저장할 때마다 최고 기록이 지워졌을 것이다. 구현 중에 발견해 이어받도록 고쳤다.

### 남은 문제 (PHASE 1 범위 밖)

- 멀티플레이에서 달성 시간은 **호스트 기준**이다. 접속한 플레이어 화면의 기록은 **UNKNOWN**
- 승리 화면 자체를 접속 클라이언트가 어떻게 보는지 **UNKNOWN** (`FinishRpc` 로 문자열은 가지만 실측 안 함)

### 완료 조건 점검

| 조건 | 상태 |
| --- | --- |
| 컴파일 오류 없음 | 통과 |
| 기존 핵심 기능 정상 | 통과 (31 항목 그대로 통과) |
| 새 기능 정상 | 통과 (승리 3 항목) |
| 네트워크 구조 정상 | 서버 권한 경계 문서화. 멀티 실측은 PHASE 10 |
| null/reference 문제 없음 | 실행 로그 예외 0 |
| 중복 시스템 없음 | 통과 (새 매니저를 만들지 않음) |
| 테스트 완료 | 통과 |
| 문서 기록 완료 | 통과 |

**PHASE 1 완료.** PHASE 2 로 이동한다.
