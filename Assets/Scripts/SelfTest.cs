using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>-selftest 로 실행하면 사람 손 없이 주방 파이프라인을 한 바퀴 돌려보고
/// 결과를 로그로 남긴 뒤 종료한다. 빌드가 실제로 돌아가는지 자동으로 확인하는 용도다.</summary>
public sealed class SelfTest : MonoBehaviour
{
    private readonly List<string> failures = new List<string>();
    private int checks;

    public static bool Requested =>
        System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-selftest") >= 0;

    private IEnumerator Start()
    {
        // 월드가 만들어지고 게임이 시작될 때까지 기다린다.
        yield return new WaitForSecondsRealtime(2f);

        RestaurantGame game = RestaurantGame.Instance;
        Check(game != null, "RestaurantGame 이 존재한다");
        Check(WorldRegistry.Stations.Count >= 9, $"스테이션 등록 {WorldRegistry.Stations.Count}개");
        Check(FindStation(StationType.Fryer) != null, "튀김기가 있다");
        Check(FindStation(StationType.Checkout) != null, "계산대가 있다");
        Check(WorldRegistry.Players.Count >= 1, $"로컬 플레이어 {WorldRegistry.Players.Count}명");
        Check(FirstPersonView.Subject != null, "1인칭 시점이 내 캐릭터를 잡았다");

        if (game == null)
        {
            Report();
            yield break;
        }

        // 검증 중에는 손님이 저절로 들어오면 결과가 흔들린다.
        game.AutoOrdersEnabled = false;

        yield return TestCookingPipeline(game);
        yield return TestSauceAndPacking(game);
        yield return TestOrderCheckout(game);
        yield return TestDelivery(game);
        TestUpgradeRules(game);
        yield return TestSharedAccess(game);
        yield return TestOrderExpiry(game);
        TestCleaning(game);
        TestClosureAndReopen(game);
        TestVictory(game);

        game.AutoOrdersEnabled = true;
        Report();
    }

    /// <summary>냉장고 → 튀김기 → 익음 → 탐 순서가 실제로 진행되는지 본다.</summary>
    private IEnumerator TestCookingPipeline(RestaurantGame game)
    {
        Station fryer = FindStation(StationType.Fryer);
        PlayerInteraction actor = WorldRegistry.Players.Count > 0 ? WorldRegistry.Players[0] : null;
        if (fryer == null || actor == null)
        {
            return null;
        }

        return RunCooking(game, fryer, actor);
    }

    private IEnumerator RunCooking(RestaurantGame game, Station fryer, PlayerInteraction actor)
    {
        game.InteractWithStation(actor, FindStation(StationType.Fridge));
        Check(actor.HeldFood != null, "냉장고에서 생닭을 받았다");
        Check(actor.HeldFood != null && actor.HeldFood.state == FoodState.Raw, "생닭 상태다");

        game.InteractWithStation(actor, fryer);
        Check(fryer.StoredFood != null, "튀김기에 들어갔다");
        Check(fryer.StoredFood != null && fryer.StoredFood.state == FoodState.Frying, "튀기는 중이다");

        yield return new WaitForSeconds(6f);
        Check(fryer.StoredFood != null && fryer.StoredFood.state == FoodState.Cooked,
            $"6초 뒤 익었다 (현재 {(fryer.StoredFood != null ? fryer.StoredFood.state.ToString() : "없음")})");

        yield return new WaitForSeconds(4f);
        Check(fryer.StoredFood != null && fryer.StoredFood.state == FoodState.Burnt,
            $"10초 뒤 탔다 (현재 {(fryer.StoredFood != null ? fryer.StoredFood.state.ToString() : "없음")})");

        // 탄 치킨은 쓰레기통으로 치운다.
        game.InteractWithStation(actor, fryer);
        game.InteractWithStation(actor, FindStation(StationType.Trash));
        Check(actor.HeldFood == null, "탄 치킨을 버렸다");
    }

    /// <summary>양념을 발라야 포장이 되는 규칙을 확인한다.</summary>
    private IEnumerator TestSauceAndPacking(RestaurantGame game)
    {
        Station fryer = FindStation(StationType.Fryer);
        Station sauce = FindStation(StationType.Sauce);
        PlayerInteraction actor = WorldRegistry.Players.Count > 0 ? WorldRegistry.Players[0] : null;
        if (fryer == null || sauce == null || actor == null)
        {
            yield break;
        }

        game.InteractWithStation(actor, FindStation(StationType.Fridge));
        game.InteractWithStation(actor, fryer);
        yield return new WaitForSeconds(6f);
        game.InteractWithStation(actor, fryer);
        Check(actor.HeldFood != null && actor.HeldFood.state == FoodState.Cooked, "익은 치킨을 들었다");

        game.InteractWithStation(actor, sauce);
        Check(actor.HeldFood != null && actor.HeldFood.sauced, "양념을 발랐다");

        game.InteractWithStation(actor, FindStation(StationType.Packing));
        Check(actor.HeldFood != null && actor.HeldFood.state == FoodState.Packaged, "포장했다");
    }

