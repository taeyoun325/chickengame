using UnityEngine;

public enum GameSound
{
    OrderIn,
    Cash,
    FryStart,
    Burnt,
    Fire,
    Slip,
    Purchase,
    Delivery,
    Fail,
    Footstep,
    Pack
}

/// <summary>사운드 에셋 없이 파형을 직접 만들어 쓰는 간이 효과음 뱅크.</summary>
public sealed class GameAudio : MonoBehaviour
{
    private const int SampleRate = 44100;

    public static GameAudio Instance { get; private set; }

    private AudioSource source;
    private AudioClip orderIn;
    private AudioClip cash;
    private AudioClip fryStart;
    private AudioClip burnt;
    private AudioClip fire;
    private AudioClip slip;
    private AudioClip purchase;
    private AudioClip delivery;
    private AudioClip fail;
    private AudioClip footstep;
    private AudioClip pack;

    private void Awake()
    {
        Instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0.5f;

        orderIn = Tone("OrderIn", 0.16f, time => Sine(880f, time) * Decay(time, 0.16f, 6f));
        cash = Tone("Cash", 0.36f, time => (Sine(1046f, time) * 0.6f + Sine(1568f, time) * 0.4f) * Decay(time, 0.36f, 4f));
        fryStart = Tone("FryStart", 0.55f, time => Noise() * Mathf.Min(1f, time * 8f) * Decay(time, 0.55f, 2.2f) * 0.6f);
        burnt = Tone("Burnt", 0.45f, time => Square(320f - time * 200f, time) * Decay(time, 0.45f, 3f) * 0.5f);
        fire = Tone("Fire", 0.8f, time => (Noise() * 0.5f + Square(180f, time) * 0.5f) * Decay(time, 0.8f, 1.5f) * 0.6f);
        slip = Tone("Slip", 0.32f, time => Sine(420f - time * 700f, time) * Decay(time, 0.32f, 5f));
        purchase = Tone("Purchase", 0.32f, time => Sine(660f + time * 900f, time) * Decay(time, 0.32f, 4f) * 0.7f);
        delivery = Tone("Delivery", 0.4f, time => Sine(520f + Mathf.Sin(time * 30f) * 60f, time) * Decay(time, 0.4f, 3.5f) * 0.6f);
        fail = Tone("Fail", 0.5f, time => Square(220f - time * 180f, time) * Decay(time, 0.5f, 3f) * 0.45f);

        // 발소리는 짧고 둔해야 한다. 길거나 맑으면 걸을 때마다 귀에 걸린다.
        footstep = Tone("Footstep", 0.11f, time => (Noise() * 0.5f + Sine(110f, time) * 0.5f) * Decay(time, 0.11f, 9f));

        // 상자를 닫는 소리. 포장은 조리에서 판매로 넘어가는 지점인데 소리가 없었다.
        pack = Tone("Pack", 0.18f, time => (Noise() * 0.7f + Square(140f, time) * 0.3f) * Decay(time, 0.18f, 7f) * 0.5f);
    }

    public void Play(GameSound sound, float volume = 1f)
    {
        AudioClip clip = Resolve(sound);
        if (clip != null && source != null)
        {
            source.PlayOneShot(clip, volume);
        }
    }

    private AudioClip Resolve(GameSound sound)
    {
        return sound switch
        {
            GameSound.OrderIn => orderIn,
            GameSound.Cash => cash,
            GameSound.FryStart => fryStart,
            GameSound.Burnt => burnt,
            GameSound.Fire => fire,
            GameSound.Slip => slip,
            GameSound.Purchase => purchase,
            GameSound.Delivery => delivery,
            GameSound.Fail => fail,
            GameSound.Footstep => footstep,
            GameSound.Pack => pack,
            _ => null
        };
    }

    private static AudioClip Tone(string name, float duration, System.Func<float, float> shape)
    {
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int index = 0; index < sampleCount; index++)
        {
            samples[index] = Mathf.Clamp(shape(index / (float)SampleRate), -1f, 1f);
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float Sine(float frequency, float time)
    {
        return Mathf.Sin(2f * Mathf.PI * frequency * time);
    }

    private static float Square(float frequency, float time)
    {
        return Mathf.Sign(Sine(frequency, time));
    }

    private static float Noise()
    {
        return Random.Range(-1f, 1f);
    }

    /// <summary>지수 감쇠. 소리가 뚝 끊기지 않게 한다.</summary>
    private static float Decay(float time, float duration, float rate)
    {
        float normalized = Mathf.Clamp01(time / duration);
        return Mathf.Exp(-rate * normalized) * (1f - normalized);
    }
}
