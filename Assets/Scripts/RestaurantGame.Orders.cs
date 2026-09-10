using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>매장 주문과 배달 정산. RestaurantGame 의 일부다.</summary>
public sealed partial class RestaurantGame
{
    /// <summary>단체 주문처럼 손님이 한꺼번에 몰릴 때 쓴다.</summary>
    public void SpawnOrderBurst(int count)
    {
        for (int index = 0; index < count; index++)
        {
            CreateOrder();
        }
    }

    private void HandOverDelivery(PlayerInteraction actor)
    {
        FoodItem food = actor.HeldFood;
        if (food == null || food.state != FoodState.Packaged)
        {
            ShowMessage("포장된 치킨을 들고 오세요");
            return;
        }

        if (delivery == null)
        {
            return;
        }

        DeliveryOrder order = delivery.MatchPending(food.Recipe.kind);
        if (order == null)
        {
            ShowMessage($"{food.Recipe.displayName} 배달 주문이 없습니다");
            return;
        }

        delivery.Dispatch(order);
        actor.ClearHeldFood();
        NetworkSpawner.Remove(food.gameObject);
        ShowMessage($"배달 출발! {order.address} ({Mathf.CeilToInt(order.RideTime)}s)");
    }

    public void ReportDeliveryComplete(DeliveryOrder order)
    {
        streak++;
        bestStreak = Mathf.Max(bestStreak, streak);
        int payout = Mathf.RoundToInt((order.recipe.price + order.DeliveryFee * deliveryFeeMultiplier)
                                      * Difficulty.PriceMultiplier(day) * Difficulty.ComboMultiplier(streak));
        revenue += payout;
        successfulOrders++;
        ChangeReputation(+2);
        PlaySound(GameSound.Delivery);
        ShowMessage($"배달 완료! {order.address} +₩{payout:N0}");
    }

    public void ReportDeliveryMissed(DeliveryOrder order)
    {
        failedOrders++;
        streak = 0;
        ChangeReputation(-5);
        ShowMessage($"배달 주문 취소! {order.address}");
    }

    private void CompleteOrder(PlayerInteraction actor)
    {
        FoodItem food = actor.HeldFood;
        if (food == null || food.state != FoodState.Packaged)
        {
            ShowMessage("포장된 치킨을 들고 오세요");
            return;
        }

        if (activeOrders.Count == 0)
        {
            ShowMessage("현재 주문이 없습니다");
            return;
        }

        RestaurantOrder order = FindOrderFor(food.Recipe.kind);
        if (order == null)
        {
            ShowMessage($"{food.Recipe.displayName}을 기다리는 손님이 없습니다");
            return;
        }

        int bonus = Mathf.RoundToInt(Mathf.Clamp(order.remainingTime, 0f, OrderPatience) * 100f);
        streak++;
        bestStreak = Mathf.Max(bestStreak, streak);
        float multiplier = Difficulty.PriceMultiplier(day) * Difficulty.ComboMultiplier(streak);
        int payout = Mathf.RoundToInt((order.recipe.price + bonus) * multiplier);
        revenue += payout;
        order.delivered++;
        actor.ClearHeldFood();
        NetworkSpawner.Remove(food.gameObject);
        PlaySound(GameSound.Cash);
        GameEffects.Burst(actor.transform.position + Vector3.up * 1.4f, new Color(1f, 0.85f, 0.25f));

        // 여러 마리를 시킨 손님은 다 채워야 자리를 뜬다.
        if (order.Remaining > 0)
        {
            ShowMessage($"주문 #{order.number} {order.delivered}/{order.quantity} 마리 +₩{payout:N0}");
            return;
        }

        successfulOrders++;
        ChangeReputation(+3);
        activeOrders.Remove(order);
        SendCustomerHome(order);
        ReflowQueue();
        ShowMessage($"주문 #{order.number} {order.recipe.displayName} x{order.quantity} 완료! +₩{payout:N0}");
    }

    /// <summary>같은 메뉴를 기다리는 손님 중 가장 급한 손님을 고른다.</summary>
    private RestaurantOrder FindOrderFor(MenuKind kind)
    {
        RestaurantOrder best = null;
        foreach (RestaurantOrder order in activeOrders)
        {
            if (order.recipe.kind != kind || order.Remaining <= 0)
            {
                continue;
            }

            if (best == null || order.remainingTime < best.remainingTime)
            {
                best = order;
            }
        }

        return best;
    }

    private void CreateOrder()
    {
        if (activeOrders.Count >= MaxWaitingOrders)
        {
            return;
        }

        totalOrders++;
        MenuRecipe recipe = MenuDatabase.RandomFor(day);
        Customer customer = NetworkSpawner.SpawnCustomer($"Customer {totalOrders}");
        customer.Initialise(DoorPoint, QueueSlot(activeOrders.Count), ExitPoint, recipe.packagedColor);
        int quantity = Difficulty.RollQuantity(day);
        activeOrders.Add(new RestaurantOrder(totalOrders, recipe, customer,
            Difficulty.Patience(OrderPatience, day) * patienceMultiplier * quantity, quantity));
        PlaySound(GameSound.OrderIn);
        ShowMessage(quantity > 1
            ? $"주문 #{totalOrders} {recipe.displayName} x{quantity} 들어왔습니다!"
            : $"주문 #{totalOrders} {recipe.displayName} 들어왔습니다!");
    }

    private static Vector3 QueueSlot(int index)
    {
        return new Vector3(-4.5f + index * 2.2f, 0.9f, -4f);
    }

    private static void SendCustomerHome(RestaurantOrder order)
    {
        if (order.customer != null)
        {
            order.customer.Leave();
        }
    }

    /// <summary>앞 손님이 빠지면 뒷 손님들이 한 칸씩 당겨 선다.</summary>
    private void ReflowQueue()
    {
        for (int index = 0; index < activeOrders.Count; index++)
        {
            Customer customer = activeOrders[index].customer;
            if (customer != null)
            {
                customer.SetQueueSlot(QueueSlot(index));
            }
        }
    }
}
