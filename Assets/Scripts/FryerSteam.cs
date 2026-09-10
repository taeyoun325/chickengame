using UnityEngine;

/// <summary>튀기는 중일 때만 김이 오르게 한다.</summary>
public sealed class FryerSteam : MonoBehaviour
{
    private Station station;
    private ParticleSystem particles;

    public void Bind(Station fryer, ParticleSystem steam)
    {
        station = fryer;
        particles = steam;
    }

    private static bool IsFryingNearby(Vector3 position)
    {
        foreach (FoodItem food in WorldRegistry.Foods)
        {
            if (food != null && food.state == FoodState.Frying &&
                Vector3.Distance(food.transform.position, position) < 1.6f)
            {
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        if (station == null || particles == null)
        {
            return;
        }

        // 클라이언트에는 StoredFood 참조가 없으므로, 가까이에 튀겨지는 치킨이 있는지로 판단한다.
        bool cooking = station.StoredFood != null
            ? station.StoredFood.state == FoodState.Frying
            : IsFryingNearby(station.transform.position);
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = cooking ? 16f : 0f;
    }
}
