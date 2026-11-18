using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class OptionsMenuController : MonoBehaviour
{
    public static OptionsMenuController Instance { get; private set; }

    private const string PrefMasterVol = "Opt_MasterVol";
    private const string PrefMusicVol = "Opt_MusicVol";
    private const string PrefSFXVol = "Opt_SFXVol";
    private const string PrefFullscreen = "Opt_Fullscreen";
    private const string PrefVSync = "Opt_VSync";
    private const string PrefCamSpeed = "Opt_CamSpeed";
    private const string PrefEdgeScroll = "Opt_EdgeScroll";

    [Header("Scene Navigation")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Pause gameplay time while open in a gameplay scene.")]
    [SerializeField] private bool pauseGameWhenOpen = true;
    [Tooltip("Allow pressing Escape in gameplay to toggle the Options overlay.")]
    [SerializeField] private bool allowEscapeToggle = true;

    public static float MasterVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 0.8f;
    public static float SFXVolume { get; private set; } = 1f;
    public static float CameraSpeedMultiplier { get; private set; } = 1f;
    public static bool EdgeScrollEnabled { get; private set; } = true;

    public static event Action OnSettingsChanged;

    private UIDocument doc;
    private VisualElement root;

    private Button backButton;
    private Button closeButton;
    private Button resetButton;
    private Button mainMenuButton;

    private Slider masterSlider;
    private Slider musicSlider;
    private Slider sfxSlider;
    private Slider cameraSpeedSlider;

    private Label masterValueLabel;
    private Label musicValueLabel;
    private Label sfxValueLabel;
    private Label cameraSpeedValueLabel;

    private Toggle fullscreenToggle;
    private Toggle vSyncToggle;
    private Toggle edgeScrollToggle;

    private bool isOpen;
    private float previousTimeScale = 1f;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        doc = GetComponent<UIDocument>();
        LoadSavedSettings();
    }

    private void OnEnable()
    {
        if (doc == null || doc.rootVisualElement == null) return;
        root = doc.rootVisualElement;

        backButton = root.Q<Button>("BackButton");
        closeButton = root.Q<Button>("CloseButton");
        resetButton = root.Q<Button>("ResetDefaultsButton");
        mainMenuButton = root.Q<Button>("MainMenuButton");

        masterSlider = root.Q<Slider>("MasterVolumeSlider");
        musicSlider = root.Q<Slider>("MusicVolumeSlider");
        sfxSlider = root.Q<Slider>("SFXVolumeSlider");
        cameraSpeedSlider = root.Q<Slider>("CameraSpeedSlider");

        masterValueLabel = root.Q<Label>("MasterVolumeValue");
        musicValueLabel = root.Q<Label>("MusicVolumeValue");
        sfxValueLabel = root.Q<Label>("SFXVolumeValue");
        cameraSpeedValueLabel = root.Q<Label>("CameraSpeedValue");

        fullscreenToggle = root.Q<Toggle>("FullscreenToggle");
        vSyncToggle = root.Q<Toggle>("VSyncToggle");
        edgeScrollToggle = root.Q<Toggle>("EdgeScrollToggle");

        if (backButton != null) backButton.clicked += CloseMenu;
        if (closeButton != null) closeButton.clicked += CloseMenu;
        if (resetButton != null) resetButton.clicked += ResetToDefaults;
        if (mainMenuButton != null) mainMenuButton.clicked += ReturnToMainMenu;

        if (masterSlider != null) masterSlider.RegisterValueChangedCallback(OnMasterChanged);
        if (musicSlider != null) musicSlider.RegisterValueChangedCallback(OnMusicChanged);
        if (sfxSlider != null) sfxSlider.RegisterValueChangedCallback(OnSFXChanged);
        if (cameraSpeedSlider != null) cameraSpeedSlider.RegisterValueChangedCallback(OnCameraSpeedChanged);

        if (fullscreenToggle != null) fullscreenToggle.RegisterValueChangedCallback(OnFullscreenChanged);
        if (vSyncToggle != null) vSyncToggle.RegisterValueChangedCallback(OnVSyncChanged);
        if (edgeScrollToggle != null) edgeScrollToggle.RegisterValueChangedCallback(OnEdgeScrollChanged);

        SyncUIFromSettings();

        // Start hidden unless already toggled visible
        isOpen = root.style.display == DisplayStyle.Flex;
        if (!isOpen)
        {
            root.style.display = DisplayStyle.None;
        }
    }

    private void OnDisable()
    {
        if (backButton != null) backButton.clicked -= CloseMenu;
        if (closeButton != null) closeButton.clicked -= CloseMenu;
        if (resetButton != null) resetButton.clicked -= ResetToDefaults;
        if (mainMenuButton != null) mainMenuButton.clicked -= ReturnToMainMenu;

        if (masterSlider != null) masterSlider.UnregisterValueChangedCallback(OnMasterChanged);
        if (musicSlider != null) musicSlider.UnregisterValueChangedCallback(OnMusicChanged);
        if (sfxSlider != null) sfxSlider.UnregisterValueChangedCallback(OnSFXChanged);
        if (cameraSpeedSlider != null) cameraSpeedSlider.UnregisterValueChangedCallback(OnCameraSpeedChanged);

        if (fullscreenToggle != null) fullscreenToggle.UnregisterValueChangedCallback(OnFullscreenChanged);
        if (vSyncToggle != null) vSyncToggle.UnregisterValueChangedCallback(OnVSyncChanged);
        if (edgeScrollToggle != null) edgeScrollToggle.UnregisterValueChangedCallback(OnEdgeScrollChanged);
    }

    private void Update()
    {
        if (!allowEscapeToggle || Keyboard.current == null) return;
        if (!Keyboard.current.escapeKey.wasPressedThisFrame) return;

        // Don't hijack Escape on the Main Menu screen itself
        if (SceneManager.GetActiveScene().name == mainMenuSceneName && !isOpen) return;

        if (isOpen) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        if (root == null) return;
        SyncUIFromSettings();

        // Hide "Main Menu" button if we are already on the Main Menu scene
        if (mainMenuButton != null)
        {
            bool onMainMenu = SceneManager.GetActiveScene().name == mainMenuSceneName;
            mainMenuButton.style.display = onMainMenu ? DisplayStyle.None : DisplayStyle.Flex;
        }

        if (!isOpen && pauseGameWhenOpen && SceneManager.GetActiveScene().name != mainMenuSceneName)
        {
            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
        }

        isOpen = true;
        root.style.display = DisplayStyle.Flex;
    }

    public void CloseMenu()
    {
        if (root == null) return;

        if (isOpen && pauseGameWhenOpen && SceneManager.GetActiveScene().name != mainMenuSceneName)
        {
            Time.timeScale = previousTimeScale;
        }

        isOpen = false;
        root.style.display = DisplayStyle.None;
        PlayerPrefs.Save();
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        isOpen = false;
        PlayerPrefs.Save();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ResetToDefaults()
    {
        MasterVolume = 1f;
        MusicVolume = 0.8f;
        SFXVolume = 1f;
        CameraSpeedMultiplier = 1f;
        EdgeScrollEnabled = true;

        Screen.fullScreen = true;
        QualitySettings.vSyncCount = 1;

        SaveAllSettings();
        ApplyAudioSettings();
        SyncUIFromSettings();
        OnSettingsChanged?.Invoke();
    }

    private void LoadSavedSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(PrefMasterVol, 1f);
        MusicVolume = PlayerPrefs.GetFloat(PrefMusicVol, 0.8f);
        SFXVolume = PlayerPrefs.GetFloat(PrefSFXVol, 1f);
        CameraSpeedMultiplier = PlayerPrefs.GetFloat(PrefCamSpeed, 1f);
        EdgeScrollEnabled = PlayerPrefs.GetInt(PrefEdgeScroll, 1) == 1;

        bool fs = PlayerPrefs.GetInt(PrefFullscreen, 1) == 1;
        int vsync = PlayerPrefs.GetInt(PrefVSync, 1);

        Screen.fullScreen = fs;
        QualitySettings.vSyncCount = vsync;
        ApplyAudioSettings();
    }

    private void SaveAllSettings()
    {
        PlayerPrefs.SetFloat(PrefMasterVol, MasterVolume);
        PlayerPrefs.SetFloat(PrefMusicVol, MusicVolume);
        PlayerPrefs.SetFloat(PrefSFXVol, SFXVolume);
        PlayerPrefs.SetFloat(PrefCamSpeed, CameraSpeedMultiplier);
        PlayerPrefs.SetInt(PrefEdgeScroll, EdgeScrollEnabled ? 1 : 0);
        PlayerPrefs.SetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0);
        PlayerPrefs.SetInt(PrefVSync, QualitySettings.vSyncCount);
    }

    private void SyncUIFromSettings()
    {
        if (masterSlider != null) masterSlider.SetValueWithoutNotify(MasterVolume * 100f);
        if (musicSlider != null) musicSlider.SetValueWithoutNotify(MusicVolume * 100f);
        if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(SFXVolume * 100f);
        if (cameraSpeedSlider != null) cameraSpeedSlider.SetValueWithoutNotify(CameraSpeedMultiplier * 100f);

        if (masterValueLabel != null) masterValueLabel.text = $"{Mathf.RoundToInt(MasterVolume * 100f)}%";
        if (musicValueLabel != null) musicValueLabel.text = $"{Mathf.RoundToInt(MusicVolume * 100f)}%";
        if (sfxValueLabel != null) sfxValueLabel.text = $"{Mathf.RoundToInt(SFXVolume * 100f)}%";
        if (cameraSpeedValueLabel != null) cameraSpeedValueLabel.text = $"{Mathf.RoundToInt(CameraSpeedMultiplier * 100f)}%";

        if (fullscreenToggle != null) fullscreenToggle.SetValueWithoutNotify(Screen.fullScreen);
        if (vSyncToggle != null) vSyncToggle.SetValueWithoutNotify(QualitySettings.vSyncCount > 0);
        if (edgeScrollToggle != null) edgeScrollToggle.SetValueWithoutNotify(EdgeScrollEnabled);
    }

    private void OnMasterChanged(ChangeEvent<float> evt)
    {
        MasterVolume = Mathf.Clamp01(evt.newValue / 100f);
        if (masterValueLabel != null) masterValueLabel.text = $"{Mathf.RoundToInt(evt.newValue)}%";
        PlayerPrefs.SetFloat(PrefMasterVol, MasterVolume);
        ApplyAudioSettings();
        OnSettingsChanged?.Invoke();
    }

    private void OnMusicChanged(ChangeEvent<float> evt)
    {
        MusicVolume = Mathf.Clamp01(evt.newValue / 100f);
        if (musicValueLabel != null) musicValueLabel.text = $"{Mathf.RoundToInt(evt.newValue)}%";
        PlayerPrefs.SetFloat(PrefMusicVol, MusicVolume);
        ApplyAudioSettings();
        OnSettingsChanged?.Invoke();
    }

    private void OnSFXChanged(ChangeEvent<float> evt)
    {
        SFXVolume = Mathf.Clamp01(evt.newValue / 100f);
        if (sfxValueLabel != null) sfxValueLabel.text = $"{Mathf.RoundToInt(evt.newValue)}%";
        PlayerPrefs.SetFloat(PrefSFXVol, SFXVolume);
        ApplyAudioSettings();
        OnSettingsChanged?.Invoke();
    }

    private void OnCameraSpeedChanged(ChangeEvent<float> evt)
    {
        CameraSpeedMultiplier = Mathf.Clamp(evt.newValue / 100f, 0.25f, 2f);
        if (cameraSpeedValueLabel != null) cameraSpeedValueLabel.text = $"{Mathf.RoundToInt(evt.newValue)}%";
        PlayerPrefs.SetFloat(PrefCamSpeed, CameraSpeedMultiplier);
        OnSettingsChanged?.Invoke();
    }

    private void OnFullscreenChanged(ChangeEvent<bool> evt)
    {
        Screen.fullScreen = evt.newValue;
        PlayerPrefs.SetInt(PrefFullscreen, evt.newValue ? 1 : 0);
    }

    private void OnVSyncChanged(ChangeEvent<bool> evt)
    {
        QualitySettings.vSyncCount = evt.newValue ? 1 : 0;
        PlayerPrefs.SetInt(PrefVSync, QualitySettings.vSyncCount);
    }

    private void OnEdgeScrollChanged(ChangeEvent<bool> evt)
    {
        EdgeScrollEnabled = evt.newValue;
        PlayerPrefs.SetInt(PrefEdgeScroll, evt.newValue ? 1 : 0);
        OnSettingsChanged?.Invoke();
    }

    private static void ApplyAudioSettings()
    {
        AudioListener.volume = MasterVolume;
    }
}