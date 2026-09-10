using UnityEngine;

/// <summary>에셋 없이 만드는 배경 음악. 짧은 한 마디를 합성해 반복 재생하고,
/// 바쁠수록(주문이 밀릴수록) 빨라져서 압박감을 준다.</summary>
public sealed class GameMusic : MonoBehaviour
{
    private const int SampleRate = 44100;
    private const float BeatSeconds = 0.5f;

    // 단순한 5음계 루프. 8비트 게임 음악처럼 들리게 사각파를 쓴다.
    private static readonly float[] Melody =
    {
        392f, 0f, 523f, 587f, 392f, 0f, 466f, 523f,
        349f, 0f, 523f, 587f, 349f, 0f, 440f, 392f
    };

    private static readonly float[] Bass =
    {
        98f, 98f, 0f, 98f, 131f, 131f, 0f, 131f,
        87f, 87f, 0f, 87f, 110f, 110f, 0f, 110f
    };

    private AudioSource source;

    private void Start()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.clip = BuildLoop();
        source.loop = true;
        source.volume = 0.16f;
        source.spatialBlend = 0f;
        source.Play();
    }

    private void Update()
    {
        if (source == null)
        {
            return;
        }

        // 결산이나 타이틀에서는 조용히, 손님이 밀리면 빠르게.
        bool playing = GameFlow.Instance == null || GameFlow.Instance.IsPlaying;
        source.volume = playing ? 0.16f : 0.07f;

        int waiting = RestaurantGame.Instance != null ? RestaurantGame.Instance.ActiveOrders.Count : 0;
        source.pitch = Mathf.Lerp(source.pitch, 1f + Mathf.Clamp(waiting, 0, 5) * 0.04f, Time.unscaledDeltaTime * 2f);
    }

    private static AudioClip BuildLoop()
    {
        int samplesPerBeat = Mathf.RoundToInt(SampleRate * BeatSeconds);
        int total = samplesPerBeat * Melody.Length;
        float[] samples = new float[total];

        for (int beat = 0; beat < Melody.Length; beat++)
        {
            float melodyHz = Melody[beat];
            float bassHz = Bass[beat % Bass.Length];

            for (int index = 0; index < samplesPerBeat; index++)
            {
                float time = index / (float)SampleRate;
                float envelope = Mathf.Exp(-3.5f * (index / (float)samplesPerBeat));
                float value = 0f;

                if (melodyHz > 0f)
                {
                    value += Square(melodyHz, time) * 0.18f * envelope;
                }

                if (bassHz > 0f)
                {
                    value += Mathf.Sin(2f * Mathf.PI * bassHz * time) * 0.22f * envelope;
                }

                samples[beat * samplesPerBeat + index] = Mathf.Clamp(value, -1f, 1f);
            }
        }

        AudioClip clip = AudioClip.Create("ShopLoop", total, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float Square(float frequency, float time)
    {
        return Mathf.Sign(Mathf.Sin(2f * Mathf.PI * frequency * time));
    }
}