    /// <summary>주문을 하나 만들어 그 메뉴를 그대로 만들어 팔고, 매출이 오르는지 본다.</summary>
    private IEnumerator TestOrderCheckout(RestaurantGame game)
    {
        PlayerInteraction actor = WorldRegistry.Players.Count > 0 ? WorldRegistry.Players[0] : null;
        if (actor == null)
        {
            yield break;
        }

        ClearHands(game, actor);
        game.SpawnOrderBurst(1);
        Check(game.ActiveOrders.Count > 0, "주문이 들어왔다");
        if (game.ActiveOrders.Count == 0)
        {
            yield break;
        }

        RestaurantOrder order = game.ActiveOrders[0];
        MenuRecipe wanted = order.recipe;
        int quantity = order.quantity;
        int revenueBefore = game.Revenue;
        int streakBefore = game.Streak;

        // 여러 마리를 시킨 주문은 수량만큼 채워야 손님이 간다.
        for (int served = 1; served <= quantity; served++)
        {
            yield return MakeMenu(game, actor, wanted);
            Check(actor.HeldFood != null && actor.HeldFood.state == FoodState.Packaged,
                $"{wanted.displayName} 포장 완성 ({served}/{quantity})");

            game.InteractWithStation(actor, FindStation(StationType.Checkout));
            Check(actor.HeldFood == null, $"계산대에 넘겼다 ({served}/{quantity})");

            bool shouldStillWait = served < quantity;
            Check(StillWaiting(game, order) == shouldStillWait,
                shouldStillWait ? "남은 수량이 있어 손님이 기다린다" : "수량을 다 채우면 손님이 떠난다");
        }

        Check(game.Revenue > revenueBefore, $"매출이 올랐다 ({revenueBefore} → {game.Revenue})");
        Check(game.Streak == streakBefore + quantity, $"콤보가 수량만큼 올랐다 ({game.Streak})");
    }

    /// <summary>배달 주문을 만들고 배달대에 넘겨 스쿠터가 출발하는지 본다.</summary>
    private IEnumerator TestDelivery(RestaurantGame game)
    {
        PlayerInteraction actor = WorldRegistry.Players.Count > 0 ? WorldRegistry.Players[0] : null;
        DeliverySystem deliverySystem = game.Delivery;
        if (actor == null || deliverySystem == null)
        {
            yield break;
        }

        ClearHands(game, actor);
        deliverySystem.ForceRequest();
        Check(deliverySystem.PendingCount > 0, "배달 주문이 들어왔다");
        if (deliverySystem.PendingCount == 0)
        {
            yield break;
        }

        MenuRecipe wanted = deliverySystem.Pending[0].recipe;
        int ridingBefore = deliverySystem.RidingCount;

        yield return MakeMenu(game, actor, wanted);
        game.InteractWithStation(actor, FindStation(StationType.Delivery));
        Check(actor.HeldFood == null, "배달대에 넘겼다");
        Check(deliverySystem.RidingCount == ridingBefore + 1, "스쿠터가 출발했다");

        yield return TestDeliveryCrash(game, actor, deliverySystem);
    }

    /// <summary>배달 중 사고. 먼 주문이 배달비만 비싼 공짜 이득이 되지 않으려면
    /// 실제로 돈을 못 받고 평판이 깎여야 한다.</summary>
    private IEnumerator TestDeliveryCrash(RestaurantGame game, PlayerInteraction actor, DeliverySystem deliverySystem)
    {
        // 앞선 배달이 돌아오는 중이면 그 매출이 섞여 사고의 결과를 가릴 수 없다.
        float wait = 0f;
        while (deliverySystem.RidingCount > 0 && wait < 40f)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        Check(deliverySystem.RidingCount == 0, "앞선 배달이 모두 돌아왔다");

        ClearHands(game, actor);
        deliverySystem.ForceRequest();
        if (deliverySystem.PendingCount == 0)
        {
            Check(false, "사고를 시험할 배달 주문이 있다");
            yield break;
        }

        MenuRecipe wanted = deliverySystem.Pending[0].recipe;
        yield return MakeMenu(game, actor, wanted);

        int revenueBefore = game.Revenue;
        int reputationBefore = game.Reputation;
        int crashedBefore = deliverySystem.CrashedDeliveries;

        deliverySystem.ForceNextRideCrash();
        game.InteractWithStation(actor, FindStation(StationType.Delivery));

        // 사고는 왕복의 절반 지점에서 드러난다.
        yield return new WaitForSeconds(12f);

        Check(deliverySystem.CrashedDeliveries == crashedBefore + 1, "배달이 사고로 끝났다");
        Check(game.Revenue == revenueBefore, "사고 난 배달은 돈을 못 받는다");
        Check(game.Reputation < reputationBefore,
            $"배달 사고로 평판이 깎인다 ({reputationBefore} → {game.Reputation})");
    }

