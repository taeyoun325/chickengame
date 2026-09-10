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
        // 최고 기록은 판이 바뀌어도 남아야 하므로, 새로 쓰기 전에 이어받는다.
        SaveData previous = SaveSystem.Load();

        SaveData data = new SaveData
        {
            bestClearSeconds = previous != null ? previous.bestClearSeconds : 0,
            playSeconds = Mathf.FloorToInt(playSeconds),
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
        playSeconds = data.playSeconds;
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
        int shoes = upgrades.LevelOf(UpgradeKind.MoveSpeed);
        GameTuning.PlayerSpeedMultiplier = Mathf.Pow(1.12f, shoes);
        GameTuning.SlipResistance = Mathf.Pow(0.7f, shoes);
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

    /// <summary>셀프테스트가 승리 경로를 밟아 보려고 쓴다. 정상 플레이로 ₩10,000,000 을
    /// 채우려면 수백 DAY 가 걸려 자동 검증이 불가능하다.</summary>
    public void AddRevenueForTest(int amount)
    {
        revenue += amount;
        CheckVictory();
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

        if (won)
        {
            // 목표 금액은 정해져 있으므로, 남는 기록은 걸린 시간뿐이다.
            text.AppendLine($"달성 시간  {Clock(playSeconds)}");
            string best = RecordClearTime();
            if (!string.IsNullOrEmpty(best))
            {
                text.AppendLine(best);
            }

            text.AppendLine();
            text.AppendLine("SPACE 를 눌러 처음부터");
            GameFlow.Instance.EnterVictory(text.ToString());
            BroadcastFinish(text.ToString(), true);
            return;
        }

        text.AppendLine(DiagnoseClosure());
        text.AppendLine();
        text.AppendLine($"R  재기하기 (매출 {ReopenPenaltyPercent}% 벌금, 업그레이드는 유지)");
        text.AppendLine("SPACE  처음부터");
        GameFlow.Instance.EnterDefeat(text.ToString());
        BroadcastFinish(text.ToString(), false);
    }

    /// <summary>최고 기록을 갱신하고, 갱신했으면 그렇다고 알려준다.
    /// 기록은 이 PC 에 남으므로 호스트 기준이다.</summary>
    private string RecordClearTime()
    {
        SaveData data = SaveSystem.Load() ?? new SaveData();
        int seconds = Mathf.Max(1, Mathf.FloorToInt(playSeconds));

        if (data.bestClearSeconds <= 0 || seconds < data.bestClearSeconds)
        {
            int previous = data.bestClearSeconds;
            data.bestClearSeconds = seconds;
            SaveSystem.Save(data);
            return previous <= 0
                ? "첫 기록입니다"
                : $"최고 기록 경신!  이전 {Clock(previous)}";
        }

        return $"최고 기록  {Clock(data.bestClearSeconds)}";
    }

    private static void BroadcastFinish(string report, bool won)
    {
        if (KitchenNetwork.Online && KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.BroadcastFinish(report, won);
        }
    }

    /// <summary>무엇 때문에 망했는지 한 줄로 알려준다. 그냥 문 닫았다고만 하면 배울 것이 없다.</summary>
    private string DiagnoseClosure()
    {
        int fires = hazards != null ? hazards.FireCount : 0;
        if (failedOrders >= burntChicken && failedOrders >= fires)
        {
            return $"놓친 주문 {failedOrders}건이 가장 크게 깎았습니다. 손이 모자라면 급한 손님부터 처리하세요.";
        }

        if (burntChicken >= fires)
        {
            return $"탄 치킨 {burntChicken}마리가 발목을 잡았습니다. 익으면 바로 꺼내세요.";
        }

        return $"화재 {fires}번이 결정적이었습니다. 탄 치킨을 튀김기에 두지 마세요.";
    }

    /// <summary>재기: 업그레이드와 DAY 는 남기고 평판을 절반으로 되돌린다.
    /// 며칠치 진행이 한 번에 날아가면 다시 앉을 마음이 들지 않는다.</summary>
    public void Reopen()
    {
        int fine = Mathf.RoundToInt(revenue * (ReopenPenaltyPercent / 100f));
        revenue = Mathf.Max(0, revenue - fine);
        spending += fine;
        reopenCount++;
        reputation = 50;
        finished = false;
        streak = 0;

        // 밀린 주문은 정리하고 다시 연다.
        for (int index = activeOrders.Count - 1; index >= 0; index--)
        {
            SendCustomerHome(activeOrders[index]);
            activeOrders.RemoveAt(index);
        }

        dayTimer = 0f;
        orderTimer = 6f;
        dayStartNetRevenue = revenue + spending;
        dayStartOrders = totalOrders;
        dayStartSuccess = successfulOrders;
        dayStartFailed = failedOrders;

        if (GameFlow.Instance != null)
        {
            GameFlow.Instance.ResumeFromSettlement();
        }

        if (KitchenNetwork.Online && KitchenNetwork.Instance.IsServer)
        {
            KitchenNetwork.Instance.BroadcastResume();
        }

        Debug.Log($"[Day] 재기 - 벌금 {fine}, 평판 50");
        ShowMessage($"재기! 벌금 ₩{fine:N0}, 평판 50에서 다시 시작합니다");
    }
}
