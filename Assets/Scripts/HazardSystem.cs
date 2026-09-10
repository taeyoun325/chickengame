using System.Collections.Generic;
using UnityEngine;

/// <summary>기름 웅덩이와 화재를 만들고, 사고를 집계한다.</summary>
public sealed class HazardSystem : MonoBehaviour
{
    private const float OilInterval = 22f;
    private const float BurntFireDelay = 10f;
    private const float SlipDuration = 1.3f;
    private const float SlipCooldown = 2.5f;

    private readonly List<OilPuddle> puddles = new List<OilPuddle>();
    private readonly List<FryerFire> fires = new List<FryerFire>();
    private readonly Dictionary<PlayerInteraction, float> slipCooldowns = new Dictionary<PlayerInteraction, float>();
    private readonly Dictionary<Station, float> burntTimers = new Dictionary<Station, float>();
    private RestaurantGame game;
    private float oilTimer = OilInterval;

    public int SlipCount { get; private set; }
    public int FireCount { get; private set; }
    public bool AnyFire => fires.Count > 0;

    public void Initialise(RestaurantGame restaurantGame)
    {
        game = restaurantGame;
    }

    public bool IsOnFire(Station fryer)
    {
        foreach (FryerFire fire in fires)
        {
            if (fire != null && fire.Fryer == fryer)
            {
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        if (!KitchenNetwork.IsHostSide)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        puddles.RemoveAll(puddle => puddle == null);
        fires.RemoveAll(fire => fire == null);

        oilTimer -= deltaTime;
        if (oilTimer <= 0f)
        {
            SpillOil();
            oilTimer = OilInterval;
        }

        CheckSlips(deltaTime);
        CheckBurntFires(deltaTime);
    }

    /// <summary>튀김기 주변 아무 데나 기름이 튄다.</summary>
    private void SpillOil()
    {
        if (puddles.Count >= 4)
        {
            return;
        }

        Station fryer = FindAnyFryer();
        if (fryer == null)
        {
            return;
        }

        Vector3 spot = fryer.transform.position + new Vector3(Random.Range(-2.5f, 2.5f), 0f, Random.Range(-3f, -1f));
        SpillOilAt(spot);
    }

    /// <summary>정해진 자리에 기름을 쏟는다. 튀김기에서 튀는 경우 말고도
    /// 떨어뜨린 음식처럼 다른 사고에서 불러 쓸 수 있어야 한다.</summary>
    public void SpillOilAt(Vector3 spot)
    {
        spot.y = 0.02f;
        GameObject puddleObject = NetworkSpawner.SpawnHazard(
            "NetworkOil", PrimitiveType.Cylinder, spot,
            new Vector3(OilPuddle.Radius * 2f, 0.02f, OilPuddle.Radius * 2f),
            new Color(0.75f, 0.6f, 0.15f));
        OilPuddle puddle = puddleObject.GetComponent<OilPuddle>() ?? puddleObject.AddComponent<OilPuddle>();
        puddles.Add(puddle);
        game.ShowMessage("기름이 튀었습니다! 조심하세요");
    }

    private void CheckSlips(float deltaTime)
    {
        foreach (PlayerInteraction actor in WorldRegistry.Players)
        {
            if (actor == null)
            {
                continue;
            }

            if (slipCooldowns.TryGetValue(actor, out float cooldown) && cooldown > 0f)
            {
                slipCooldowns[actor] = cooldown - deltaTime;
                continue;
            }

            foreach (OilPuddle puddle in puddles)
            {
                if (puddle == null || !puddle.Covers(actor.transform.position))
                {
                    continue;
                }

                PlayerMotor motor = actor.GetComponent<PlayerMotor>();
                if (motor == null)
                {
                    continue;
                }

                motor.Slip(SlipDuration, actor.transform.forward);
                slipCooldowns[actor] = SlipCooldown;
                SlipCount++;
                RestaurantGame.PlaySound(GameSound.Slip);
                GameEffects.Shake(0.35f, 0.35f);
                game.ShowMessage("미끄러졌습니다!");
                break;
            }
        }
    }

    /// <summary>탄 치킨을 튀김기에 오래 두면 불이 난다.</summary>
    private void CheckBurntFires(float deltaTime)
    {
        foreach (Station station in WorldRegistry.Stations)
        {
            if (station == null || station.stationType != StationType.Fryer || !station.gameObject.activeInHierarchy)
            {
                continue;
            }

            FoodItem stored = station.StoredFood;
            bool smoking = stored != null && stored.state == FoodState.Burnt;
            if (!smoking || IsOnFire(station))
            {
                burntTimers.Remove(station);
                continue;
            }

            burntTimers.TryGetValue(station, out float elapsed);
            elapsed += deltaTime;
            burntTimers[station] = elapsed;
            if (elapsed >= BurntFireDelay)
            {
                burntTimers.Remove(station);
                StartFire(station);
            }
        }
    }

    private void StartFire(Station fryer)
    {
        GameObject fireObject = NetworkSpawner.SpawnHazard(
            "NetworkFire", PrimitiveType.Capsule, fryer.transform.position + Vector3.up * 1.6f,
            new Vector3(1.2f, 1.8f, 1.2f), new Color(1f, 0.4f, 0.05f));
        FryerFire fire = fireObject.GetComponent<FryerFire>() ?? fireObject.AddComponent<FryerFire>();
        fire.Attach(fryer);
        fires.Add(fire);
        FireCount++;
        RestaurantGame.PlaySound(GameSound.Fire);
        GameEffects.Shake(0.6f, 0.5f);
        GameEffects.Burst(fireObject.transform.position, new Color(1f, 0.45f, 0.1f), 30);
        game.ShowMessage("불이 났습니다! 소화기를 가져오세요");
    }

    /// <summary>소화기를 든 플레이어가 근처 불을 끈다.</summary>
    /// <summary>발밑의 기름을 닦는다. 사고가 나도 사람 손으로 되돌릴 수 있어야
    /// 미끄러짐이 그냥 당하는 일이 아니라 수습할 수 있는 일이 된다.</summary>
    public bool TryClean(Vector3 position)
    {
        OilPuddle puddle = OilPuddle.Covering(position);
        if (puddle == null)
        {
            return false;
        }

        puddles.Remove(puddle);
        puddle.Remove();
        return true;
    }

    public bool TryExtinguish(Vector3 position)
    {
        for (int index = fires.Count - 1; index >= 0; index--)
        {
            FryerFire fire = fires[index];
            if (fire == null)
            {
                continue;
            }

            if (Vector3.Distance(position, fire.transform.position) > 2.6f)
            {
                continue;
            }

            FoodItem stored = fire.Fryer != null ? fire.Fryer.StoredFood : null;
            if (stored != null)
            {
                NetworkSpawner.Remove(stored.gameObject);
            }

            fires.RemoveAt(index);
            NetworkSpawner.Remove(fire.gameObject);
            return true;
        }

        return false;
    }

    private static Station FindAnyFryer()
    {
        Station picked = null;
        int seen = 0;
        foreach (Station station in WorldRegistry.Stations)
        {
            if (station == null || station.stationType != StationType.Fryer || !station.gameObject.activeInHierarchy)
            {
                continue;
            }

            seen++;
            if (Random.Range(0, seen) == 0)
            {
                picked = station;
            }
        }

        return picked;
    }
}