    /// <summary>돈이 없으면 업그레이드가 팔리지 않아야 한다.</summary>
    private void TestUpgradeRules(RestaurantGame game)
    {
        Station desk = FindStation(StationType.Upgrade);
        Check(desk != null, "업그레이드 데스크가 있다");

        int revenue = game.Revenue;
        game.BuyUpgrade(0);
        bool affordable = revenue >= 120_000;
        Check(affordable || game.Revenue == revenue, "자금이 모자라면 결제되지 않는다");
    }

    /// <summary>손님을 놓치는 것은 이 게임의 주된 실패다. 놓쳤을 때 평판이 깎이고
    /// 콤보가 끊기지 않으면 서두를 이유가 사라진다.</summary>
    private IEnumerator TestOrderExpiry(RestaurantGame game)
    {
        game.SpawnOrderBurst(1);
        yield return null;

        int failedBefore = game.FailedOrders;
        int reputationBefore = game.Reputation;

        if (!game.ExpireOldestOrderForTest())
        {
            Check(false, "만료를 시험할 주문이 있다");
            yield break;
        }

        // 만료는 다음 갱신에서 처리된다.
        yield return null;
        yield return null;

        Check(game.FailedOrders > failedBefore,
            $"인내심이 다한 주문은 실패로 센다 ({failedBefore} → {game.FailedOrders})");
        Check(game.Reputation < reputationBefore,
            $"손님을 놓치면 평판이 깎인다 ({reputationBefore} → {game.Reputation})");
    }

    /// <summary>여럿이 같은 것을 동시에 건드려도 중복 처리되면 안 된다.
    ///
    /// 협동 게임이라 두 사람이 같은 튀김기와 같은 치킨을 노리는 일이 계속 일어난다.
    /// 남의 손에 든 치킨이 주워지거나 튀김기의 치킨이 절차 없이 빠져나가면
    /// 조리 파이프라인 자체가 무의미해진다.</summary>
    private IEnumerator TestSharedAccess(RestaurantGame game)
    {
        PlayerInteraction actor = WorldRegistry.Players.Count > 0 ? WorldRegistry.Players[0] : null;
        Station fryer = FindStation(StationType.Fryer);
        if (actor == null || fryer == null)
        {
            Check(false, "동시 접근을 시험할 플레이어와 튀김기가 있다");
            yield break;
        }

        actor.DropEverything();

        // 손에 든 치킨은 그 자리에서 다시 주워지면 안 된다.
        game.InteractWithStation(actor, FindStation(StationType.Fridge));
        FoodItem held = actor.HeldFood;
        Check(held != null, "생닭을 들었다");
        Check(WorldRegistry.NearestLooseFood(actor.transform.position, 3f) == null,
            "손에 든 치킨은 주울 대상이 아니다");

        // 튀김기에 올린 치킨도 스테이션 절차를 거쳐야 한다.
        game.InteractWithStation(actor, fryer);
        Check(fryer.StoredFood != null, "튀김기에 들어갔다");
        Check(WorldRegistry.NearestLooseFood(fryer.transform.position, 3f) == null,
            "튀김기의 치킨은 주울 대상이 아니다");

        // 튀기는 중에는 아무도 꺼낼 수 없어야 한다.
        game.InteractWithStation(actor, fryer);
        Check(fryer.StoredFood != null && actor.HandsFree, "튀기는 중에는 꺼내지 못한다");

        yield return new WaitForSeconds(6f);

        // 한 번 꺼내면 자리가 비어야 두 번째 사람이 헛손질을 한다.
        game.InteractWithStation(actor, fryer);
        Check(fryer.StoredFood == null && actor.HeldFood != null, "익은 뒤 꺼내면 튀김기가 빈다");

        game.InteractWithStation(actor, FindStation(StationType.Trash));
        actor.DropEverything();
    }

    /// <summary>사고는 사람 손으로 수습할 수 있어야 한다. 기름을 저절로 마르기만
    /// 기다려야 한다면 미끄러짐은 그냥 당하는 일이 된다.</summary>
    private void TestCleaning(RestaurantGame game)
    {
        PlayerInteraction actor = WorldRegistry.Players.Count > 0 ? WorldRegistry.Players[0] : null;
        if (actor == null)
        {
            Check(false, "청소를 시험할 플레이어가 있다");
            return;
        }

        actor.DropEverything();
        game.SpillOilAt(actor.transform.position);
        Check(OilPuddle.Covering(actor.transform.position) != null, "발밑에 기름이 생겼다");
        Check(actor.CanClean, "손이 비어 있으면 닦을 수 있다고 알려준다");

        Check(game.TryCleanNearby(actor.transform.position), "기름을 닦았다");
        Check(OilPuddle.Covering(actor.transform.position) == null, "닦은 자리에 기름이 없다");
    }

