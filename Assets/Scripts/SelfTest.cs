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
        Check(WorldRegistry.Players.Count >= 2, $"로컬 플레이어 {WorldRegistry.Players.Count}명");

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
                sauce.sauceKind = wanted.kind;
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
