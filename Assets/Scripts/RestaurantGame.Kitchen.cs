using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>주방 조리와 스테이션 상호작용. RestaurantGame 의 일부다.</summary>
public sealed partial class RestaurantGame
{
    public bool TryExtinguishNearby(Vector3 position)
    {
        if (hazards == null || !hazards.TryExtinguish(position))
        {
            return false;
        }

        ShowMessage("불을 껐습니다!");
        return true;
    }

    private void ThrowAway(PlayerInteraction actor)
    {
        if (actor.HeldFood == null)
        {
            ShowMessage("버릴 것이 없습니다");
            return;
        }

        string description = actor.HeldFood.Describe();
        NetworkSpawner.Remove(actor.HeldFood.gameObject);
        actor.ClearHeldFood();
        wastedFood++;
        ShowMessage($"{description}을 버렸습니다");
    }

    private void ToggleExtinguisher(PlayerInteraction actor)
    {
        if (actor.CarryingExtinguisher)
        {
            actor.ReturnExtinguisher();
            ShowMessage("소화기를 제자리에 두었습니다");
            return;
        }

        if (!actor.HandsFree)
        {
            ShowMessage("손을 비우고 오세요");
            return;
        }

        actor.TakeExtinguisher();
        ShowMessage("소화기를 들었습니다. 불 앞에서 E");
    }

    /// <summary>조리는 호스트가 등록된 음식들을 직접 돌린다.
    /// 프리팹에 붙는 별도 컴포넌트를 두면 직렬화가 깨질 여지가 있어 한곳에서 처리한다.</summary>
    private void TickAllFood(float deltaTime)
    {
        for (int index = 0; index < WorldRegistry.Foods.Count; index++)
        {
            FoodItem food = WorldRegistry.Foods[index];
            if (food != null)
            {
                TickFood(food, deltaTime);
            }
        }
    }

    public void TickFood(FoodItem food, float deltaTime)
    {
        if (!powerOn || food.burnCounted)
        {
            return;
        }

        // 익은 뒤에도 튀김기에 그대로 두면 계속 익어서 결국 탄다.
        // (익는 순간 진행이 멈춰 버려서 한동안 치킨이 아예 타지 않았다.)
        bool restingInFryer = food.RestingStation != null && food.RestingStation.stationType == StationType.Fryer;
        bool stillCooking = food.state == FoodState.Frying || (food.state == FoodState.Cooked && restingInFryer);
        if (!stillCooking)
        {
            return;
        }

        food.cookProgress += deltaTime;
        if (food.cookProgress >= burnTime && !food.burnCounted)
        {
            food.burnCounted = true;
            burntChicken++;
            ChangeReputation(-2);
            food.SetState(FoodState.Burnt);
            PlaySound(GameSound.Burnt);
            ShowMessage("치킨이 탔습니다!");
        }
        else if (food.cookProgress >= fryTime && food.state == FoodState.Frying)
        {
            food.SetState(FoodState.Cooked);
            ShowMessage("치킨이 익었습니다! 튀김기에서 꺼내세요");
        }
    }

    private void TakeRawChicken(PlayerInteraction actor)
    {
        if (!actor.HandsFree)
        {
            ShowMessage("손에 든 물건을 먼저 내려놓으세요");
            return;
        }

        FoodItem chicken = NetworkSpawner.SpawnChicken();
        chicken.SetState(FoodState.Raw);
        actor.SetHeldFood(chicken);
        ShowMessage("생닭을 들었습니다");
    }

    private void UseFryer(PlayerInteraction actor, Station station)
    {
        if (hazards != null && hazards.IsOnFire(station))
        {
            ShowMessage("불이 붙은 튀김기입니다! 소화기를 쓰세요");
            return;
        }

        if (actor.HeldFood != null && actor.HeldFood.state == FoodState.Raw)
        {
            if (station.StoredFood != null)
            {
                ShowMessage("튀김기가 사용 중입니다");
                return;
            }

            FoodItem chicken = actor.ReleaseHeldFoodTo(station);
            chicken.SetState(FoodState.Frying);
            chicken.cookProgress = 0f;
            PlaySound(GameSound.FryStart);
            ShowMessage($"튀김 시작! {fryTime:0.0}초 뒤 꺼내세요");
            return;
        }

        FoodItem cookedChicken = station.StoredFood;
        if (actor.HandsFree && cookedChicken != null && cookedChicken.state != FoodState.Frying)
        {
            actor.SetHeldFood(cookedChicken);
            ShowMessage(cookedChicken.state == FoodState.Burnt ? "탄 치킨입니다. 버리세요" : "튀긴 치킨을 들었습니다");
            return;
        }

        ShowMessage("생닭을 들고 튀김기에 넣으세요");
    }

    private void ApplySauce(PlayerInteraction actor, Station station)
    {
        FoodItem food = actor.HeldFood;
        if (food == null || food.state != FoodState.Cooked)
        {
            ShowMessage("튀긴 치킨을 들고 오세요");
            return;
        }

        if (food.sauced)
        {
            ShowMessage("이미 양념을 발랐습니다");
            return;
        }

        MenuRecipe recipe = MenuDatabase.Get(station.sauceKind);
        food.sauced = true;
        food.SetRecipe(recipe);
        ShowMessage($"{recipe.displayName} 양념 완료!");
    }

    /// <summary>양념대에서 바를 소스를 바꾼다.</summary>
    public void CycleSauce(Station station)
    {
        station.sauceKind = station.sauceKind == MenuKind.Seasoned ? MenuKind.Soy : MenuKind.Seasoned;
        ShowMessage($"양념대: {MenuDatabase.Get(station.sauceKind).displayName}");
    }

    private void PackChicken(PlayerInteraction actor)
    {
        FoodItem food = actor.HeldFood;
        if (food == null || food.state != FoodState.Cooked)
        {
            ShowMessage("익은 치킨을 들고 오세요");
            return;
        }

        if (food.dirty)
        {
            ShowMessage("바닥에 떨어진 치킨입니다. 쓰레기통에 버리세요");
            return;
        }

        if (!food.ReadyToPack)
        {
            ShowMessage($"{food.Recipe.displayName}은 양념대를 먼저 거쳐야 합니다");
            return;
        }

        food.SetState(FoodState.Packaged);
        ShowMessage($"{food.Recipe.displayName} 포장 완료! 계산대로 가져가세요");
    }
}
