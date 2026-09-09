using System.Collections.Generic;
using UnityEngine;

public enum MenuKind
{
    Fried,
    Seasoned,
    Soy
}

/// <summary>하나의 메뉴가 어떤 재료 흐름과 가격을 가지는지 정의한다.</summary>
public sealed class MenuRecipe
{
    public readonly MenuKind kind;
    public readonly string displayName;
    public readonly int price;
    public readonly bool needsSauce;
    public readonly Color cookedColor;
    public readonly Color packagedColor;

    public MenuRecipe(MenuKind kind, string displayName, int price, bool needsSauce, Color cookedColor, Color packagedColor)
    {
        this.kind = kind;
        this.displayName = displayName;
        this.price = price;
        this.needsSauce = needsSauce;
        this.cookedColor = cookedColor;
        this.packagedColor = packagedColor;
    }
}

public static class MenuDatabase
{
    public static readonly MenuRecipe Fried = new MenuRecipe(
        MenuKind.Fried, "후라이드", 18_000, false,
        new Color(0.85f, 0.55f, 0.15f), new Color(0.95f, 0.85f, 0.35f));

    public static readonly MenuRecipe Seasoned = new MenuRecipe(
        MenuKind.Seasoned, "양념", 21_000, true,
        new Color(0.85f, 0.3f, 0.05f), new Color(0.9f, 0.35f, 0.3f));

    public static readonly MenuRecipe Soy = new MenuRecipe(
        MenuKind.Soy, "간장", 20_000, true,
        new Color(0.85f, 0.3f, 0.05f), new Color(0.55f, 0.38f, 0.18f));

    private static readonly MenuRecipe[] all = { Fried, Seasoned, Soy };

    public static IReadOnlyList<MenuRecipe> All => all;

    public static MenuRecipe Get(MenuKind kind)
    {
        foreach (MenuRecipe recipe in all)
        {
            if (recipe.kind == kind)
            {
                return recipe;
            }
        }

        return Fried;
    }

    /// <summary>DAY 가 올라갈수록 손이 더 가는 메뉴가 자주 나온다.</summary>
    public static MenuRecipe RandomFor(int day)
    {
        int unlocked = day <= 1 ? 1 : day <= 2 ? 2 : all.Length;
        return all[Random.Range(0, unlocked)];
    }
}
