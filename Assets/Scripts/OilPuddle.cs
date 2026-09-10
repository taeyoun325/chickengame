using UnityEngine;

/// <summary>바닥에 튄 기름. 밟으면 미끄러진다.</summary>
public sealed class OilPuddle : MonoBehaviour
{
    public const float Radius = 1.1f;

    private float lifetime = 40f;

    private void Start()
    {
        GameLog.Verbose($"[Haz] 기름 웅덩이 ({(KitchenNetwork.IsHostSide ? "host" : "client")})");
    }

    private void Update()
    {
        if (!KitchenNetwork.IsHostSide)
        {
            return;
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            NetworkSpawner.Remove(gameObject);
        }
    }

    public bool Covers(Vector3 position)
    {
        Vector3 flat = new Vector3(position.x - transform.position.x, 0f, position.z - transform.position.z);
        return flat.sqrMagnitude <= Radius * Radius;
    }
}
