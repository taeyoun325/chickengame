using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>하루 진행, 저장, 업그레이드, 이벤트, 승패. RestaurantGame 의 일부다.</summary>
public sealed partial class RestaurantGame
{
    /// <summary>하루가 끝나면 게임을 멈추고 결산을 보여준 뒤 저장한다.</summary>
    private void EndDay()
    {
        dayTimer = 0f;

        int dayRevenue = revenue + spending - dayStartNetRevenue;
        if (dayRevenue > bestDayRevenue)
        {
            bestDayRevenue = dayRevenue;
        }

        if (day > bestDay)
        {
            bestDay = day;
        }

        int dayOrders = totalOrders - dayStartOrders;
        int daySuccess = successfulOrders - dayStartSuccess;
        int dayFailed = failedOrders - dayStartFailed;

        // 하루를 잘 마치면 평판이 회복된다. 회복 수단이 없으면 한 번 무너진 가게는 끝이었다.
        int recovered = 0;
        if (daySuccess + dayFailed > 0 && daySuccess >= (daySuccess + dayFailed) * 0.6f)
        {
            recovered = Mathf.Min(15, MaxReputation - reputation);
            if (recovered > 0)
            {
                ChangeReputation(recovered);
            }
        }
        float progress = revenue / (float)TargetRevenue * 100f;

        StringBuilder text = new StringBuilder();
        text.AppendLine($"DAY {day} 영업 종료");
        text.AppendLine();
        text.AppendLine($"오늘 매출        ₩{dayRevenue:N0}");
        text.AppendLine($"주문 {dayOrders}건   성공 {daySuccess}   실패 {dayFailed}");
        text.AppendLine($"탄 치킨 {burntChicken}   버린 음식 {wastedFood}");
        if (hazards != null)
        {
            text.AppendLine($"미끄러짐 {hazards.SlipCount}회   화재 {hazards.FireCount}회");
        }

        text.AppendLine($"최고 콤보 {bestStreak}연속   평판 {reputation}/{MaxReputation} ({Difficulty.ReputationGrade(reputation)})");
        if (recovered > 0)
        {
            text.AppendLine($"오늘 성적이 좋아 평판 +{recovered}");
        }

        text.AppendLine($"업그레이드 지출  ₩{spending:N0}");
        text.AppendLine($"내일 단가 배율   x{Difficulty.PriceMultiplier(day + 1):0.0}");
        text.AppendLine();
        text.AppendLine($"누적 매출        ₩{revenue:N0} / ₩{TargetRevenue:N0}  ({progress:0.00}%)");
        text.AppendLine();
        text.AppendLine("SPACE 를 눌러 다음 DAY 시작");

        Debug.Log($"[Day] DAY {day} 결산 - 매출 {revenue}");
        SaveProgress();

        if (GameFlow.Instance != null)
        {
            GameFlow.Instance.EnterSettlement(text.ToString());
        }

        if (KitchenNetwork.Online && KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.BroadcastSettlement(text.ToString());
        }
    }

    public void StartNextDay()
    {
        if (GameFlow.Instance != null)
        {
            GameFlow.Instance.ResumeFromSettlement();
        }

        if (KitchenNetwork.Online && KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.BroadcastResume();
        }

        day++;
        dayStartNetRevenue = revenue + spending;
        dayStartOrders = totalOrders;
        dayStartSuccess = successfulOrders;
        dayStartFailed = failedOrders;
        ShowMessage($"DAY {day} 시작!");
    }

    public void SaveProgress()
    {
        SaveData data = new SaveData
        {
            day = day,
            revenue = revenue,
            totalOrders = totalOrders,
            successfulOrders = successfulOrders,
            failedOrders = failedOrders,
            burntChicken = burntChicken,
            wastedFood = wastedFood,
            spending = spending,
            reputation = reputation,
            bestStreak = bestStreak,
            bestDayRevenue = bestDayRevenue,
            bestDay = bestDay,
            upgradeLevels = upgrades != null ? upgrades.ExportLevels() : System.Array.Empty<int>()
        };

        SaveSystem.Save(data);
    }

    /// <summary>세이브가 있으면 이어서 시작한다.</summary>
    public void LoadProgress()
    {
        SaveData data = SaveSystem.Load();
        if (data == null)
        {
            return;
        }

        day = Mathf.Max(1, data.day);
        revenue = data.revenue;
        totalOrders = data.totalOrders;
        successfulOrders = data.successfulOrders;
        failedOrders = data.failedOrders;
        burntChicken = data.burntChicken;
        wastedFood = data.wastedFood;
        spending = data.spending;
        reputation = data.reputation > 0 ? data.reputation : MaxReputation;
        bestStreak = data.bestStreak;
        bestDayRevenue = data.bestDayRevenue;
        bestDay = data.bestDay;
        if (upgrades != null)
        {
            upgrades.ImportLevels(data.upgradeLevels);
            ApplyUpgrades();
        }

        dayStartNetRevenue = revenue + spending;
        dayStartOrders = totalOrders;
        dayStartSuccess = successfulOrders;
        dayStartFailed = failedOrders;
        ShowMessage($"DAY {day} 이어하기 (저장 {data.savedAt})");
    }

