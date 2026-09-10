# CURRENT_STATE — PHASE 0 프로젝트 분석

작성 2026-09-11. 커밋 `0c314c3` 기준.

이 문서는 조사 결과만 적는다. 확인하지 못한 것은 `UNKNOWN` 으로 남긴다.

---

## 1. 환경

| 항목 | 값 | 확인 방법 |
| --- | --- | --- |
| Unity | 6000.5.7f1 | ProjectSettings, 에디터 실행 |
| 렌더 파이프라인 | URP 17.5.0 | Packages/manifest.json, GraphicsSettings 가 PC_RPAsset 참조 |
| 네트워크 | Netcode for GameObjects 2.13.2 | Packages/manifest.json |
| 입력 | Input System 1.20.0 | Packages/manifest.json |
| 플랫폼 | Windows 64-bit | 빌드 성공 확인 |
| 시점 | 1인칭 전용 | FirstPersonView.cs, 탑다운 카메라는 제거됨 |
| 컴파일 상태 | 오류 0 | Roslyn 직접 컴파일 + 배치 빌드 |
| 스크립트 | 48 개 파일 / 7,282 줄 | `Assets/Scripts` |

---

## 2. 씬과 프리팹

**게임 씬은 하나뿐이다.** `Assets/Scenes/ChickenGame.unity`

이 씬에는 오브젝트가 사실상 없다. `Chicken Game` 게임오브젝트 하나에
`ChickenGameBootstrap` 이 붙어 있고, **가게 전체가 실행 시점에 코드로 지어진다.**
바닥, 벽, 스테이션, 플레이어, 카메라, HUD 가 전부 `ChickenGameBootstrap.Start()` 에서 생성된다.

이건 이 프로젝트의 가장 중요한 구조적 사실이다. 맵을 바꾸려면 씬이 아니라 코드를 고쳐야 한다.

나머지 씬 4 개는 에셋 팩에 딸려온 데모 씬이며 게임과 무관하다.

### 네트워크 프리팹 (`Assets/Resources/`)

NGO 는 스폰할 프리팹이 에셋으로 존재해야 하므로, 에디터 메뉴
`Chicken Game > Build Network Prefab` 이 이것들을 구워 둔다.

| 프리팹 | 용도 |
| --- | --- |
| NetworkPlayer | 접속한 플레이어 아바타 |
| NetworkCustomer | 손님 |
| NetworkFood | 치킨 |
| NetworkOil | 기름 웅덩이 |
| NetworkFire | 튀김기 화재 |
| KitchenNetwork | 가게 상태 동기화 + RPC 수신 |

`Assets/DefaultNetworkPrefabs.asset` 에 7 개 항목이 등록되어 있다.
6 개 프리팹 + 1 개는 **UNKNOWN** (GUID 대조를 하지 않았다).

### 소품 프리팹 (`Assets/Resources/Props/`)

23 개. 런타임에 `Resources.Load` 로 불러 쓴다. 원본 팩의 프리팹을 복사한 것이며
메시와 머티리얼은 원본을 GUID 로 참조한다.

---

## 3. 에셋 — 실제 사용 중 vs 존재만 함

**모두 URP 로 제작된 팩이다.** 빌트인 파이프라인에서는 셰이더가 깨져
머티리얼이 텍스처와 색을 전부 잃는다. 프로젝트가 URP 로 간 이유가 이것이다.

| 팩 | 상태 | 어디에 쓰이나 |
| --- | --- | --- |
| FREE/Pack_FREE_PartyCharacters | **사용 중** | 플레이어와 손님 몸체 (CharacterVisual). 모자 3 종 |
| Fried Chicken LITE | **사용 중** | 치킨 아이템 (FoodItem). 포장 시 상자 프롭 켜짐 |
| MARCIN'S Assets/Electric Scooter | **사용 중** | 배달 스쿠터 |
| Mnostva Art/FREE_Interiors_2 | **사용 중** | 양념대·업그레이드 선반·계산대 금전등록기, 카운터 소품, 화분, 메뉴판 |
| Pack_FREE_Cars | **사용 중** | 문 밖 Van / Taxi / Hatchback |
| RetroDiner | **사용 중** | 냉장고(Freezer), 포장대(Dinner_Table), 배달 상자(CupBox), 벤치, 커피머신 |

