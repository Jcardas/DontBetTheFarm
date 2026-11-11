using UnityEngine;
using System.Collections.Generic;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

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
        // Ensure that the SoundManager persists across scenes
        DontDestroyOnLoad(gameObject);
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

    public void PlayMusic(AudioClip musicClip, bool loop = true)
    {
        musicSource.clip = musicClip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void PlaySFX(string soundName, float volume = 1f)
    {
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
