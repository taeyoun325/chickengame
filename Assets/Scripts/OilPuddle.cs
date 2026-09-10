using System.Collections.Generic;
using UnityEngine;

/// <summary>바닥에 튄 기름. 밟으면 미끄러지고, 손이 비어 있으면 닦을 수 있다.</summary>
public sealed class OilPuddle : MonoBehaviour
{
    public const float Radius = 1.1f;

    private static readonly List<OilPuddle> live = new List<OilPuddle>();

    private float lifetime = 40f;

    /// <summary>호스트의 HazardSystem 만 목록을 들고 있으면 접속한 플레이어 화면에서는
    /// 발밑에 기름이 있는지 알 수가 없다. 안내를 띄우려면 양쪽 다 볼 수 있어야 한다.</summary>
    public static OilPuddle Covering(Vector3 position)
    {
        for (int index = live.Count - 1; index >= 0; index--)
        {
            OilPuddle puddle = live[index];
            if (puddle == null)
            {
                live.RemoveAt(index);
                continue;
            }

            if (puddle.Covers(position))
            {
                return puddle;
            }
        }

        return null;
    }

    /// <summary>닦은 즉시 목록에서 빠져야 한다. Destroy 는 프레임 끝에야 실제로 지워지므로,
    /// 여기서 빼두지 않으면 방금 닦은 자리에서 그대로 미끄러진다.</summary>
    public void Remove()
    {
        live.Remove(this);
        NetworkSpawner.Remove(gameObject);
    }

    private void Awake()
    {
        live.Add(this);
    }

    private void OnDestroy()
    {
        live.Remove(this);
    }

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
            Remove();
        }
    }

    public bool Covers(Vector3 position)
    {
        Vector3 flat = new Vector3(position.x - transform.position.x, 0f, position.z - transform.position.z);
        return flat.sqrMagnitude <= Radius * Radius;
    }
}