> 지시서에 RetroDiner 가 빠져 있으나 프로젝트에 존재하고 실제로 쓰이고 있다.

미사용: 각 팩의 데모 씬, 미사용 변형 프리팹(치킨 16 종 중 1 종만 사용, 스쿠터 10 종 중 1 종만 사용, 차량 8 종 중 3 종 사용).
중복 에셋은 없다.

---

## 4. 네트워크 구조

**서버 권한 구조가 이미 갖춰져 있다.** 이걸 갈아엎을 이유가 없다.

- 클라이언트는 `KitchenNetwork.Request*()` 를 호출한다.
- 이는 `[Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]` 로 서버에 전달된다.
- 서버가 `RestaurantGame` 에서 실제 처리를 한다.
- 결과는 `NetworkVariable` 로 모두에게 퍼진다.

동기화되는 상태:

| NetworkVariable | 내용 |
| --- | --- |
| netRevenue | 누적 매출 |
| netDay | 현재 DAY |
| netReputation | 평판 |
| netOrders | 주문 목록 (문자열로 직렬화) |

RPC: `InteractRpc`, `PickupRpc`, `DropRpc`, `SauceRpc`, `UpgradeRpc` (클라 → 서버),
`SettlementRpc`, `FinishRpc` (서버 → 클라).

손님과 치킨은 `NetworkObject` 로 스폰되고 `NetworkTransform` 이 위치를 동기화한다.
음식을 손에 드는 것은 `NetworkObject.TrySetParent` 로 처리하며 **서버만** 부모를 바꿀 수 있다.

`WorldRegistry` 가 스테이션에 안정적인 인덱스를 부여해 RPC 가 스테이션을 가리킬 때 쓴다.

### 확인되지 않은 부분 (PHASE 10 대상)

- 두 명이 같은 치킨/튀김기/주문을 동시에 건드리는 경우 — **UNKNOWN**
- 클라이언트 재접속 — **UNKNOWN**
- 호스트 종료 시 클라이언트 동작 — **UNKNOWN**
- 지연/패킷 손실 — **UNKNOWN**
- 실제 2 대 이상의 PC 로 붙여본 적 없음 — **UNKNOWN**

지금까지의 모든 검증은 `-mode local` 단일 프로세스에서만 이뤄졌다.

---

## 5. 구현 상태

### 구현 완료 (동작 확인됨)

| 기능 | 확인 근거 |
| --- | --- |
| 1인칭 이동·시점·점프·머리 흔들림·발소리 | 실행 캡처, 셀프테스트 |
| 조준선 기반 상호작용 + 화면 안내 | 캡처에서 "[E] 생닭 꺼내기" 등 확인 |
| 조리 파이프라인 (냉장고 → 튀김기 → 양념 → 포장 → 계산/배달) | 셀프테스트 31 항목 |
| 익음 / 탐 / 폐기 | 셀프테스트 |
| 주문 생성·완료·매출 반영 | 셀프테스트 |
| 콤보 | 셀프테스트 ("콤보가 수량만큼 올랐다") |
| 배달 (주소·거리·스쿠터 왕복) | 셀프테스트 |
| 업그레이드 5 종 + 자금 부족 거부 | 셀프테스트 |
| 평판 0 → 폐업, 재기 | 셀프테스트 |
| 손님 성격 4 종 (보통/급함/느긋/단골) | CustomerMood.cs |
| 사고 (기름·미끄러짐·화재·소화기) | HazardSystem.cs, 실행 중 기름 웅덩이 확인 |
| 랜덤 이벤트 6 종 | 실행 중 "러시아워", "진상 손님" 확인 |
| 런타임 합성 효과음 + 음악 | GameAudio.cs, GameMusic.cs |
| 캐릭터·소품 모델 적용 | 실행 캡처 |
| 자동 셀프테스트 / 밸런스 봇 | 31/31 통과 |

### 부분 구현

