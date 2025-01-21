using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class AudioSfx
{
    public AudioClip clip;
    public float volume = 1f;
    public float pitch = 1f;
    public AudioMixerGroup mixer;
    public void Play(AudioSource source)
    {
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.outputAudioMixerGroup = mixer;

        source.Play();
    }
}
