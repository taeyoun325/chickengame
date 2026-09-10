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

---

## PHASE 2 — 플레이어 및 상호작용

### STEP A — 조사 결과

지시서가 요구한 12 가지 행동을 실제 코드와 대조했다.

| 행동 | 상태 | 근거 |
| --- | --- | --- |
| 이동 | 있음 | `PlayerMotor` |
| 집기 | 있음 | `TryPickUpNearbyFood` |
| 내려놓기 | 있음 | `RequestDrop` → `DropRpc` |
| 재료 가져오기 | 있음 | 냉장고 스테이션 |
| 튀김기 사용 | 있음 | 튀김기 스테이션 |
| 소스 사용 | 있음 | `RequestSauceCycle` → `SauceRpc` |
| 포장 | 있음 | 포장대 스테이션 |
| 주문 전달 | 있음 | 계산대 스테이션 |
| 배달 | 있음 | 배달대 스테이션 |
| 업그레이드 | 있음 | `RequestUpgrade` → `UpgradeRpc` |
| **소화기** | **결함** | 아래 참조 |
| **청소** | **미구현** | 아래 참조 |

**고정 직업은 존재하지 않는다.** 모든 플레이어가 같은 `PlayerInteraction` 을 가지며
역할 개념 자체가 코드에 없다. 지시서 요구와 일치한다. 변경 없음.

### 발견된 결함 두 가지

**1. 접속한 플레이어는 불을 끌 수 없었다.**

`PlayerInteraction.Interact()` 가 소화기 처리를 클라이언트 분기보다 **먼저** 했다.
클라이언트에서 `RestaurantGame.TryExtinguishNearby()` 를 로컬 호출하는데,
`HazardSystem.Update()` 는 호스트에서만 돌아 클라이언트의 `fires` 목록은 항상 비어 있다.
따라서 항상 false 를 반환하고, 불을 끄는 RPC 도 없었다.
호스트만 소화기를 쓸 수 있는 상태였다.

**2. 기름은 아무도 청소할 수 없었다.**

`OilPuddle` 은 40 초 뒤 저절로 사라질 뿐, 지우는 경로가 없었다.
지시서의 "청소" 행동이 없고, PHASE 7 이 요구하는 "사고는 복구 가능해야 한다" 도 충족하지 못했다.

### STEP C — 구현

- `KitchenNetwork.RequestExtinguish()` / `ExtinguishRpc` 추가.
  서버가 `actor.CarryingExtinguisher` 를 확인한 뒤 처리한다
- `KitchenNetwork.RequestClean()` / `CleanRpc` 추가.
  서버가 `actor.HandsFree` 를 확인한 뒤 처리한다
- `HazardSystem.TryClean()`, `RestaurantGame.TryCleanNearby()` 추가
- `HazardSystem.SpillOilAt(Vector3)` 를 공개로 분리.
  PHASE 7 의 "떨어뜨림 → 기름" 연쇄에서 다시 쓴다
- `OilPuddle` 에 정적 목록을 두어 호스트·클라이언트 양쪽에서 발밑 기름을 알 수 있게 했다
- `PlayerInteraction.CanClean` — **손이 비어 있고 발밑에 기름이 있을 때만** 청소한다.
  손에 뭘 들고 있으면 평소대로 스테이션과 상호작용하므로 기름이 조리 동선을 막지 않는다
- `AimHud` 가 `[E] 기름 닦기` 를 띄운다. 안내가 없으면 닦을 수 있다는 걸 알 방법이 없다

### STEP E~F — 테스트에서 잡은 버그

첫 실행에서 `닦은 자리에 기름이 없다` 가 **실패**했다.

`Object.Destroy` 는 프레임 끝에야 실제로 지운다. 그래서 닦은 직후에도 그 자리가
여전히 기름으로 판정됐다 — **방금 닦은 기름에 미끄러질 수 있었다는 뜻이다.**

`OilPuddle.Remove()` 를 만들어 목록에서 먼저 빼고 나서 despawn 하도록 고쳤다.
수명이 다해 사라지는 경로도 같은 함수를 쓴다.

### 수정 파일

- `Assets/Scripts/KitchenNetwork.cs`
- `Assets/Scripts/PlayerInteraction.cs`
- `Assets/Scripts/HazardSystem.cs`
- `Assets/Scripts/OilPuddle.cs`
- `Assets/Scripts/RestaurantGame.Kitchen.cs`
- `Assets/Scripts/AimHud.cs`
- `Assets/Scripts/SelfTest.cs`

### 테스트 결과

- 컴파일 오류 0
- 셀프테스트 **38/38 통과** (기존 34 + 청소 4)
- 빌드 성공

### 남은 문제

- 소화기·청소 RPC 를 **실제 클라이언트로 확인하지 못했다** — **UNKNOWN**.
  단일 프로세스에서는 서버 분기만 실행된다. PHASE 10 에서 2 대로 검증한다
- 쓰레기통은 있으나 "쓰레기가 쌓인다" 는 개념은 없다. 지시서의 사고 목록에 있는
  "쓰레기 문제" 는 미구현 — PHASE 7 대상

### 완료 조건 점검

| 조건 | 상태 |
| --- | --- |
| 컴파일 오류 없음 | 통과 |
| 기존 핵심 기능 정상 | 통과 (34 항목 그대로 통과) |
| 새 기능 정상 | 통과 (청소 4 항목) |
| 네트워크 구조 정상 | 서버 검증 경로 추가. 실측은 **UNKNOWN** |
| null/reference 문제 없음 | 실행 로그 예외 0 |
| 중복 시스템 없음 | 통과 |
| 테스트 완료 | 통과 |
| 문서 기록 완료 | 통과 |

**PHASE 2 완료.** PHASE 3 으로 이동한다.
