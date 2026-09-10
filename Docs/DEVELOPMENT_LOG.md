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

---

## PHASE 3 — 치킨 조리 시스템

### STEP A — 상태 모델 대조

지시서의 상태 목록을 실제 구현과 맞춰 봤다.

| 지시서 | 실제 | 판단 |
| --- | --- | --- |
| Raw | `FoodState.Raw` | 그대로 |
| Preparing | 없음 | 냉장고에서 꺼내면 바로 Raw 다. 중간 단계를 두어도 플레이어가 할 일이 늘지 않는다 |
| Cooking | `FoodState.Frying` | 이름만 다름 |
| Cooked | `FoodState.Cooked` | 그대로 |
| Seasoning | `sauced` 플래그 | 양념은 순간 동작이라 상태가 아니라 속성이 맞다 |
| Ready | `ReadyToPack` (계산 속성) | 상태 중복 없이 파생된다 |
| Burnt | `FoodState.Burnt` | 그대로 |
| Dropped | `dirty` 플래그 | 바닥에 닿은 치킨. 상태와 직교하는 성질이라 플래그가 맞다 |
| Wasted | 삭제 | 쓰레기통에 넣으면 사라진다. 남길 상태가 아니다 |

**열거형을 늘리지 않았다.** Preparing / Seasoning 을 상태로 만들면 전이 표만 커지고
플레이어가 하는 일은 그대로다. 기존 모델이 필요한 구분을 이미 전부 표현한다.

튀김기 상태(Idle / Cooking / Cooked / Overcooked / Fire)도 마찬가지로
`Station.StoredFood` + 음식 상태 + `HazardSystem.IsOnFire()` 에서 파생되며,
스테이션 이름표가 이미 `넣기` / `꺼내기` / `생닭 필요` 로 읽어 준다.

### 동시 접근 조사

튀김기 자체는 **안전하다.** 모든 조작이 RPC 로 서버에 모여 한 줄로 처리되고,
`Station.Clear()` 가 즉시 자리를 비우므로 두 번째 요청은 빈 튀김기를 본다.
넣을 때도 `StoredFood != null` 을 먼저 본다.

### 발견된 결함 — 줍기에 소유권 검사가 없었다

`WorldRegistry.NearestLooseFood()` 는 `Frying` 만 걸렀다. 그래서:

1. **남의 손에 든 치킨을 옆 사람이 주워 갈 수 있었다.**
   조준 광선은 들고 있는 음식의 콜라이더가 꺼져 있어 못 맞히지만,
   가까우면 잡히는 보조 경로에는 검사가 없었다
2. **스테이션에 올려둔 치킨을 절차 없이 집어 갈 수 있었다.**
   튀김기·포장대에 놓인 음식이 그대로 후보에 들어갔다

협동 게임에서 두 사람이 같은 치킨을 노리는 일은 계속 일어난다. 그대로 두면
조리 파이프라인을 지킬 이유가 없어진다.

### STEP C — 구현

- `FoodItem.IsHeld` 추가. `SetHeld()` 가 이미 불리고 있어 호출부 변경은 없다
- `WorldRegistry.CanBePickedUp()` — 튀기는 중 / 누가 들고 있음 / 스테이션에 올려둠을 모두 제외

### STEP E~F — 테스트가 잡은 것

처음 쓴 검사 `꺼내면 튀김기가 빈다` 가 실패했다. 확인해 보니 **코드가 아니라 검사가 틀렸다** —
튀기는 중에 꺼내지 못하는 것이 올바른 동작이다. 검사를 둘로 나눴다.

- `튀기는 중에는 꺼내지 못한다`
- 6 초 기다린 뒤 `익은 뒤 꺼내면 튀김기가 빈다`

실패를 코드 탓으로 돌리지 않고 확인한 결과, 오히려 검증 항목이 하나 늘었다.

### 수정 파일

- `Assets/Scripts/FoodItem.cs`
- `Assets/Scripts/WorldRegistry.cs`
- `Assets/Scripts/SelfTest.cs`