    /// <summary>평판이 0 이 되면 폐업하고, 재기하면 다시 영업할 수 있어야 한다.</summary>
    private void TestClosureAndReopen(RestaurantGame game)
    {
        int revenueBefore = game.Revenue;

        game.ChangeReputation(-200);
        Check(GameFlow.Instance != null && GameFlow.Instance.State == GameState.Defeat,
            $"평판 0 이면 폐업한다 (현재 {(GameFlow.Instance != null ? GameFlow.Instance.State.ToString() : "없음")})");

        game.Reopen();
        Check(game.Reputation == 50, $"재기하면 평판이 50 이 된다 (현재 {game.Reputation})");
        Check(game.Revenue <= revenueBefore, $"재기 벌금이 매출에서 빠진다 ({revenueBefore} → {game.Revenue})");
        Check(GameFlow.Instance != null && GameFlow.Instance.IsPlaying, "재기하면 다시 영업 상태가 된다");
    }

    /// <summary>목표 매출을 넘기면 승리하고, 걸린 시간이 기록으로 남아야 한다.
    /// 이 게임의 목표는 버티기가 아니라 ₩10,000,000 을 빨리 찍는 것이므로
    /// 승리 화면에 시간이 나오지 않으면 게임이 성립하지 않는다.
    ///
    /// 재기 테스트가 finished 를 되돌려 놓으므로 그 뒤에 실행해야 한다.</summary>
    private void TestVictory(RestaurantGame game)
    {
        Check(game.PlaySeconds > 0f, $"영업 시간이 흐른다 ({RestaurantGame.Clock(game.PlaySeconds)})");

        game.AddRevenueForTest(RestaurantGame.TargetRevenue);
        Check(GameFlow.Instance != null && GameFlow.Instance.State == GameState.Victory,
            $"목표 매출을 넘기면 승리한다 (현재 {(GameFlow.Instance != null ? GameFlow.Instance.State.ToString() : "없음")})");

        SaveData record = SaveSystem.Load();
        Check(record != null && record.bestClearSeconds > 0,
            $"달성 시간이 기록으로 남는다 ({(record != null ? RestaurantGame.Clock(record.bestClearSeconds) : "없음")})");
    }

    /// <summary>원하는 메뉴 하나를 처음부터 포장까지 만든다.</summary>
    private IEnumerator MakeMenu(RestaurantGame game, PlayerInteraction actor, MenuRecipe wanted)
    {
        Station fryer = FindStation(StationType.Fryer);
        game.InteractWithStation(actor, FindStation(StationType.Fridge));
        game.InteractWithStation(actor, fryer);
        yield return new WaitForSeconds(6f);
        game.InteractWithStation(actor, fryer);

        if (wanted.needsSauce)
        {
            Station sauce = FindStation(StationType.Sauce);
            if (sauce != null)
            {
                actor.SetSauceChoice(wanted.kind);
                game.InteractWithStation(actor, sauce);
            }
        }

        game.InteractWithStation(actor, FindStation(StationType.Packing));
    }

    private static void ClearHands(RestaurantGame game, PlayerInteraction actor)
    {
        if (actor.HeldFood != null)
        {
            game.InteractWithStation(actor, FindStation(StationType.Trash));
        }
    }

    private static bool StillWaiting(RestaurantGame game, RestaurantOrder order)
    {
        foreach (RestaurantOrder active in game.ActiveOrders)
        {
            if (active == order)
            {
                return true;
            }
        }

        return false;
    }

    private static Station FindStation(StationType type)
    {
        foreach (Station station in WorldRegistry.Stations)
        {
            if (station != null && station.stationType == type && station.gameObject.activeInHierarchy)
            {
                return station;
            }
        }

        return null;
    }

    private void Check(bool passed, string label)
    {
        checks++;
        if (passed)
        {
            Debug.Log($"[SelfTest] PASS  {label}");
        }
        else
        {
            failures.Add(label);
            Debug.LogError($"[SelfTest] FAIL  {label}");
        }
    }

    private void Report()
    {
        if (failures.Count == 0)
        {
            Debug.Log($"[SelfTest] RESULT OK  {checks}개 항목 통과");
        }
        else
        {
            Debug.LogError($"[SelfTest] RESULT FAILED  {failures.Count}/{checks} 실패: {string.Join(", ", failures)}");
        }

        Application.Quit(failures.Count == 0 ? 0 : 1);
    }
}