    /// <summary>증설 업그레이드로 열리는 예비 튀김기는 처음에는 꺼져 있다.</summary>
    public void RegisterReserveFryer(GameObject fryer)
    {
        reserveFryers.Add(fryer);
        fryer.SetActive(false);
    }

    public bool TrySpend(int amount)
    {
        if (revenue < amount)
        {
            return false;
        }

        revenue -= amount;
        spending += amount;
        return true;
    }

    /// <summary>구매한 업그레이드 레벨을 실제 게임 값에 반영한다.</summary>
    public void ApplyUpgrades()
    {
        if (upgrades == null)
        {
            return;
        }

        fryTime = BaseFryTime * Mathf.Pow(0.88f, upgrades.LevelOf(UpgradeKind.FryerSpeed));
        burnTime = fryTime + BurnGrace;
        orderInterval = BaseOrderInterval * Mathf.Pow(0.9f, upgrades.LevelOf(UpgradeKind.Marketing));
        GameTuning.PlayerSpeedMultiplier = Mathf.Pow(1.12f, upgrades.LevelOf(UpgradeKind.MoveSpeed));
        GameTuning.DeliverySpeedMultiplier = Mathf.Pow(0.85f, upgrades.LevelOf(UpgradeKind.ScooterSpeed));

        int extraFryers = upgrades.LevelOf(UpgradeKind.ExtraFryer);
        for (int index = 0; index < reserveFryers.Count; index++)
        {
            if (reserveFryers[index] != null)
            {
                reserveFryers[index].SetActive(index < extraFryers);
            }
        }
    }

    public void BuyUpgrade(int slot)
    {
        if (upgrades != null)
        {
            upgrades.TryPurchase(slot);
        }
    }

    public void OnEventStarted(GameEvent gameEvent)
    {
        switch (gameEvent.kind)
        {
            case GameEventKind.RushHour:
                orderIntervalMultiplier = 0.5f;
                break;
            case GameEventKind.AppPromotion:
                deliveryFeeMultiplier = 2f;
                break;
            case GameEventKind.PickyCustomers:
                patienceMultiplier = 0.5f;
                break;
            case GameEventKind.Blackout:
                powerOn = false;
                if (ShopLighting.Instance != null)
                {
                    ShopLighting.Instance.SetPowered(false);
                }

                break;
        }

        ShowMessage($"[{gameEvent.displayName}] {gameEvent.description}");
    }

    public void OnEventEnded(GameEvent gameEvent)
    {
        orderIntervalMultiplier = 1f;
        deliveryFeeMultiplier = 1f;
        patienceMultiplier = 1f;
        if (!powerOn)
        {
            powerOn = true;
            if (ShopLighting.Instance != null)
            {
                ShopLighting.Instance.SetPowered(true);
            }
        }

        ShowMessage($"{gameEvent.displayName} 종료");
    }

    public void PayFine(int amount)
    {
        revenue = Mathf.Max(0, revenue - amount);
        spending += amount;
    }

    /// <summary>평판이 0이 되면 폐업, 목표 매출을 넘기면 승리.</summary>
    public void ChangeReputation(int amount)
    {
        reputation = Mathf.Clamp(reputation + amount, 0, MaxReputation);
        if (reputation <= 0)
        {
            FinishGame(false);
        }
    }

    private void CheckVictory()
    {
        if (revenue >= TargetRevenue)
        {
            FinishGame(true);
        }
    }

    private void FinishGame(bool won)
    {
        if (finished || GameFlow.Instance == null)
        {
            return;
        }

        finished = true;
        Debug.Log($"[Day] 게임 종료 - {(won ? "승리" : "폐업")}, 평판 {reputation}, 매출 {revenue}");
        StringBuilder text = new StringBuilder();
        text.AppendLine(won ? "목표 달성! 치킨 재벌" : "평판 0 - 폐업했습니다");
        text.AppendLine();
        text.AppendLine($"DAY {day} 까지 영업");
        text.AppendLine($"누적 매출  ₩{revenue:N0} / ₩{TargetRevenue:N0}");
        text.AppendLine($"주문 {totalOrders}건   성공 {successfulOrders}   실패 {failedOrders}");
        text.AppendLine($"탄 치킨 {burntChicken}   버린 음식 {wastedFood}");
        if (hazards != null)
        {
            text.AppendLine($"미끄러짐 {hazards.SlipCount}회   화재 {hazards.FireCount}회");
        }

        text.AppendLine();
        text.AppendLine("SPACE 를 눌러 처음부터");

        if (won)
        {
            GameFlow.Instance.EnterVictory(text.ToString());
        }
        else
        {
            GameFlow.Instance.EnterDefeat(text.ToString());
        }
    }
}
