using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum UpgradeKind
{
    FryerSpeed,
    ExtraFryer,
    MoveSpeed,
    ScooterSpeed,
    Marketing
}

public sealed class Upgrade
{
    public readonly UpgradeKind kind;
    public readonly string displayName;
    public readonly string effect;
    public readonly int baseCost;
    public readonly int maxLevel;

    public int Level { get; private set; }

    public Upgrade(UpgradeKind kind, string displayName, string effect, int baseCost, int maxLevel)
    {
        this.kind = kind;
        this.displayName = displayName;
        this.effect = effect;
        this.baseCost = baseCost;
        this.maxLevel = maxLevel;
    }

    public bool MaxedOut => Level >= maxLevel;

    /// <summary>레벨이 오를수록 가격이 1.8배씩 뛴다.</summary>
    public int NextCost => Mathf.RoundToInt(baseCost * Mathf.Pow(1.8f, Level));

    public void LevelUp()
    {
        Level = Mathf.Min(Level + 1, maxLevel);
    }

    public void SetLevel(int level)
    {
        Level = Mathf.Clamp(level, 0, maxLevel);
    }
}

/// <summary>매출을 써서 가게를 키운다. 업그레이드 스테이션에서 1~5 키로 구매한다.</summary>
public sealed class UpgradeSystem : MonoBehaviour
{
    private readonly List<Upgrade> upgrades = new List<Upgrade>
    {
        new Upgrade(UpgradeKind.FryerSpeed, "고성능 튀김기", "튀김 시간 -12%", 120_000, 4),
        new Upgrade(UpgradeKind.ExtraFryer, "튀김기 증설", "튀김기 +1대", 250_000, 2),
        new Upgrade(UpgradeKind.MoveSpeed, "미끄럼방지 신발", "이동 속도 +12%", 90_000, 4),
        new Upgrade(UpgradeKind.ScooterSpeed, "배달 스쿠터 튜닝", "배달 시간 -15%", 150_000, 3),
        new Upgrade(UpgradeKind.Marketing, "전단지 마케팅", "주문 간격 -10%", 110_000, 3)
    };

    private RestaurantGame game;

    public IReadOnlyList<Upgrade> Upgrades => upgrades;

    public void Initialise(RestaurantGame restaurantGame)
    {
        game = restaurantGame;
    }

    public int LevelOf(UpgradeKind kind)
    {
        return Find(kind).Level;
    }

    public Upgrade Find(UpgradeKind kind)
    {
        foreach (Upgrade upgrade in upgrades)
        {
            if (upgrade.kind == kind)
            {
                return upgrade;
            }
        }

        return upgrades[0];
    }

    /// <summary>스테이션 앞에서 숫자 키를 눌렀을 때 호출된다.</summary>
    public void TryPurchase(int slot)
    {
        if (game == null || slot < 0 || slot >= upgrades.Count)
        {
            return;
        }

        Upgrade upgrade = upgrades[slot];
        if (upgrade.MaxedOut)
        {
            game.ShowMessage($"{upgrade.displayName}은 이미 최대 레벨입니다");
            return;
        }

        int cost = upgrade.NextCost;
        if (!game.TrySpend(cost))
        {
            game.ShowMessage($"자금 부족! {upgrade.displayName} ₩{cost:N0} 필요");
            return;
        }

        upgrade.LevelUp();
        game.ApplyUpgrades();
        RestaurantGame.PlaySound(GameSound.Purchase);
        GameEffects.Burst(transform.position + Vector3.up, new Color(0.6f, 0.4f, 0.9f));
        game.ShowMessage($"{upgrade.displayName} Lv.{upgrade.Level} 구매! {upgrade.effect}");
    }

    public int[] ExportLevels()
    {
        int[] levels = new int[upgrades.Count];
        for (int index = 0; index < upgrades.Count; index++)
        {
            levels[index] = upgrades[index].Level;
        }

        return levels;
    }

    public void ImportLevels(int[] levels)
    {
        if (levels == null)
        {
            return;
        }

        for (int index = 0; index < upgrades.Count && index < levels.Length; index++)
        {
            upgrades[index].SetLevel(levels[index]);
        }
    }

    public string BuildShopText()
    {
        StringBuilder text = new StringBuilder("UPGRADE (스테이션 앞에서 1~5)\n");
        for (int index = 0; index < upgrades.Count; index++)
        {
            Upgrade upgrade = upgrades[index];
            string price = upgrade.MaxedOut ? "MAX" : $"₩{upgrade.NextCost:N0}";
            text.AppendLine($"{index + 1}. {upgrade.displayName} Lv.{upgrade.Level}  {price}  ({upgrade.effect})");
        }

        return text.ToString();
    }
}
