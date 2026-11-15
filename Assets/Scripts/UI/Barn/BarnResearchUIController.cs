using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class BarnResearchUIController : MonoBehaviour
{
    public bool IsMenuOpen => gameObject.activeSelf;
    
    [Header("UI & Sub-Templates")]
    [SerializeField] private UIDocument barnUIDocument;
    [SerializeField] private VisualTreeAsset mutationIconTemplate;          // MutationIcon.uxml
    [SerializeField] private VisualTreeAsset selectedMutationCardTemplate;  // SelectedMutationContainer.uxml

    [Header("3D Preview Stage")]
    [SerializeField] private Transform previewSpawnPoint;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture previewRenderTexture;

    [Header("Captured Aliens in Barn")]
    [SerializeField] private List<Creature> capturedAliens = new();

    [Header("Play Mode Testing")]
    [Tooltip("Optional: Drag an Alien prefab here to populate the Barn UI for testing when no live aliens are stored.")]
    [SerializeField] private Creature testAlienPrefab;
    [Tooltip("Optional: If the test prefab has no pre-equipped mutations in its mutator yet, equip these on the test preview.")]
    [SerializeField] private List<MutationData> testFallbackMutations = new();

    private int _currentAlienIndex;
    private GameObject _previewInstance;
    private int _previewLayer;

    // Mutations currently equipped on the inspected alien (Slot -> MutationData)
    private readonly Dictionary<SlotType, MutationData> _alienEquippedMutations = new();

    // Slots the player has selected to extract/unlock from this alien
    private readonly HashSet<SlotType> _selectedSlotsToUnlock = new();
    private readonly SlotType[] _slotOrder = { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail };

    // Cached UI Elements
    private VisualElement _root;
    private VisualElement _creature3DRender;
    private Label _alienHeaderLabel;
    private Label _aliensInBarnCountLabel;
    private VisualElement _mutationsList;
    private ScrollView _checkoutScrollView;
    private Label _costLabel;
    private Button _cancelButton;
    private Button _unlockButton;

    private void Awake()
    {
        _previewLayer = LayerMask.NameToLayer("UI_Preview");
    }

    private void Update()
    {
        if (previewSpawnPoint != null && _previewInstance != null)
        {
            // Rotate the preview alien slowly for a dynamic effect
            _previewInstance.transform.Rotate(Vector3.up, 15f * Time.deltaTime, Space.World);
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseMenu();
        }
    }

    private void OnRootKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Escape)
        {
            evt.StopPropagation();
            CloseMenu();
        }
    }

    /// <summary>
    /// Opens the Barn Research UI and refreshes the list of captured aliens from CreatureManager.
    /// </summary>
    public void OpenMenu()
    {
        gameObject.SetActive(true);

        // If the GameObject was already active when OpenMenu was called, refresh the UI immediately
        // (Otherwise, OnEnable() will automatically run RefreshMenuContents() when activated)
        if (_root != null)
        {
            RefreshMenuContents();
        }
    }

    private void OnEnable()
    {
        if (barnUIDocument == null)
        {
            Debug.LogError("[BarnResearchUIController] barnUIDocument is not assigned!");
            return;
        }

        _root = barnUIDocument.rootVisualElement;
        if (_root == null) return;

        _root.focusable = true;
        _root.Focus();
        _root.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);

        // 1. Bind RenderTexture to #Creature3DRender
        _creature3DRender = _root.Q<VisualElement>("Creature3DRender");
        if (_creature3DRender != null)
        {
            if (previewRenderTexture != null)
            {
                _creature3DRender.style.backgroundImage =
                    new StyleBackground(Background.FromRenderTexture(previewRenderTexture));
            }
            SetupViewportOrbit(_creature3DRender);
        }

        // 2. Query UI Elements
        _alienHeaderLabel = _root.Q<Label>("AlienHeader");
        _aliensInBarnCountLabel = _root.Q<Label>("AliensInBarnCount");
        _mutationsList = _root.Q<VisualElement>("MutationsList");
        _checkoutScrollView = _root.Q<ScrollView>("CheckoutScrollView");
        _costLabel = _root.Q<Label>("Cost");

        _root.Q<Button>("BackButton")?.RegisterCallback<ClickEvent>(OnBackButtonClicked);
        _root.Q<Button>("Back")?.RegisterCallback<ClickEvent>(OnPrevAlienClicked);
        _root.Q<Button>("Forward")?.RegisterCallback<ClickEvent>(OnNextAlienClicked);

        var checkoutContainer = _root.Q<VisualElement>("MutationCheckoutContainer");
        if (checkoutContainer != null)
        {
            _cancelButton = checkoutContainer.Q<Button>("CancelButton");
            _unlockButton = checkoutContainer.Q<Button>("UnlockButton");

            _cancelButton?.RegisterCallback<ClickEvent>(OnCancelButtonClicked);
            _unlockButton?.RegisterCallback<ClickEvent>(OnUnlockButtonClicked);
        }

        // 3. Populate initial alien or show empty barn state
        RefreshMenuContents();
    }

    private void OnDisable()
    {
        if (_root != null)
        {
            _root.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            _root.Q<Button>("BackButton")?.UnregisterCallback<ClickEvent>(OnBackButtonClicked);
            _root.Q<Button>("Back")?.UnregisterCallback<ClickEvent>(OnPrevAlienClicked);
            _root.Q<Button>("Forward")?.UnregisterCallback<ClickEvent>(OnNextAlienClicked);
        }

        _cancelButton?.UnregisterCallback<ClickEvent>(OnCancelButtonClicked);
        _unlockButton?.UnregisterCallback<ClickEvent>(OnUnlockButtonClicked);

        if (previewCamera != null)
            previewCamera.enabled = false;
    }

    private void RefreshMenuContents()
    {
        // Pull the latest captured aliens from CreatureManager if available
        if (CreatureManager.Instance != null)
        {
            capturedAliens = CreatureManager.Instance.GetCapturedAliens();
        }

        // Clean out any destroyed/null alien references
        capturedAliens.RemoveAll(a => a == null);

        // Inject Test Alien Prefab if assigned and list is empty
        if (capturedAliens.Count == 0 && testAlienPrefab != null)
        {
            capturedAliens.Add(testAlienPrefab);
        }

        if (capturedAliens.Count > 0)
        {
            _currentAlienIndex = Mathf.Clamp(_currentAlienIndex, 0, capturedAliens.Count - 1);
            LoadAlienPreview(_currentAlienIndex);
        }
        else
        {
            ShowEmptyBarnState();
        }
    }

    private void OnBackButtonClicked(ClickEvent evt) => CloseMenu();
    private void OnPrevAlienClicked(ClickEvent evt) => CycleAlien(-1);
    private void OnNextAlienClicked(ClickEvent evt) => CycleAlien(1);
    private void OnCancelButtonClicked(ClickEvent evt) => ClearSelectedMutations();
    private void OnUnlockButtonClicked(ClickEvent evt) => ConfirmUnlock();

    private void CycleAlien(int direction)
    {
        if (capturedAliens.Count == 0) return;
        _currentAlienIndex = (_currentAlienIndex + direction + capturedAliens.Count) % capturedAliens.Count;
        LoadAlienPreview(_currentAlienIndex);
    }

    public void LoadAlienPreview(int index)
    {
        _currentAlienIndex = index;
        _selectedSlotsToUnlock.Clear();
        _alienEquippedMutations.Clear();

        Creature alien = capturedAliens[_currentAlienIndex];
        if (_alienHeaderLabel != null) _alienHeaderLabel.text = alien.name;
        if (_aliensInBarnCountLabel != null) _aliensInBarnCountLabel.text = $"{_currentAlienIndex + 1}/{capturedAliens.Count}";

        ReadAlienMutations(alien);
        PopulateAlienMutationIcons();
        RebuildPreviewInstance();
        RefreshCheckoutScrollView();
    }

    private void ReadAlienMutations(Creature alien)
    {
        if (alien.TryGetComponent<CreatureMutator>(out var mutator))
        {
            foreach (SlotType slot in _slotOrder)
            {
                MutationData equipped = mutator.GetEquippedMutation(slot);
                if (equipped != null)
                {
                    _alienEquippedMutations[slot] = equipped;
                }
            }
        }

        // If testing a raw prefab whose CreatureMutator hasn't run Start() yet, load testFallbackMutations
        if (_alienEquippedMutations.Count == 0 && alien == testAlienPrefab && testFallbackMutations.Count > 0)
        {
            foreach (var mutation in testFallbackMutations)
            {
                if (mutation == null) continue;
                foreach (SlotType slot in _slotOrder)
                {
                    if ((mutation.allowedSlots & slot) != 0 && !_alienEquippedMutations.ContainsKey(slot))
                    {
                        _alienEquippedMutations[slot] = mutation;
                        break;
                    }
                }
            }
        }
    }

    private void PopulateAlienMutationIcons()
    {
        if (_mutationsList == null) return;

        var slotContainers = _mutationsList.Query<TemplateContainer>().ToList();

        for (int i = 0; i < _slotOrder.Length; i++)
        {
            if (i >= slotContainers.Count) break;

            SlotType slot = _slotOrder[i];
            TemplateContainer container = slotContainers[i];

            container.UnregisterCallback<ClickEvent>(OnSlotContainerClicked);
            container.userData = slot;

            Image imageElement = container.Q<Image>();
            VisualElement bgTarget = container.Q<VisualElement>("Icon")
                                  ?? container.Q<VisualElement>("MutationImage")
                                  ?? container.Query<VisualElement>().Last();

            bool hasMutation = _alienEquippedMutations.TryGetValue(slot, out var mutation);

            Sprite displaySprite = hasMutation && mutation.prefabSprite != null
                ? mutation.prefabSprite
                : GameConstants.Instance.DefaultSprite;

            if (imageElement != null)
            {
                imageElement.sprite = displaySprite;
                imageElement.style.backgroundImage = StyleKeyword.None;
            }
            else if (bgTarget != null)
            {
                bgTarget.style.backgroundImage = new StyleBackground(displaySprite);
            }

            if (!hasMutation)
            {
                SetSlotIconVisualState(container, imageElement, bgTarget, enabled: false, selected: false, lockedOut: false);
                container.tooltip = $"Empty ({slot})";
                continue;
            }

            bool isAlreadyUnlocked = MutationUnlockManager.Instance != null && MutationUnlockManager.Instance.IsUnlocked(mutation);
            bool isSelectedInThisSlot = _selectedSlotsToUnlock.Contains(slot);
            bool isDuplicateSelectedElsewhere = !isSelectedInThisSlot && IsMutationStagedInAnySlot(mutation);

            if (isAlreadyUnlocked)
            {
                SetSlotIconVisualState(container, imageElement, bgTarget, enabled: false, selected: false, lockedOut: true);
                container.tooltip = $"{mutation.mutationName} — Already Unlocked";
            }
            else if (isDuplicateSelectedElsewhere)
            {
                SetSlotIconVisualState(container, imageElement, bgTarget, enabled: false, selected: false, lockedOut: true);
                container.tooltip = $"{mutation.mutationName} — Already Selected";
            }
            else
            {
                SetSlotIconVisualState(container, imageElement, bgTarget, enabled: true, selected: isSelectedInThisSlot, lockedOut: false);
                container.tooltip = $"{mutation.mutationName} ({slot}) — Cost: {mutation.mutationCost}";
                container.RegisterCallback<ClickEvent>(OnSlotContainerClicked);
            }
        }
    }

    private void SetSlotIconVisualState(
        TemplateContainer container,
        Image imageElement,
        VisualElement bgTarget,
        bool enabled,
        bool selected,
        bool lockedOut)
    {
        container.SetEnabled(enabled);

        if (lockedOut)
        {
            container.style.opacity = 0.35f;
            if (imageElement != null) imageElement.tintColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            if (bgTarget != null) bgTarget.style.unityBackgroundImageTintColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        }
        else if (!enabled)
        {
            container.style.opacity = 0.4f;
            if (imageElement != null) imageElement.tintColor = Color.white;
            if (bgTarget != null) bgTarget.style.unityBackgroundImageTintColor = Color.white;
        }
        else
        {
            container.style.opacity = 1f;
            if (imageElement != null) imageElement.tintColor = Color.white;
            if (bgTarget != null) bgTarget.style.unityBackgroundImageTintColor = Color.white;
        }

        float borderWidth = selected ? 3f : 0f;
        container.style.borderBottomWidth = borderWidth;
        container.style.borderTopWidth = borderWidth;
        container.style.borderLeftWidth = borderWidth;
        container.style.borderRightWidth = borderWidth;
        container.style.borderBottomColor = Color.white;
        container.style.borderTopColor = Color.white;
        container.style.borderLeftColor = Color.white;
        container.style.borderRightColor = Color.white;
    }

    private bool IsMutationStagedInAnySlot(MutationData mutation)
    {
        foreach (SlotType slot in _selectedSlotsToUnlock)
        {
            if (_alienEquippedMutations.TryGetValue(slot, out var staged) && staged == mutation)
                return true;
        }
        return false;
    }

    private void OnSlotContainerClicked(ClickEvent evt)
    {
        if (evt.currentTarget is VisualElement el && el.userData is SlotType slot)
        {
            ToggleSelectSlotForUnlock(slot);
        }
    }

    private void ToggleSelectSlotForUnlock(SlotType slot)
    {
        if (!_alienEquippedMutations.TryGetValue(slot, out var mutation)) return;

        // Block selecting if already unlocked globally or if an identical mutation is already staged
        if (!_selectedSlotsToUnlock.Contains(slot))
        {
            if (MutationUnlockManager.Instance != null && MutationUnlockManager.Instance.IsUnlocked(mutation))
                return;

            if (IsMutationStagedInAnySlot(mutation))
                return;

            _selectedSlotsToUnlock.Add(slot);
        }
        else
        {
            _selectedSlotsToUnlock.Remove(slot);
        }

        PopulateAlienMutationIcons();
        RebuildPreviewInstance();
        RefreshCheckoutScrollView();
    }

    private void RebuildPreviewInstance()
    {
        if (capturedAliens.Count == 0) return;

        if (_previewInstance != null)
            Destroy(_previewInstance);

        Creature alien = capturedAliens[_currentAlienIndex];

        // Temporarily deactivate spawn point so Instantiate doesn't trigger Awake/OnEnable before lobotomizing
        bool wasSpawnActive = previewSpawnPoint.gameObject.activeSelf;
        previewSpawnPoint.gameObject.SetActive(false);

        _previewInstance = Instantiate(
            alien.gameObject,
            previewSpawnPoint.position,
            previewSpawnPoint.rotation,
            previewSpawnPoint
        );

        SanitizePreviewClone(_previewInstance);

        previewSpawnPoint.gameObject.SetActive(wasSpawnActive);

        if (_previewInstance.TryGetComponent<CreatureMutator>(out var previewMutator))
        {
            foreach (var kvp in _alienEquippedMutations)
            {
                if (!_selectedSlotsToUnlock.Contains(kvp.Key))
                    previewMutator.EquipMutation(kvp.Value, kvp.Key);
                else
                    previewMutator.UnequipMutation(kvp.Key);
            }
        }

        SetLayerRecursively(_previewInstance, _previewLayer);
        if (previewCamera != null) previewCamera.enabled = true;
    }

    private void RefreshCheckoutScrollView()
    {
        if (_checkoutScrollView == null) return;
        _checkoutScrollView.Clear();

        int totalCost = 0;

        foreach (SlotType slot in _slotOrder)
        {
            if (!_selectedSlotsToUnlock.Contains(slot)) continue;
            if (!_alienEquippedMutations.TryGetValue(slot, out var mutation)) continue;

            totalCost += mutation.mutationCost;

            if (selectedMutationCardTemplate == null) continue;

            VisualElement card = selectedMutationCardTemplate.Instantiate();

            // 1. Icon
            Sprite sprite = mutation.prefabSprite != null
                ? mutation.prefabSprite
                : GameConstants.Instance.DefaultSprite;

            Image cardImage = card.Q<Image>();
            if (cardImage != null)
            {
                cardImage.sprite = sprite;
                cardImage.style.backgroundImage = StyleKeyword.None;
            }
            else
            {
                VisualElement iconBox = card.Q<VisualElement>("Icon") ?? card.Q<VisualElement>("PartIcon");
                if (iconBox != null)
                    iconBox.style.backgroundImage = new StyleBackground(sprite);
            }

            // 2. Labels
            var labels = card.Query<Label>().ToList();
            string formattedSlots = FormatAllowedSlots(mutation.allowedSlots);
            string effectSummary = mutation.GetEffectSummary();

            if (labels.Count >= 5)
            {
                labels[0].text = mutation.mutationName;
                labels[2].text = formattedSlots;
                labels[4].text = effectSummary;

                if (labels.Count >= 6)
                    labels[5].text = mutation.mutationCost.ToString();
            }
            else
            {
                if (labels.Count > 0) labels[0].text = mutation.mutationName;
                if (labels.Count > 1) labels[1].text = $"Slots: {formattedSlots}";
                if (labels.Count > 2) labels[2].text = $"Effect: {effectSummary}";
            }

            SlotType capturedSlot = slot;
            card.RegisterCallback<ClickEvent>(_ => ToggleSelectSlotForUnlock(capturedSlot));

            _checkoutScrollView.Add(card);
        }

        bool hasSelection = _selectedSlotsToUnlock.Count > 0;
        bool canAfford = CanAffordTechnology(totalCost);

        if (_costLabel != null)
        {
            _costLabel.text = totalCost.ToString();
            _costLabel.style.color = (!hasSelection || canAfford)
                ? new Color(0.65f, 0.35f, 1f, 1f) // Purple
                : new Color(1f, 0.35f, 0.35f, 1f);
        }

        _cancelButton?.SetEnabled(hasSelection);
        _unlockButton?.SetEnabled(hasSelection && canAfford);
    }

    public void ClearSelectedMutations()
    {
        if (_selectedSlotsToUnlock.Count == 0) return;

        _selectedSlotsToUnlock.Clear();
        PopulateAlienMutationIcons();
        RebuildPreviewInstance();
        RefreshCheckoutScrollView();
    }

    private int CalculateSelectedCost()
    {
        int totalCost = 0;
        foreach (SlotType slot in _selectedSlotsToUnlock)
        {
            if (_alienEquippedMutations.TryGetValue(slot, out var mutation))
                totalCost += mutation.mutationCost;
        }
        return totalCost;
    }

    private bool CanAffordTechnology(int cost)
    {
        if (cost <= 0) return true;
        if (ResourceManager.Instance == null) return false;

        return ResourceManager.GetResourceAmount(ResourceType.Technology) >= cost;
    }

    private void SpendTechnology(int cost)
    {
        if (cost <= 0 || ResourceManager.Instance == null) return;

        ResourceManager.Spend(ResourceType.Technology, cost);
    }

    private void ConfirmUnlock()
    {
        if (capturedAliens.Count == 0 || _selectedSlotsToUnlock.Count == 0) return;

        int totalCost = CalculateSelectedCost();
        if (!CanAffordTechnology(totalCost))
        {
            Debug.LogWarning($"[BarnResearch] Not enough Technology! Needed {totalCost}.");
            return;
        }

        SpendTechnology(totalCost);

        Creature liveAlien = capturedAliens[_currentAlienIndex];
        bool isPrefabAsset = !liveAlien.gameObject.scene.IsValid();
        liveAlien.TryGetComponent<CreatureMutator>(out var liveMutator);

        foreach (SlotType slot in _selectedSlotsToUnlock)
        {
            if (!_alienEquippedMutations.TryGetValue(slot, out var mutation)) continue;

            if (MutationUnlockManager.Instance != null)
                MutationUnlockManager.Instance.UnlockMutation(mutation);

            if (!isPrefabAsset && liveMutator != null)
                liveMutator.UnequipMutation(slot);

            _alienEquippedMutations.Remove(slot);
        }

        _selectedSlotsToUnlock.Clear();

        // Check if all parts have now been stripped off this alien
        if (_alienEquippedMutations.Count == 0)
        {
            capturedAliens.RemoveAt(_currentAlienIndex);

            // Unregister from CreatureManager and destroy the live scene GameObject
            if (!isPrefabAsset && liveAlien != null)
            {
                if (CreatureManager.Instance != null)
                {
                    CreatureManager.Instance.UnregisterCapturedAlien(liveAlien);
                    CreatureManager.Instance.Unregister(liveAlien);
                }
                Destroy(liveAlien.gameObject);
            }

            if (capturedAliens.Count > 0)
            {
                _currentAlienIndex = Mathf.Clamp(_currentAlienIndex, 0, capturedAliens.Count - 1);
                LoadAlienPreview(_currentAlienIndex);
            }
            else
            {
                ShowEmptyBarnState();
            }
            return;
        }

        PopulateAlienMutationIcons();
        RebuildPreviewInstance();
        RefreshCheckoutScrollView();
    }

    private void ShowEmptyBarnState()
    {
        if (_previewInstance != null)
            Destroy(_previewInstance);

        if (previewCamera != null)
            previewCamera.enabled = false;

        _alienEquippedMutations.Clear();
        _selectedSlotsToUnlock.Clear();

        if (_alienHeaderLabel != null) _alienHeaderLabel.text = "No Aliens in Barn";
        if (_aliensInBarnCountLabel != null) _aliensInBarnCountLabel.text = "0/0";

        PopulateAlienMutationIcons();
        RefreshCheckoutScrollView();
    }

    public void CloseMenu()
    {
        if (previewCamera != null) previewCamera.enabled = false;
        if (_previewInstance != null) Destroy(_previewInstance);
        _selectedSlotsToUnlock.Clear();
        gameObject.SetActive(false);
    }

    private void SetupViewportOrbit(VisualElement viewport)
    {
        bool isDragging = false;
        viewport.RegisterCallback<PointerDownEvent>(_ => isDragging = true);
        viewport.RegisterCallback<PointerUpEvent>(_ => isDragging = false);
        viewport.RegisterCallback<PointerLeaveEvent>(_ => isDragging = false);
        viewport.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (isDragging && previewSpawnPoint != null)
                previewSpawnPoint.Rotate(Vector3.up, -evt.deltaPosition.x * 0.6f, Space.World);
        });
    }

    private string FormatAllowedSlots(SlotType flags)
    {
        if (flags == SlotType.None) return "None";

        List<string> names = new();
        foreach (SlotType slot in _slotOrder)
        {
            if ((flags & slot) != 0)
                names.Add(slot.ToString());
        }
        return names.Count > 0 ? string.Join(", ", names) : flags.ToString();
    }

    private static string FormatEnumName(string rawName)
    {
        // Inserts spaces before capital letters (e.g. "CanFly" -> "Can Fly")
        return System.Text.RegularExpressions.Regex.Replace(rawName, "(\\B[A-Z])", " $1");
    }

    private void SanitizePreviewClone(GameObject clone)
    {
        foreach (var brain in clone.GetComponentsInChildren<CreatureBrain>(true))
            brain.enabled = false;

        foreach (var agent in clone.GetComponentsInChildren<NavMeshAgent>(true))
            agent.enabled = false;

        foreach (var creature in clone.GetComponentsInChildren<Creature>(true))
            creature.enabled = false;

        foreach (var selectable in clone.GetComponentsInChildren<Selectable>(true))
            selectable.enabled = false;

        foreach (var bob in clone.GetComponentsInChildren<ToyBob>(true))
            bob.enabled = false;

        foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
            rb.isKinematic = true;

        foreach (var outline in clone.GetComponentsInChildren<Outline>(true))
            outline.enabled = false;
        
        foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (layer < 0) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}