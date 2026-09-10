using UnityEngine;

/// <summary>스테이션 앞에서 지금 무엇을 하면 되는지 한 줄로 알려준다.
/// 월드 이름표와 화면 중앙 안내가 같은 문장을 써야 헷갈리지 않는다.</summary>
public static class StationHints
{
    public static string For(Station station, PlayerInteraction actor)
    {
        if (station == null || actor == null)
        {
            return string.Empty;
        }

        FoodItem held = actor.HeldFood;
        switch (station.stationType)
        {
            case StationType.Fridge:
                return actor.HandsFree ? "생닭 꺼내기" : "손 비우기";
            case StationType.Fryer:
                if (held != null && held.state == FoodState.Raw) return "넣기";
                if (station.StoredFood != null && station.StoredFood.state != FoodState.Frying) return "꺼내기";
                return "생닭 필요";
            case StationType.Sauce:
                return held != null && held.state == FoodState.Cooked
                    ? $"{MenuDatabase.Get(actor.SauceChoice).displayName} 바르기 (Q 로 소스 변경)"
                    : "튀긴 치킨 필요";
            case StationType.Packing:
                return held != null && held.ReadyToPack ? "포장하기" : "익은 치킨 필요";
            case StationType.Checkout:
                return held != null && held.state == FoodState.Packaged ? "손님에게 주기" : "포장 필요";
            case StationType.Delivery:
                return held != null && held.state == FoodState.Packaged ? "배달 보내기" : "포장 필요";
            case StationType.Upgrade:
                return "숫자 키로 구매";
            case StationType.Trash:
                return held != null ? "버리기" : string.Empty;
            case StationType.Extinguisher:
                return actor.CarryingExtinguisher ? "제자리에 두기" : "소화기 들기";
            default:
                return string.Empty;
        }
    }
}
