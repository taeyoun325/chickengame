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
                                      * Difficulty.PriceMultiplier(day)
                                      * Difficulty.ComboMultiplier(streak)
                                      * Difficulty.ReputationTip(reputation));
        revenue += payout;
        successfulOrders++;
        ChangeReputation(+2);
        PlaySound(GameSound.Delivery);

        // 배달은 스쿠터가 돌아오는 자리에서 정산되므로 그쪽에 띄운다.
        if (delivery != null && delivery.ScooterPosition.HasValue)
        {
            MoneyPopup.Show(delivery.ScooterPosition.Value + Vector3.up * 1.4f,
                $"+₩{payout:N0}", new Color(0.5f, 0.85f, 1f));
        }

        ShowMessage($"배달 완료! {order.address} +₩{payout:N0}");
    }

    /// <summary>배달 중 사고. 음식은 이미 손을 떠났으므로 돈은 못 받고 평판만 깎인다.
    /// 놓친 주문보다 아프지만 폐업으로 직행할 만큼은 아니어야 수습이 가능하다.</summary>
    public void ReportDeliveryCrashed(DeliveryOrder order)
    {
        streak = 0;
        ChangeReputation(-3);
        PlaySound(GameSound.Fail);
        ShowMessage($"배달 사고! {order.address} - 치킨을 쏟았습니다");
    }

    /// <summary>받지 않은 배달 요청이 사라지는 것은 기회를 놓친 것이지 사고가 아니다.
    /// 예전에는 -5 평판에 실패 집계까지 해서, 배달을 안 하면 하루 만에 폐업했다.</summary>
    public void ReportDeliveryMissed(DeliveryOrder order)
    {
        ChangeReputation(-1);
        ShowMessage($"배달 주문을 놓쳤습니다 - {order.address}");
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
        float multiplier = Difficulty.PriceMultiplier(day)
                           * Difficulty.ComboMultiplier(streak)
                           * Difficulty.ReputationTip(reputation)
                           * order.mood.priceScale;
        int payout = Mathf.RoundToInt((order.recipe.price + bonus) * multiplier);
        revenue += payout;
        order.delivered++;
        actor.ClearHeldFood();
        NetworkSpawner.Remove(food.gameObject);
        PlaySound(GameSound.Cash);
        GameEffects.Burst(actor.transform.position + Vector3.up * 1.4f, new Color(1f, 0.85f, 0.25f));
        MoneyPopup.Show(actor.transform.position + Vector3.up * 1.6f, $"+₩{payout:N0}", new Color(1f, 0.88f, 0.3f));

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
    /// <summary>셀프테스트가 주문 만료를 확인하려고 쓴다. 인내심이 다할 때까지
    /// 실제로 기다리면 검증에만 수십 초가 걸린다.</summary>
    public bool ExpireOldestOrderForTest()
    {
        if (activeOrders.Count == 0)
        {
            return false;
        }

        activeOrders[0].remainingTime = 0f;
        return true;
    }

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

    /// <summary>만든 주문의 수량을 돌려준다. 다음 주문 간격을 정하는 데 쓴다.</summary>
    private int CreateOrder()
    {
        if (activeOrders.Count >= MaxWaitingOrders)
        {
            return 1;
        }

        totalOrders++;
        MenuRecipe recipe = MenuDatabase.RandomFor(day);
        Customer customer = NetworkSpawner.SpawnCustomer($"Customer {totalOrders}");
        customer.Initialise(DoorPoint, QueueSlot(activeOrders.Count), ExitPoint, recipe.packagedColor);
        int quantity = Difficulty.RollQuantity(day);
        CustomerMood mood = CustomerMood.Roll(day);
        customer.ApplyMood(mood);
        activeOrders.Add(new RestaurantOrder(totalOrders, recipe, customer,
            Difficulty.Patience(OrderPatience, day) * patienceMultiplier * quantity * mood.patienceScale,
            quantity, mood));
        PlaySound(GameSound.OrderIn);
        ShowMessage(quantity > 1
            ? $"주문 #{totalOrders} {recipe.displayName} x{quantity} 들어왔습니다!"
            : $"주문 #{totalOrders} {recipe.displayName} 들어왔습니다!");
        return quantity;
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

    /// <summary>한 명이 화내며 나가면 줄에 선 손님들도 조급해진다.</summary>
    private void RushRemainingCustomers(RestaurantOrder leaving)
    {
        foreach (RestaurantOrder order in activeOrders)
        {
            if (order != leaving)
            {
                order.remainingTime *= 0.9f;
            }
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