### 테스트 결과

- 컴파일 오류 0
- 셀프테스트 **44/44 통과** (기존 38 + 동시 접근 6)
- 빌드 성공

### 남은 문제

- 동시 접근을 **한 프로세스 안에서만** 확인했다. 실제 두 클라이언트가 같은 프레임에
  요청을 보내는 상황은 **UNKNOWN** — PHASE 10 대상
- 지시서의 `Dropped` 는 현재 "바닥에 닿으면 못 쓴다" 로만 구현돼 있고,
  떨어뜨린 자리에 기름이 생기는 연쇄는 없다 — PHASE 7 대상

### 완료 조건 점검

| 조건 | 상태 |
| --- | --- |
| 컴파일 오류 없음 | 통과 |
| 기존 핵심 기능 정상 | 통과 (38 항목 그대로 통과) |
| 새 기능 정상 | 통과 (동시 접근 6 항목) |
| 네트워크 구조 정상 | 서버 단일 처리 확인. 실측은 **UNKNOWN** |
| null/reference 문제 없음 | 실행 로그 예외 0 |
| 중복 시스템 없음 | 통과 (상태 열거형을 늘리지 않음) |
| 테스트 완료 | 통과 |
| 문서 기록 완료 | 통과 |

**PHASE 3 완료.** PHASE 4 로 이동한다.

---

## PHASE 4 — 주문 및 손님

### STEP A — 조사 결과

이미 잘 되어 있는 것이 많았다. 손대지 않았다.

| 지시서 요구 | 실제 | 판단 |
| --- | --- | --- |
| 주문 데이터 (ID/메뉴/수량/시간/상태) | `RestaurantOrder` — number, recipe, quantity, delivered, remainingTime, mood | 갖춰짐 |
| 세트 주문 DAY3 = 2, DAY5 = 3 | `Difficulty.RollQuantity()` 가 정확히 그 규칙 | 일치 |
| 손님 머리 위 표시 | `Customer` TextMesh + `CustomerSync` 로 클라이언트에도 전달 | 갖춰짐 |
| 주문 만료 실패 처리 | 실패 집계, 콤보 초기화, 성격별 평판 차감, 남은 손님 조급해짐 | 갖춰짐 |
| 배달 주문 | `DeliverySystem` | 갖춰짐 |

**주문 매칭이 이미 옳게 되어 있었다.** `FindOrderFor()` 는 메뉴가 맞는 주문 중
**남은 시간이 가장 적은 손님**을 고른다. 먼저 온 순서가 아니라 급한 순서다.
같은 메뉴를 기다리는 손님이 둘일 때 곧 화낼 사람부터 받는 것이 맞다. 변경 없음.

### 손님 유형 대조

| 지시서 | 실제 | 판단 |
| --- | --- | --- |
| Normal | `Normal` | 있음 |
| Hungry | `Hurry` (급함) — 인내심 0.6배, 값 1.45배 | 이름만 다름 |
| Regular | `Regular` (단골) — 값 2배, 놓치면 벌점 2배 | 있음 |
| Picky | 없음 | 아래 참조 |
| Group | 없음 (유형으로는) | 아래 참조 |
| — | `Patient` (느긋) — 지시서에 없으나 있음 | 유지 |

**Picky 를 만들지 않았다.** 붙일 만한 기제가 없다. 잘못된 메뉴를 건네는 경로 자체가
막혀 있어(`CompleteOrder` 가 메뉴 맞는 주문이 없으면 거절한다) "까다로움" 이 걸릴 지점이 없다.
인내심만 짧게 하면 급함과 같아진다. 기제 없는 유형은 이름만 늘리는 일이다.

**Group 은 이미 이벤트로 있다.** `SpawnOrderBurst()` 를 `RandomEventSystem` 의
단체 주문이 쓴다. 손님이 한꺼번에 몰리는 경험은 그대로 나온다.
유형으로 옮기면 같은 일을 두 곳에서 하게 된다.

### 실패 상태 대조

