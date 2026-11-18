using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [SerializeField] private float musicVolume = 0.50f;
    [SerializeField] private float sfxVolume = 0.50f;

    [System.Serializable]
    public struct Sound
    {
        public string name;
        public AudioClip clip;
    }

    [SerializeField] private List<Sound> soundLibrary;
    private Dictionary<string, AudioClip> soundDictionary;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        InitalizeSoundLibrary();
    }

    private void InitalizeSoundLibrary()
    {
        soundDictionary = new Dictionary<string, AudioClip>();

        foreach (Sound sound in soundLibrary)
        {
            if (!soundDictionary.ContainsKey((sound.name)))
            {
                soundDictionary.Add(sound.name, sound.clip);
            }
            else
            {
                Debug.LogWarning($"Duplicate sound name detected: {sound.name}. Only the first occurrence will be used.");
            }
        }
    }

    public void PlayMusic(string songName, bool loop = true)
    {
        musicSource.loop = loop;

        if (soundDictionary.TryGetValue(songName, out AudioClip clip))
        {
            sfxSource.PlayOneShot(clip, musicVolume);
        }
        else
        {
            Debug.LogWarning($"Song '{songName}' not found in SoundManager!");
        }
        
        musicSource.Play();
    }

    public void PlaySFX(string soundName)
    { 
        if (soundDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
        else
        {
            Debug.LogWarning($"Sound '{soundName}' not found in SoundManager!");
        }
    }

    public void setMusicVolume(float newVolume)
    {
        if (newVolume > 1 || newVolume < 0)
        {
            Debug.LogWarning($"New SFXVolume must be between 0 and 1");
        }

        musicVolume = newVolume;
        musicSource.volume = newVolume;
    }

    public void setSFXVolume(float newVolume)
    {
        if(newVolume > 1 || newVolume < 0)
        {
            Debug.LogWarning($"New SFXVolume must be between 0 and 1");
        }
        sfxVolume = newVolume;
        sfxSource.volume = newVolume;
    }
}
