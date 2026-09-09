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

    private void Update()
    {
        if (station == null || particles == null)
        {
            return;
        }

        FoodItem stored = station.StoredFood;
        bool cooking = stored != null && stored.state == FoodState.Frying;
        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = cooking ? 16f : 0f;
    }
}

/// <summary>파티클과 카메라 흔들림 같은 즉각적인 피드백.</summary>
public static class GameEffects
{
    /// <summary>튀김기에서 올라오는 김.</summary>
    public static ParticleSystem CreateSteam(Transform parent, Vector3 localOffset)
    {
        GameObject steamObject = new GameObject("Steam");
        steamObject.transform.SetParent(parent, false);
        steamObject.transform.localPosition = localOffset;

        ParticleSystem particles = steamObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.startColor = new Color(0.9f, 0.9f, 0.9f, 0.35f);
        main.startLifetime = 1.4f;
        main.startSpeed = 1.1f;
        main.startSize = 0.5f;
        main.gravityModifier = -0.15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 12f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.5f;

        ParticleSystemRenderer renderer = steamObject.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        return particles;
    }

    /// <summary>계산 성공, 구매 등에 터지는 짧은 파티클.</summary>
    public static void Burst(Vector3 position, Color color, int count = 18)
    {
        GameObject burstObject = new GameObject("Burst");
        burstObject.transform.position = position;

        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.startColor = color;
        main.startLifetime = 0.7f;
        main.startSpeed = 3.5f;
        main.startSize = 0.22f;
        main.gravityModifier = 0.9f;
        main.duration = 0.4f;
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        ParticleSystemRenderer renderer = burstObject.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        particles.Play();
    }

    public static void Shake(float duration, float strength)
    {
        if (FollowPlayerCamera.Instance != null)
        {
            FollowPlayerCamera.Instance.Shake(duration, strength);
        }
    }
}