| 지시서 | 실제 |
| --- | --- |
| Expired | 인내심 소진 → 실패 집계 + 평판 차감 |
| Cancelled | Expired 와 같은 경로 (손님이 화내며 떠남) |
| Wasted | 쓰레기통 폐기 → `wastedFood` 집계 |
| WrongOrder | **일어날 수 없다.** 맞는 주문이 없으면 건네지지 않는다 |

WrongOrder 에 벌점을 넣지 않았다. 지금도 틀린 메뉴를 만들면 시간과 재료를 버리고
쓰레기통까지 다녀와야 하므로 대가는 이미 치른다. 건네려는 시도까지 처벌하면
조준으로 상호작용하는 흐름에서 억울하게 느껴진다.

### STEP C — 구현

**1. 식탁이 손님 줄 가장자리에 붙어 있었다.**

손님은 장애물을 피해 걷지 않고 문에서 줄까지 직선으로 온다. 줄은 z −4.5, x ±5.5 인데
PHASE 8 성 장식으로 넣은 식탁 벤치가 x 5.7 에 있어 20cm 차이로 스칠 참이었다.
식탁을 옆벽 쪽 (±7.3, −1.6) 으로 옮기고, 벤치는 안쪽 하나만 두었다 —
양쪽에 두면 바깥 벤치가 옆벽을 뚫는다.

**2. 주문 만료가 검증된 적이 없었다.**

손님을 놓치는 것은 이 게임의 주된 실패인데 셀프테스트에 없었다.
`ExpireOldestOrderForTest()` 를 넣어(실제로 기다리면 수십 초가 걸린다) 검증했다.

### 수정 파일

- `Assets/Scripts/ChickenGameBootstrap.cs`
- `Assets/Scripts/RestaurantGame.Orders.cs`
- `Assets/Scripts/SelfTest.cs`

### 테스트 결과

- 컴파일 오류 0
- 셀프테스트 **46/46 통과** (기존 44 + 만료 2)
  - `인내심이 다한 주문은 실패로 센다 (0 → 1)`
  - `손님을 놓치면 평판이 깎인다 (100 → 94)`
- 캡처로 줄 구역에 가구가 없음을 확인

### 남은 문제

- 손님은 여전히 **직선으로 걷는다.** 가구를 피해 가지 않는다.
  지금은 가구를 길 밖에 두어 눈에 띄지 않게 했을 뿐이다.
  제대로 고치려면 NavMesh 가 필요한데, 가게가 런타임에 코드로 지어지므로
  런타임 베이크용 패키지(`com.unity.ai.navigation`)를 새로 들여야 한다 — **보류**
- 손님끼리 서로 통과하는지 **UNKNOWN**

### 완료 조건 점검

| 조건 | 상태 |
| --- | --- |
| 컴파일 오류 없음 | 통과 |
| 기존 핵심 기능 정상 | 통과 (44 항목 그대로 통과) |
| 새 기능 정상 | 통과 (만료 2 항목) |
| 네트워크 구조 정상 | 주문은 서버 생성·서버 완료. 변경 없음 |
| null/reference 문제 없음 | 실행 로그 예외 0 |
| 중복 시스템 없음 | 통과 (Group 을 유형으로 중복 구현하지 않음) |
| 테스트 완료 | 통과 |
| 문서 기록 완료 | 통과 |

**PHASE 4 완료.** PHASE 5 로 이동한다.

---

## PHASE 5 — 포장 및 배달

### STEP A — 조사 결과

포장과 배달은 대부분 이미 구현돼 있었다.

