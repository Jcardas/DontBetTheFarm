using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class MainMenuController : MonoBehaviour
{
    [Header("Scene Navigation")]
    [Tooltip("Name of the main gameplay scene (must be added to Build Profiles / Build Settings).")]
    [SerializeField] private string playButtonSceneToRedirectTo = "Farm";

    [Header("Optional Overlay Panels")]
    [SerializeField] private UIDocument optionsModalDocument;

    [Header("3D Animal Preview Turntable (Optional)")]
    [Tooltip("Root Transform of the creature displayed by MenuPreviewRenderTexture.")]
    [SerializeField] private Transform previewCreatureRoot;
    [SerializeField] private float turntableSpeed = 20f;

    private UIDocument menuDocument;
    private Button loadGameButton;
    private Button optionsButton;
    private Button exitButton;

    public event Action OnOptionsRequested;
    public event Action OnCreditsRequested;

    private void Awake()
    {
        menuDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        Time.timeScale = 1f;

        if (menuDocument == null || menuDocument.rootVisualElement == null)
        {
            Debug.LogWarning("[MainMenuController] Missing UIDocument or rootVisualElement.");
            return;
        }

        VisualElement root = menuDocument.rootVisualElement;

        loadGameButton = root.Q<Button>("LoadGameButton");
        optionsButton = root.Q<Button>("OptionsButton");
        exitButton = root.Q<Button>("ExitButton");

        if (loadGameButton != null) loadGameButton.clicked += HandlePlayClicked;
        if (optionsButton != null) optionsButton.clicked += HandleOptionsClicked;
        if (exitButton != null) exitButton.clicked += HandleExitClicked;

        SetOverlayVisible(optionsModalDocument, false);
    }

    private void OnDisable()
    {
        if (loadGameButton != null) loadGameButton.clicked -= HandlePlayClicked;
        if (optionsButton != null) optionsButton.clicked -= HandleOptionsClicked;
        if (exitButton != null) exitButton.clicked -= HandleExitClicked;
    }

    private void Update()
    {
        if (previewCreatureRoot != null)
        {
            previewCreatureRoot.Rotate(Vector3.up, turntableSpeed * Time.unscaledDeltaTime, Space.World);
        }
    }

    private void HandlePlayClicked()
    {
        if (string.IsNullOrWhiteSpace(playButtonSceneToRedirectTo))
        {
            Debug.LogError("[MainMenuController] Gameplay Scene Name is empty!");
            return;
        }

        SceneManager.LoadScene(playButtonSceneToRedirectTo);
    }

    private void HandleOptionsClicked()
    {
        ToggleOverlay(optionsModalDocument);
        OnOptionsRequested?.Invoke();
    }
    

    private void HandleExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void ToggleOverlay(UIDocument doc)
    {
        if (doc == null || doc.rootVisualElement == null) return;
        bool isCurrentlyVisible = doc.rootVisualElement.style.display != DisplayStyle.None;
        doc.rootVisualElement.style.display = isCurrentlyVisible ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private static void SetOverlayVisible(UIDocument doc, bool visible)
    {
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}