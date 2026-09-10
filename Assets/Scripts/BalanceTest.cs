using System.Collections;
using UnityEngine;

/// <summary>-balancetest 로 실행하면 봇이 쉬지 않고 주문을 처리하면서
/// DAY 별 매출과 목표까지 걸리는 일수를 측정한다. 밸런스를 눈이 아니라 숫자로 본다.</summary>
public sealed class BalanceTest : MonoBehaviour
{
    private const float TimeScale = 4f;

    private RestaurantGame game;
    private PlayerInteraction bot;
    private int lastDay = 1;
    private int lastRevenue;
    private int daysToRun = 8;

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

        game = RestaurantGame.Instance;
        bot = WorldRegistry.Players.Count > 0 ? WorldRegistry.Players[0] : null;
        if (game == null || bot == null)
        {
            Debug.LogError("[Balance] 게임을 찾지 못했습니다");
            Application.Quit(1);
            yield break;
        }

        GameSpeed.Normal = TimeScale;
        GameSpeed.Resume();
        Debug.Log($"[Balance] 시작 - {daysToRun} DAY, 배속 {TimeScale}x");

        while (game.Day <= daysToRun)
        {
            ReportDayChange();

            // 폐업하거나 목표를 채우면 더 볼 것이 없다.
            if (GameFlow.Instance != null &&
                (GameFlow.Instance.State == GameState.Defeat || GameFlow.Instance.State == GameState.Victory))
            {
                Debug.Log($"[Balance] {(GameFlow.Instance.State == GameState.Victory ? "목표 달성" : "폐업")} - " +
                          $"DAY {game.Day}, 총 매출 ₩{game.GrossEarned:N0}");
                break;
            }

            BuyWhatWeCanAfford();
            yield return ServeOneOrder();
        }

        Debug.Log($"[Balance] 종료 - DAY {game.Day} 총 매출 ₩{game.GrossEarned:N0} (보유 ₩{game.Revenue:N0})");
        GameSpeed.Normal = 1f;
        GameSpeed.Resume();
        Application.Quit(0);
    }

    /// <summary>가장 급한 주문 하나를 처음부터 끝까지 처리한다.</summary>
    private IEnumerator ServeOneOrder()
    {
        if (game.ActiveOrders.Count == 0)
        {
            yield return null;
            yield break;
        }

        RestaurantOrder target = null;
        foreach (RestaurantOrder order in game.ActiveOrders)
        {
            if (order.Remaining > 0 && (target == null || order.remainingTime < target.remainingTime))
            {
                target = order;
            }
        }

        if (target == null)
        {
            yield return null;
            yield break;
        }

        MenuRecipe wanted = target.recipe;
        Station fryer = FindStation(StationType.Fryer);
        if (fryer == null)
        {
            yield return null;
            yield break;
        }

        // 냉장고 → 튀김기
        game.InteractWithStation(bot, FindStation(StationType.Fridge));
        if (bot.HeldFood == null)
        {
            yield return null;
            yield break;
        }

        game.InteractWithStation(bot, fryer);
        yield return new WaitForSeconds(5.2f);
        game.InteractWithStation(bot, fryer);

        if (bot.HeldFood == null || bot.HeldFood.state != FoodState.Cooked)
        {
            // 타 버렸으면 버리고 다시 시작한다.
            game.InteractWithStation(bot, FindStation(StationType.Trash));
            yield break;
        }

        if (wanted.needsSauce)
        {
            Station sauce = FindStation(StationType.Sauce);
            if (sauce != null)
            {
                sauce.sauceKind = wanted.kind;
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

    /// <summary>돈이 모이면 가장 싼 업그레이드부터 산다.</summary>
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

        int earned = game.GrossEarned - lastRevenue;
        Debug.Log($"[Balance] DAY {lastDay} 매출 ₩{earned:N0}  총 ₩{game.GrossEarned:N0}  " +
                  $"평판 {game.Reputation}  성공 {game.SuccessfulOrders}  실패 {game.FailedOrders}");
        lastDay = game.Day;
        lastRevenue = game.GrossEarned;
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
