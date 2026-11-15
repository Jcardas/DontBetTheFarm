using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
#endif

[ExecuteAlways]
public class BuildMenu : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject buildMenu;
    public TMP_Text buildButtonText;

    [Header("Optional Direct References (Auto-Found if Left Empty)")]
    [SerializeField] private Button buildFieldButton;
    [SerializeField] private Button buildPenButton;

    [Header("Prefabs for buttons")]
    [SerializeField] private Buildable fieldPrefab;
    [SerializeField] private Buildable penPrefab;

    private BuildingManager _buildingManager;

    private void OnEnable()
    {
        _buildingManager = FindAnyObjectByType<BuildingManager>();
        BindButtons();
    }

    void Start()
    {
        if (!Application.isPlaying) return;

        if (_buildingManager == null)
            _buildingManager = FindAnyObjectByType<BuildingManager>();

        BindButtons();

        buildMenu.SetActive(false);
        buildButtonText.text = "+ Build";
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBuildMenu();
        }
    }

    private void BindButtons()
    {
        if (_buildingManager == null)
            _buildingManager = FindAnyObjectByType<BuildingManager>();

        if (_buildingManager == null)
        {
            if (Application.isPlaying)
                Debug.LogWarning("[BuildMenu] No BuildingManager found in the scene.");
            return;
        }

        // Auto-locate buttons by exact Hierarchy name if not manually assigned
        if (buildFieldButton == null)
            buildFieldButton = FindButtonByName("BuildFieldButton");

        if (buildPenButton == null)
            buildPenButton = FindButtonByName("BuildPenButton");

        if (Application.isPlaying)
        {
            // Runtime listeners
            if (buildFieldButton != null)
            {
                buildFieldButton.onClick.RemoveAllListeners();
                buildFieldButton.onClick.AddListener(() =>
                {
                    _buildingManager.StartBuilding(fieldPrefab);
                    CloseBuildMenu();
                });
            }

            if (buildPenButton != null)
            {
                buildPenButton.onClick.RemoveAllListeners();
                buildPenButton.onClick.AddListener(() =>
                {
                    _buildingManager.StartBuilding(penPrefab);
                    CloseBuildMenu();
                });
            }
        }
#if UNITY_EDITOR
        else
        {
            // Editor persistent listeners (visible in the Button Inspector and saved with the scene)
            BindPersistentButton(buildFieldButton, fieldPrefab);
            BindPersistentButton(buildPenButton, penPrefab);
        }
#endif
    }

#if UNITY_EDITOR
    private void BindPersistentButton(Button button, Buildable prefab)
    {
        if (button == null || _buildingManager == null || prefab == null) return;

        // Clear existing persistent listeners so reloading the scene doesn't duplicate entries
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        }

        UnityEventTools.AddObjectPersistentListener(
            button.onClick,
            _buildingManager.StartBuilding,
            prefab
        );

        UnityEventTools.AddVoidPersistentListener(
            button.onClick,
            CloseBuildMenu
        );

        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(this);
    }
#endif

    private Button FindButtonByName(string objectName)
    {
        // Searches even if buildMenu starts inactive
        foreach (Button btn in GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name == objectName)
                return btn;
        }
        return null;
    }

    public void ToggleBuildMenu()
    {
        bool isOpen = !buildMenu.activeSelf;

        buildMenu.SetActive(isOpen);

        buildButtonText.text = isOpen ? "× Close" : "+ Build";
    }

    public void CloseBuildMenu()
    {
        buildMenu.SetActive(false);
        buildButtonText.text = "+ Build";
    }
}