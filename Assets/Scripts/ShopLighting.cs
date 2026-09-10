using UnityEngine;

/// <summary>가게 조명. 정전이 나면 어두워지고 깜빡인다.</summary>
public sealed class ShopLighting : MonoBehaviour
{
    public static ShopLighting Instance { get; private set; }

    private static readonly Color NormalAmbient = new Color(0.55f, 0.48f, 0.38f);
    private static readonly Color BlackoutAmbient = new Color(0.14f, 0.12f, 0.12f);

    private Light shopLight;
    private float normalIntensity;
    private bool powered = true;
    private float flickerTimer;

    private void Awake()
    {
        Instance = this;
        shopLight = GetComponent<Light>();
        normalIntensity = shopLight != null ? shopLight.intensity : 1.3f;
    }

    public void SetPowered(bool on)
    {
        powered = on;
        RenderSettings.ambientLight = on ? NormalAmbient : BlackoutAmbient;
        if (shopLight != null)
        {
            shopLight.intensity = on ? normalIntensity : normalIntensity * 0.25f;
        }
    }

    private void Update()
    {
        if (powered || shopLight == null)
        {
            return;
        }

        // 정전 중에는 조명이 불안하게 흔들린다.
        flickerTimer -= Time.deltaTime;
        if (flickerTimer <= 0f)
        {
            flickerTimer = Random.Range(0.05f, 0.35f);
            shopLight.intensity = normalIntensity * Random.Range(0.08f, 0.4f);
        }
    }
}
