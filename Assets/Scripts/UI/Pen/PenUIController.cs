using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PenUIController : MonoBehaviour
{
    [SerializeField] private UIDocument penUIDocument;
    [SerializeField] private CreaturePreviewBooth previewBooth;
    [SerializeField] private VisualTreeAsset mutationIconTemplate;
    [SerializeField] private VisualTreeAsset breedingCardTemplate;
    [SerializeField] private List<Animal> testPenAnimals = new();

    private readonly List<Animal> _animals = new();
    private AnimalPen _activePen;
    private VisualElement _root, _animalsView, _breedingView;
    private Button _animalsTabBtn, _breedingTabBtn;
    private PenAnimalsTab _animalsTab;
    private PenBreedingTab _breedingTab;

    
    private int _lastSyncedCount = -1;
    
    public bool IsOpen { get; private set; }
    public int Capacity => _activePen != null ? _activePen.Capacity : 6;
    public IReadOnlyList<Animal> Animals => _animals;
    public AnimalPen ActivePen => _activePen;
    

    public void OpenMenu() => OpenMenu(_activePen);

    public void OpenMenu(AnimalPen pen)
    {
        _activePen = pen;
        SyncAnimals();
        _lastSyncedCount = _animals.Count;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        IsOpen = true;

        if (_root != null)
        {
            _root.style.display = DisplayStyle.Flex;
            _root.Focus();
            ShowAnimalsTab();
        }
    }

    public void CloseMenu()
    {
        IsOpen = false;
        _animalsTab?.ResetState();
        _breedingTab?.ResetState();
        previewBooth?.ClearLive();
        previewBooth?.ClearCache();
        if (_root != null) _root.style.display = DisplayStyle.None;
    }

    public void SyncAnimals()
    {
        _animals.Clear();
        if (_activePen != null)
            _animals.AddRange(_activePen.AnimalsInPen.Where(a => a != null));
        else
            _animals.AddRange(testPenAnimals.Where(a => a != null));
    }

    private void OnEnable()
    {
        _root = penUIDocument != null ? penUIDocument.rootVisualElement : null;
        if (_root == null) return;

        _root.focusable = true;
        _root.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Escape) { e.StopPropagation(); CloseMenu(); } }, TrickleDown.TrickleDown);

        _animalsView = _root.Q("AnimalsView");
        _breedingView = _root.Q("BreedingView");
        _animalsTabBtn = _root.Q<Button>("AnimalsTabButton");
        _breedingTabBtn = _root.Q<Button>("BreedingTabButton");

        _root.Q<Button>("BackButton").clicked += CloseMenu;
        _animalsTabBtn.clicked += ShowAnimalsTab;
        _breedingTabBtn.clicked += ShowBreedingTab;

        _animalsTab = new PenAnimalsTab(this, _root, previewBooth, mutationIconTemplate);
        _breedingTab = new PenBreedingTab(this, _root, previewBooth, breedingCardTemplate);

        _root.style.display = IsOpen ? DisplayStyle.Flex : DisplayStyle.None;
        if (IsOpen) ShowAnimalsTab();
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseMenu();
            return;
        }

        // 1. If the active pen was destroyed or broken while open, close immediately
        if (_activePen != null && (!_activePen || _activePen.IsBroken || !_activePen.gameObject.activeInHierarchy))
        {
            _activePen = null;
            CloseMenu();
            return;
        }

        // 2. Live-sync animal roster (removes abducted animals immediately while open)
        SyncAnimals();
        if (_animals.Count != _lastSyncedCount)
        {
            _lastSyncedCount = _animals.Count;
            if (_breedingView != null && _breedingView.style.display == DisplayStyle.Flex)
                _breedingTab?.Refresh();
            else
                _animalsTab?.Refresh();
        }
    }

    private void ShowAnimalsTab()
    {
        _animalsView.style.display = DisplayStyle.Flex;
        _breedingView.style.display = DisplayStyle.None;
        _animalsTabBtn.AddToClassList("active");
        _breedingTabBtn.RemoveFromClassList("active");
        _animalsTab.Refresh();
    }

    private void ShowBreedingTab()
    {
        _animalsView.style.display = DisplayStyle.None;
        _breedingView.style.display = DisplayStyle.Flex;
        _breedingTabBtn.AddToClassList("active");
        _animalsTabBtn.RemoveFromClassList("active");
        previewBooth?.ClearLive();
        _breedingTab.Refresh();
    }
}