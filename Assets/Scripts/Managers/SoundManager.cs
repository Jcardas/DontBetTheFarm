using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [SerializeField] private int musicVolume = 50;
    [SerializeField] private int sfxVolume = 50;


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
            if (!soundDictionary.ContainsKey(sound.name))
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
        musicSource.volume = ((float)musicVolume) / 100;
        musicSource.loop = loop;

        if (soundDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            sfxSource.PlayOneShot(clip, volume);
        }
        else
        {
            Debug.LogWarning($"Sound '{soundName}' not found in SoundManager!");
        }
        
        musicSource.Play();
    }

    public void PlaySFX(string soundName, float volume = 1f)
    {
        sfxSource.volume = ((float)sfxVolume) / 100;

        if (soundDictionary.TryGetValue(soundName, out AudioClip clip))
        {
            sfxSource.PlayOneShot(clip, volume);
        }
        else
        {
            Debug.LogWarning($"Sound '{soundName}' not found in SoundManager!");
        }
    }
}
