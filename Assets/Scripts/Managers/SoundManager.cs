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

    public enum SoundName
    {
        RoosterSFX,
        UiClick,
        UiClose,
        UiOpen,
        UiMutate,
        MutateConfirm,
        UiBreed,
        GameOver,
        NightStart,
        DayStart,
        DayMusic,
        NightMusic,
        BuildingDestroyed,
        EnterBuilding,
        ExitBuilding,
        OrderUnitSuccess,
        OrderUnitFail,
        ActionFail,
        EnemyUfoAbduction,
        FriendlyUfoAbduction,
        CreatureAttack,
        ChopWood,
        HarvestWheat,
        CowAmbient,
        CowSelected,
        LlamaAmbient,
        LlamaSpit,
        LlamaSelected,
        GooseAmbient,
        GooseSelected,
        AlienBeamDown,
        AlienBeamUp,
        UfoHover,
        MothershipHover
    }


    [System.Serializable]
    public struct Sound // TODO: Consider making this a class instead of a struct so we can control the volume of each sound effect individually
    {
        public SoundName name;
        public AudioClip clip;
        
        // Adjust the volume of the sound effect (0.0 to 1.0)
        [Range(0f, 2f)] public float clipVolume;  //TODO: This isnt linked to anything
    }

    [SerializeField] private List<Sound> soundLibrary;
    private Dictionary<SoundName, AudioClip> soundDictionary;


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
        soundDictionary = new Dictionary<SoundName, AudioClip>();

        foreach (Sound sound in soundLibrary)
        {
            if (!soundDictionary.ContainsKey((sound.name)))
            {
                soundDictionary.Add(sound.name, sound.clip);
            }
            else
            {
                Debug.LogWarning($"Duplicate enum name detected: {sound.name}. Only the first occurrence will be used.");
            }
        }
    }

    public void PlayMusic(SoundName songName, bool loop = true)
    {
        if (soundDictionary.TryGetValue(songName, out AudioClip clip))
        {
            musicSource.loop = loop;
            musicSource.clip = clip;
            musicSource.volume = musicVolume;
            musicSource.Play();
        }
        else
        {
            Debug.LogWarning($"Song '{songName}' not found in SoundManager!");
        }
        
        musicSource.Play();
    }

    public void PlaySFX(SoundName soundName)
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

        if (musicSource == null)
        {
            return;
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
