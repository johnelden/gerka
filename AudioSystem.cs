
using UnityEngine;

public class AudioSystem : MonoBehaviour
{
    AudioSource musicSource;
    AudioSource effectsSource;

    AudioClip musicClip;
    AudioClip hitClip;
    AudioClip deployClip;
    AudioClip victoryClip;

    public void Initialize()
    {
        musicSource = gameObject.AddComponent<AudioSource>();
        effectsSource = gameObject.AddComponent<AudioSource>();

        musicSource.loop = true;
        musicSource.volume = 0.10f;
        effectsSource.playOnAwake = false;
        effectsSource.volume = 0.30f;

        hitClip = MakeTone("Hit", 180f, 0.10f, 0.40f);
        deployClip = MakeTone("Deploy", 420f, 0.16f, 0.30f);
        victoryClip = MakeTone("Victory", 640f, 0.65f, 0.25f);
        musicClip = MakeMusic();

        musicSource.clip = musicClip;
        musicSource.Play();
    }

    AudioClip MakeTone(string clipName, float frequency,
        float duration, float volume)
    {
        const int sampleRate = 22050;
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[count];

        for (int i = 0; i < count; i++)
        {
            float time = i / (float)sampleRate;
            float fade = 1f - i / (float)count;
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time)
                         * volume * fade;
        }

        AudioClip clip = AudioClip.Create(
            clipName, count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    AudioClip MakeMusic()
    {
        const int sampleRate = 22050;
        const float duration = 8f;
        int count = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[count];
        float[] notes = { 110f, 130.81f, 146.83f, 164.81f };

        for (int i = 0; i < count; i++)
        {
            float time = i / (float)sampleRate;
            int index = Mathf.FloorToInt(time / 2f) % notes.Length;
            float note = notes[index];

            float bass = Mathf.Sin(2f * Mathf.PI * note * time);
            float harmony = Mathf.Sin(
                2f * Mathf.PI * note * 1.5f * time) * 0.25f;

            samples[i] = (bass * 0.11f + harmony * 0.05f) * 0.55f;
        }

        AudioClip clip = AudioClip.Create(
            "Fantasy Background Music", count, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void PlayHit()
    {
        if (effectsSource != null && hitClip != null)
            effectsSource.PlayOneShot(hitClip);
    }

    public void PlayDeploy()
    {
        if (effectsSource != null && deployClip != null)
            effectsSource.PlayOneShot(deployClip);
    }

    public void PlayVictory()
    {
        if (effectsSource != null && victoryClip != null)
            effectsSource.PlayOneShot(victoryClip);
    }

    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
            musicSource.volume = Mathf.Clamp01(volume);
    }

    public void SetEffectsVolume(float volume)
    {
        if (effectsSource != null)
            effectsSource.volume = Mathf.Clamp01(volume);
    }
}