| 지시서 요구 | 실제 | 판단 |
| --- | --- | --- |
| Ready → Packed | `PackChicken()` → `FoodState.Packaged` | 갖춰짐 |
| 매장 / 배달 분기 | 계산대와 배달대가 따로 있고 각각 다른 주문 목록을 본다 | 갖춰짐 |
| 기존 Electric Scooter 사용 | `Assets/Resources/Props/Scooter.prefab` (MARCIN'S) | 그대로 사용. 새 에셋 추가 없음 |
| 주소마다 거리 차이 | 주소 5 종, 거리 1~4 무작위 | 갖춰짐 |
| 거리에 따른 시간·보상 | `RideTime = 6 + 거리 × 2.5`, `DeliveryFee = 3,000 + 거리 × 800` | 갖춰짐 |
| 스쿠터 왕복 연출 | `MoveScooter()` 가 문 밖으로 보냈다가 부른다 | 갖춰짐 |
| 배달 수락 시간 제한 | 60 초, 놓치면 평판 −1 | 갖춰짐 |
| **배달 중 사고** | **없음** | 아래 |

배달 매칭도 `MatchPending()` 이 남은 시간이 가장 적은 주문부터 고른다. 매장 주문과 같은 원칙이다.

### 발견된 문제 — 먼 배달이 위험 없는 이득이었다

거리가 멀수록 배달비가 오르고 시간만 더 걸릴 뿐, 실패할 이유가 없었다.
그래서 **먼 주문을 고르는 것이 언제나 옳은 선택**이었고, 고를 이유가 생기지 않았다.
지시서도 "배달 중 사고도 가능하게 만든다" 를 요구한다.

### STEP C — 구현

- `DeliveryOrder.CrashChance` — 거리에 비례(4% + 거리 × 3%, 최대 25%).
  **스쿠터 튜닝 업그레이드가 시간뿐 아니라 위험도 함께 줄인다.**
  플레이어가 손쓸 수 있는 지렛대가 있어야 사고가 그냥 운이 되지 않는다
- 출발할 때 한 번 굴린다. 사고면 왕복 절반 지점에서 돌아온다
- `RestaurantGame.ReportDeliveryCrashed()` — 콤보 끊김, 평판 −3, 실패음.
  놓친 주문(−1)보다 아프지만 폐업 직행은 아니어야 수습이 가능하다
- `DeliverySystem.ForceNextRideCrash()` — 확률에 기대면 검증이 어떤 날은 통과하고
  어떤 날은 실패한다

### STEP E~F — 테스트가 잡은 것

`사고 난 배달은 돈을 못 받는다` 가 처음에 실패했다. 원인은 코드가 아니라 **검사 격리 실패**였다 —
앞선 배달 테스트의 스쿠터가 사고를 기다리는 12 초 사이에 도착해 매출을 올렸다.
사고의 결과를 재려면 다른 배달이 하늘에 떠 있으면 안 된다.
앞선 배달이 모두 돌아올 때까지 기다리는 단계를 넣었고, 그 자체도 검사 항목이 되었다.
평판 변화도 −1 (섞인 값) 에서 −3 (사고 값) 으로 정확해졌다.

### 수정 파일

- `Assets/Scripts/DeliverySystem.cs`
- `Assets/Scripts/RestaurantGame.Orders.cs`
- `Assets/Scripts/SelfTest.cs`

### 테스트 결과

- 컴파일 오류 0
- 셀프테스트 **50/50 통과** (기존 46 + 배달 사고 4)
- 빌드 성공

### 남은 문제

- 사고 확률 수치(4% + 거리 × 3%)가 적당한지는 **UNKNOWN** — PHASE 11 밸런스에서 측정한다
- 사고 연출이 메시지와 소리뿐이다. 스쿠터가 넘어지는 등의 시각 연출은 없다 — PHASE 9 대상

### 완료 조건 점검

| 조건 | 상태 |
| --- | --- |
| 컴파일 오류 없음 | 통과 |
| 기존 핵심 기능 정상 | 통과 (46 항목 그대로 통과) |
| 새 기능 정상 | 통과 (배달 사고 4 항목) |
| 네트워크 구조 정상 | 배달은 `IsHostSide` 에서만 돈다. 변경 없음 |
| null/reference 문제 없음 | 실행 로그 예외 0 |
| 중복 시스템 없음 | 통과 (기존 스쿠터 에셋 그대로) |
| 테스트 완료 | 통과 |
| 문서 기록 완료 | 통과 |

**PHASE 5 완료.** PHASE 6 으로 이동한다.
