using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum SfxType
{
    Move = 0
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private AudioSource m_AudioSource;
    public AudioSfx_SO[] sfxes;
    private void Awake()
    {
        Instance = this;
        m_AudioSource = GetComponent<AudioSource>();
    }

    public void PlaySfx(SfxType type)
    {
        sfxes[(int)type].sfx.Play(m_AudioSource);
    }
}