| 기능 | 부족한 점 |
| --- | --- |
| 손님 이동 | **NavMesh 를 쓰지 않는다.** 목표 지점으로 직선 보간한다. 장애물을 통과할 수 있다 |
| 주문 동기화 | 문자열 직렬화(FixedString4096Bytes)로 넘긴다. 주문 수가 늘면 4096 바이트 한계 — **UNKNOWN** |
| 배달 사고 | 배달 중 사고는 없다. 왕복 시간만 있다 |
| 승리 연출 | 승리 화면은 있으나 "달성 시간" 기록은 없다 |
| 사고 연쇄 | 기름 → 미끄러짐 → 화재는 있으나, 지시서가 말한 "떨어뜨림 → 기름" 연결은 **UNKNOWN** |

### 미구현

- 손님 유형 `Group` (세트 주문은 있으나 단체 손님 개념은 없음)
- 달성 시간 기록 및 최종 기록 화면
- 재접속 / 호스트 이전
- NavMesh 기반 손님 경로
- 배달 중 사고

---

## 6. 게임 데이터 흐름

```
ChickenGameBootstrap.Start()
  └ 가게 생성 → 플레이어 생성 → 카메라 → RestaurantGame + 하위 시스템 → HUD

GameFlow (Title → Playing → Settlement → Victory/Defeat)
  └ 타이틀에서 SPACE(혼자) / H(호스트) / J(접속)

RestaurantGame (호스트 권한)
  ├ RestaurantGame.Orders    주문 생성·만료·완료
  ├ RestaurantGame.Kitchen   조리 상태 전이
  ├ RestaurantGame.Progress  매출·DAY·평판·결산
  ├ DeliverySystem           배달 주소·스쿠터
  ├ UpgradeSystem            업그레이드 5 종
  ├ RandomEventSystem        랜덤 이벤트 6 종
  └ HazardSystem             기름·미끄러짐·화재

KitchenNetwork  클라 요청 수신 + 상태 방송
```

DAY 길이 600 초 = 10 분. 지시서의 "10분 = DAY 1" 과 일치한다.
목표 ₩10,000,000 도 일치한다. 메뉴 3 종 가격도 일치한다 (18,000 / 21,000 / 20,000).

---

## 7. 발견된 버그 / 기술 부채

### 이번 세션에서 이미 고친 것

1. `Arial.ttf` 내장 폰트 이름 변경으로 인한 `ArgumentException` → `UiFont` 로 일원화
2. 소품·캐릭터가 부모 배율만큼 작아지던 크기 계산 오류 → 부모 배율을 되돌린 뒤 계산
3. 회전한 장식이 절반 크기로 나오던 오류 → 앵커 배율 대신 명시적 공간 전달
4. 손님 색 변경이 매 프레임 머티리얼 배열을 새로 할당하던 문제 → 캐시

### 남아 있는 것

| 항목 | 내용 |
| --- | --- |
| 주문 직렬화 | 문자열 기반. 구조체 + `INetworkSerializable` 이 적절 |
| 손님 경로 | 직선 보간. 가구를 통과한다 |
| 냉장고 문 | 모델이 문이 열린 상태다. 연출상 어색할 수 있음 |
| 중복 처리 방지 | 서버 단일 처리이나 동시 접근 테스트 미실시 |
| 씬 비어 있음 | 전부 코드 생성이라 에디터에서 맵을 눈으로 편집할 수 없다 |

중복 시스템은 발견되지 않았다.

---

## 8. 다음 PHASE 에서 할 일

PHASE 1 은 **새 시스템을 만드는 단계가 아니다.** `GameManager` / `DaySystem` /
`RevenueSystem` / `ReputationSystem` 에 해당하는 기능이 이미
`GameFlow` + `RestaurantGame.Progress` 에 있다. 따라서 PHASE 1 은:

1. 기존 상태 기계가 지시서의 상태 목록(Opening/Running/Rush/Closing/Victory/Shutdown)을 어떻게 대응하는지 확인
2. 빠진 상태만 추가
3. 승리 시 **달성 시간** 기록 추가 (현재 없음, 지시서의 최종 목표)
4. 서버 권한 경계를 문서화

로 한정한다. 기존 구조를 갈아엎지 않는다.
