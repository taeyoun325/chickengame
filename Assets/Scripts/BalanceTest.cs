using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>-balancetest 로 실행하면 봇들이 쉬지 않고 주문을 처리하면서
/// DAY 별 매출과 평판을 기록한다. 밸런스를 눈이 아니라 숫자로 본다.
/// 로컬 플레이어 전원을 조작하므로 2인 협동 기준도 잴 수 있다.</summary>
public sealed class BalanceTest : MonoBehaviour
{
    private const float TimeScale = 4f;

    private readonly HashSet<int> claimedOrders = new HashSet<int>();
    private RestaurantGame game;
    private int lastDay = 1;
    private int lastGross;
    private int daysToRun = 6;
    private int botCount = 2;

    public static bool Requested => Argument("-balancetest") != null;

    private static string Argument(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int index = 0; index < args.Length; index++)
        {
            if (args[index] == name)
            {
                return index + 1 < args.Length ? args[index + 1] : string.Empty;
            }
        }

        return null;
    }

    private IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2f);

        if (int.TryParse(Argument("-balancetest"), out int parsed) && parsed > 0)
        {
            daysToRun = parsed;
        }

        if (int.TryParse(Argument("-bots"), out int bots) && bots > 0)
        {
            botCount = bots;
        }

        game = RestaurantGame.Instance;
        if (game == null || WorldRegistry.Players.Count == 0)
        {
            Debug.LogError("[Balance] 게임을 찾지 못했습니다");
            Application.Quit(1);
            yield break;
        }

        botCount = Mathf.Min(botCount, WorldRegistry.Players.Count);
        GameSpeed.Normal = TimeScale;
        GameSpeed.Resume();
        Debug.Log($"[Balance] 시작 - {daysToRun} DAY, 봇 {botCount}명, 배속 {TimeScale}x");

        for (int index = 0; index < botCount; index++)
        {
            StartCoroutine(RunBot(WorldRegistry.Players[index], index));
        }

        while (game.Day <= daysToRun)
        {
            ReportDayChange();

            if (GameFlow.Instance != null &&
                (GameFlow.Instance.State == GameState.Defeat || GameFlow.Instance.State == GameState.Victory))
            {
                bool won = GameFlow.Instance.State == GameState.Victory;
                Debug.Log($"[Balance] {(won ? "목표 달성" : "폐업")} - DAY {game.Day}, 총 매출 ₩{game.GrossEarned:N0}");
                break;
            }

            BuyWhatWeCanAfford();
            yield return null;
        }

        Debug.Log($"[Balance] 종료 - DAY {game.Day} 총 매출 ₩{game.GrossEarned:N0} (보유 ₩{game.Revenue:N0})");
        GameSpeed.Normal = 1f;
        GameSpeed.Resume();
        Application.Quit(0);
    }

    /// <summary>봇 한 명의 작업 루프. 봇마다 다른 튀김기를 쓰려고 한다.</summary>
    private IEnumerator RunBot(PlayerInteraction bot, int botIndex)
    {
        while (true)
        {
            if (bot == null || GameFlow.Instance == null || !GameFlow.Instance.IsPlaying)
            {
                yield return null;
                continue;
            }

            RestaurantOrder target = ClaimOrder();
            if (target == null)
            {
                yield return null;
                continue;
            }

            yield return ServeOrder(bot, botIndex, target);
            claimedOrders.Remove(target.number);
        }
    }

    /// <summary>다른 봇이 잡지 않은 주문 중 가장 급한 것을 고른다.</summary>
    private RestaurantOrder ClaimOrder()
    {
        RestaurantOrder best = null;
        foreach (RestaurantOrder order in game.ActiveOrders)
        {
            if (order.Remaining <= 0 || claimedOrders.Contains(order.number))
            {
                continue;
            }

            if (best == null || order.remainingTime < best.remainingTime)
            {
                best = order;
            }
        }

        if (best != null)
        {
            claimedOrders.Add(best.number);
        }

        return best;
    }

    private IEnumerator ServeOrder(PlayerInteraction bot, int botIndex, RestaurantOrder target)
    {
        Station fryer = FindFreeFryer(botIndex);
        if (fryer == null)
        {
            // 빈 튀김기가 없으면 잠깐 기다린다. 사람도 그렇게 한다.
            yield return null;
            yield break;
        }

        game.InteractWithStation(bot, FindStation(StationType.Fridge));
        if (bot.HeldFood == null)
        {
            yield break;
        }

        game.InteractWithStation(bot, fryer);
        if (bot.HeldFood != null)
        {
            // 다른 봇이 한발 빨랐다면 들고 있던 생닭을 버린다.
            game.InteractWithStation(bot, FindStation(StationType.Trash));
            yield break;
        }

        // 업그레이드로 튀김 시간이 줄어드니 고정값으로 기다리면 다 태운다.
        yield return new WaitForSeconds(game.FryTime + 0.3f);
        game.InteractWithStation(bot, fryer);

        if (bot.HeldFood == null || bot.HeldFood.state != FoodState.Cooked)
        {
            game.InteractWithStation(bot, FindStation(StationType.Trash));
            yield break;
        }

        if (target.recipe.needsSauce)
        {
            Station sauce = FindStation(StationType.Sauce);
            if (sauce != null)
            {
                bot.SetSauceChoice(target.recipe.kind);
                game.InteractWithStation(bot, sauce);
            }
        }

        game.InteractWithStation(bot, FindStation(StationType.Packing));
        game.InteractWithStation(bot, FindStation(StationType.Checkout));

        if (bot.HeldFood != null)
        {
            game.InteractWithStation(bot, FindStation(StationType.Trash));
        }
    }

    /// <summary>돈이 모이면 업그레이드를 산다.</summary>
    private void BuyWhatWeCanAfford()
    {
        for (int slot = 0; slot < 5; slot++)
        {
            game.BuyUpgrade(slot);
        }
    }

    private void ReportDayChange()
    {
        if (game.Day == lastDay)
        {
            return;
        }

        int earned = game.GrossEarned - lastGross;
        Debug.Log($"[Balance] DAY {lastDay} 매출 ₩{earned:N0}  총 ₩{game.GrossEarned:N0}  " +
                  $"평판 {game.Reputation}  성공 {game.SuccessfulOrders}  실패 {game.FailedOrders}");
        lastDay = game.Day;
        lastGross = game.GrossEarned;
    }

    /// <summary>비어 있는 튀김기를 고른다. 봇마다 다른 쪽부터 훑어 충돌을 줄인다.</summary>
    private static Station FindFreeFryer(int botIndex)
    {
        List<Station> fryers = new List<Station>();
        foreach (Station station in WorldRegistry.Stations)
        {
            if (station != null && station.stationType == StationType.Fryer && station.gameObject.activeInHierarchy)
            {
                fryers.Add(station);
            }
        }

        if (fryers.Count == 0)
        {
            return null;
        }

        for (int step = 0; step < fryers.Count; step++)
        {
            Station candidate = fryers[(botIndex + step) % fryers.Count];
            if (candidate.StoredFood == null)
            {
                return candidate;
            }
        }

        return null;
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
}
