using UnityEngine;

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
