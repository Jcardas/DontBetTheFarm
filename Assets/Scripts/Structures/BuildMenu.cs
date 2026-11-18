using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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
    public TMP_Text demolishButtonText;

    [Header("Optional Direct References (Auto-Found if Left Empty)")]
    [SerializeField] private Button buildFieldButton;
    [SerializeField] private Button buildPenButton;
    [SerializeField] private Button demolishButton;

    [Header("Prefabs for buttons")]
    [SerializeField] private Buildable fieldPrefab;
    [SerializeField] private Buildable penPrefab;

    private BuildingManager _buildingManager;
    private int _lastDemolishClickFrame = -1;

    private BuildingManager ResolveBuildingManager()
    {
        if (_buildingManager == null)
            _buildingManager = BuildingManager.Instance != null
                ? BuildingManager.Instance
                : FindAnyObjectByType<BuildingManager>();
        return _buildingManager;
    }

    private void OnEnable()
    {
        ResolveBuildingManager();
        BindButtons();
    }

    void Start()
    {
        if (!Application.isPlaying) return;

        ResolveBuildingManager();
        BindButtons();

        if (buildMenu != null)
            buildMenu.SetActive(false);

        if (buildButtonText != null)
            buildButtonText.text = "+ Build";

        UpdateDemolishButtonLabel(false);
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleBuildMenu();
        }

        var manager = ResolveBuildingManager();
        if (manager != null)
        {
            UpdateDemolishButtonLabel(manager.IsDemolishing);
        }
    }

    private void BindButtons()
    {
        ResolveBuildingManager();

        if (buildButtonText == null)
        {
            Button buildBtn = FindButtonByName("BuildButton");
            if (buildBtn != null)
                buildButtonText = buildBtn.GetComponentInChildren<TMP_Text>(true);
        }

        if (buildFieldButton == null)
            buildFieldButton = FindButtonByName("BuildFieldButton");

        if (buildPenButton == null)
            buildPenButton = FindButtonByName("BuildPenButton");

        if (demolishButton == null)
            demolishButton = FindButtonByName("DemolishButton");

        if (demolishButton != null && demolishButtonText == null)
            demolishButtonText = demolishButton.GetComponentInChildren<TMP_Text>(true);

        if (Application.isPlaying)
        {

            if (buildFieldButton != null)
            {
                buildFieldButton.onClick.RemoveAllListeners();
                buildFieldButton.onClick.AddListener(() =>
                {
                    var manager = ResolveBuildingManager();
                    if (manager != null) manager.StartBuilding(fieldPrefab);
                    CloseBuildMenu();
                    ClearUISelection();
                });
            }

            if (buildPenButton != null)
            {
                buildPenButton.onClick.RemoveAllListeners();
                buildPenButton.onClick.AddListener(() =>
                {
                    var manager = ResolveBuildingManager();
                    if (manager != null) manager.StartBuilding(penPrefab);
                    CloseBuildMenu();
                    ClearUISelection();
                });
            }

            if (demolishButton != null)
            {
                var nav = demolishButton.navigation;
                nav.mode = Navigation.Mode.None;
                demolishButton.navigation = nav;

                demolishButton.onClick.RemoveAllListeners();
                demolishButton.onClick.AddListener(OnDemolishClicked);
            }
        }
#if UNITY_EDITOR
        else
        {
            BindPersistentButton(buildFieldButton, fieldPrefab);
            BindPersistentButton(buildPenButton, penPrefab);

            if (demolishButton != null)
            {
                for (int i = demolishButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    UnityEventTools.RemovePersistentListener(demolishButton.onClick, i);
                }

                UnityEventTools.AddVoidPersistentListener(
                    demolishButton.onClick,
                    OnDemolishClicked
                );

                EditorUtility.SetDirty(demolishButton);
                EditorUtility.SetDirty(this);
            }
        }
#endif
    }

    public void OnDemolishClicked()
    {

        if (Time.frameCount == _lastDemolishClickFrame)
        {
            return;
        }
        _lastDemolishClickFrame = Time.frameCount;

        CloseBuildMenu();
        var manager = ResolveBuildingManager();
        if (manager != null)
        {
            manager.ToggleDemolishMode();
            UpdateDemolishButtonLabel(manager.IsDemolishing);
        }
        else
        {
            Debug.LogError("[BuildMenu] ResolveBuildingManager returned NULL!");
        }
        ClearUISelection();
    }

    private static void ClearUISelection()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

#if UNITY_EDITOR
    private void BindPersistentButton(Button button, Buildable prefab)
    {
        if (button == null || _buildingManager == null || prefab == null) return;

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
        foreach (Button btn in GetComponentsInChildren<Button>(true))
        {
            if (btn.gameObject.name == objectName)
                return btn;
        }
        return null;
    }

    private void UpdateDemolishButtonLabel(bool isDemolishing)
    {
        string targetLabel = isDemolishing ? "× Cancel" : "- Demolish";
        if (demolishButtonText != null)
        {
            demolishButtonText.text = targetLabel;
        }
        else if (isDemolishing)
        {
            Debug.LogWarning("[BuildMenu] demolishButtonText is NULL while IsDemolishing=true!");
        }
    }

    public void ToggleBuildMenu()
    {
        var manager = ResolveBuildingManager();
        if (manager != null && manager.IsDemolishing)
            manager.CancelDemolishMode();

        if (buildMenu == null) return;

        bool isOpen = !buildMenu.activeSelf;
        buildMenu.SetActive(isOpen);

        if (buildButtonText != null)
            buildButtonText.text = isOpen ? "× Close" : "+ Build";

        ClearUISelection();
    }

    public void CloseBuildMenu()
    {
        if (buildMenu != null)
            buildMenu.SetActive(false);

        if (buildButtonText != null)
            buildButtonText.text = "+ Build";
    }
}