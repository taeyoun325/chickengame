using System.Collections.Generic;
using UnityEngine;

/// <summary>바닥에 튄 기름. 밟으면 미끄러진다.</summary>
public sealed class OilPuddle : MonoBehaviour
{
    public const float Radius = 1.1f;

    private float lifetime = 40f;

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    public bool Covers(Vector3 position)
    {
        Vector3 flat = new Vector3(position.x - transform.position.x, 0f, position.z - transform.position.z);
        return flat.sqrMagnitude <= Radius * Radius;
    }
}

/// <summary>튀김기에 붙은 불. 끄기 전까지 그 튀김기는 못 쓴다.</summary>
public sealed class FryerFire : MonoBehaviour
{
    public Station Fryer { get; private set; }

    public void Attach(Station fryer)
    {
        Fryer = fryer;
    }

    private void Update()
    {
        float pulse = 1f + Mathf.Sin(Time.time * 12f) * 0.15f;
        transform.localScale = new Vector3(1.2f, 1.8f * pulse, 1.2f);
    }
}

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
        spot.y = 0.02f;
        GameObject puddleObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        puddleObject.name = "Oil Puddle";
        puddleObject.transform.position = spot;
        puddleObject.transform.localScale = new Vector3(OilPuddle.Radius * 2f, 0.02f, OilPuddle.Radius * 2f);
        Destroy(puddleObject.GetComponent<Collider>());
        puddleObject.GetComponent<Renderer>().material.color = new Color(0.75f, 0.6f, 0.15f, 1f);
        puddles.Add(puddleObject.AddComponent<OilPuddle>());
        game.ShowMessage("기름이 튀었습니다! 조심하세요");
    }

    private void CheckSlips(float deltaTime)
    {
        foreach (PlayerInteraction actor in FindObjectsByType<PlayerInteraction>(FindObjectsInactive.Exclude))
        {
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
                game.ShowMessage("미끄러졌습니다!");
                break;
            }
        }
    }

    /// <summary>탄 치킨을 튀김기에 오래 두면 불이 난다.</summary>
    private void CheckBurntFires(float deltaTime)
    {
        foreach (Station station in FindObjectsByType<Station>(FindObjectsInactive.Exclude))
        {
            if (station.stationType != StationType.Fryer)
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
        GameObject fireObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        fireObject.name = "Fryer Fire";
        fireObject.transform.position = fryer.transform.position + Vector3.up * 1.6f;
        Destroy(fireObject.GetComponent<Collider>());
        fireObject.GetComponent<Renderer>().material.color = new Color(1f, 0.4f, 0.05f);
        FryerFire fire = fireObject.AddComponent<FryerFire>();
        fire.Attach(fryer);
        fires.Add(fire);
        FireCount++;
        game.ShowMessage("불이 났습니다! 소화기를 가져오세요");
    }

    /// <summary>소화기를 든 플레이어가 근처 불을 끈다.</summary>
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
                Destroy(stored.gameObject);
            }

            fires.RemoveAt(index);
            Destroy(fire.gameObject);
            return true;
        }

        return false;
    }

    private static Station FindAnyFryer()
    {
        Station picked = null;
        int seen = 0;
        foreach (Station station in FindObjectsByType<Station>(FindObjectsInactive.Exclude))
        {
            if (station.stationType != StationType.Fryer)
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
