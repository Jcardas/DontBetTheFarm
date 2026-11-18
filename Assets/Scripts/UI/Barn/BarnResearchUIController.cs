using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class BarnResearchUIController : MonoBehaviour
{
    private const float DragThreshold = 8f;

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
    private readonly Dictionary<SlotType, VisualElement> _slotContainers = new();

    // Drag-and-drop state (matches PenAnimalsTab)
    private Image _dragGhost;
    private MutationData _draggingMutation;
    private SlotType? _dragSourceSlot;
    private bool _draggingFromCheckout;
    private Vector2 _dragStartPos;
    private bool _isDragging;

    // Cached UI Elements
    private VisualElement _root;
    private VisualElement _creaturePreviewContainer;
    private VisualElement _creature3DRender;
    private Label _alienHeaderLabel;
    private Label _aliensInBarnCountLabel;
    private Label _selectMutationsHeaderLabel;
    private VisualElement _mutationsList;
    private VisualElement _contentPane;
    private VisualElement _checkoutContainer;
    private ScrollView _checkoutScrollView;
    private Label _costLabel;
    private Button _prevAlienButton;
    private Button _nextAlienButton;
    private Button _cancelButton;
    private Button _unlockButton;

    private void Awake()
    {
        _previewLayer = LayerMask.NameToLayer("UI_Preview");
        CloseMenu();
    }

    private void Update()
    {
        if (previewSpawnPoint != null && _previewInstance != null)
        {
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

        if (barnUIDocument != null && barnUIDocument.rootVisualElement != null)
            barnUIDocument.rootVisualElement.style.display = DisplayStyle.Flex;

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
        _creaturePreviewContainer = _root.Q<VisualElement>("CreaturePreview");
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
        _contentPane = _root.Q<VisualElement>("ContentPane");
        _checkoutScrollView = _root.Q<ScrollView>("CheckoutScrollView");
        _costLabel = _root.Q<Label>("Cost");
        _prevAlienButton = _root.Q<Button>("Back");
        _nextAlienButton = _root.Q<Button>("Forward");

        _selectMutationsHeaderLabel = _root.Q<Label>("MutationSelectionHeader") ?? _root.Q<Label>("SelectMutationsHeader");
        if (_selectMutationsHeaderLabel == null && _mutationsList?.parent != null)
        {
            foreach (var lbl in _mutationsList.parent.Query<Label>().ToList())
            {
                if (lbl != _alienHeaderLabel && lbl != _aliensInBarnCountLabel)
                {
                    _selectMutationsHeaderLabel = lbl;
                    break;
                }
            }
        }

        _root.Q<Button>("BackButton")?.RegisterCallback<ClickEvent>(OnBackButtonClicked);
        _prevAlienButton?.RegisterCallback<ClickEvent>(OnPrevAlienClicked);
        _nextAlienButton?.RegisterCallback<ClickEvent>(OnNextAlienClicked);

        _checkoutContainer = _root.Q<VisualElement>("MutationCheckoutContainer");
        if (_checkoutContainer != null)
        {
            _cancelButton = _checkoutContainer.Q<Button>("CancelButton");
            _unlockButton = _checkoutContainer.Q<Button>("UnlockButton");

            _cancelButton?.RegisterCallback<ClickEvent>(OnCancelButtonClicked);
            _unlockButton?.RegisterCallback<ClickEvent>(OnUnlockButtonClicked);
        }

        // 3. Create floating drag ghost (matching PenAnimalsTab)
        EnsureDragGhost();

        // 4. Bind slot containers and register drag-and-drop handlers once
        BindSlotContainers();

        // 5. Populate initial alien or show empty barn state
        RefreshMenuContents();
    }

    private void OnDisable()
    {
        CancelDrag();

        if (_root != null)
        {
            _root.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
            _root.Q<Button>("BackButton")?.UnregisterCallback<ClickEvent>(OnBackButtonClicked);
        }

        _prevAlienButton?.UnregisterCallback<ClickEvent>(OnPrevAlienClicked);
        _nextAlienButton?.UnregisterCallback<ClickEvent>(OnNextAlienClicked);
        _cancelButton?.UnregisterCallback<ClickEvent>(OnCancelButtonClicked);
        _unlockButton?.UnregisterCallback<ClickEvent>(OnUnlockButtonClicked);

        ClearPreviewInstanceAndTexture();
    }

    private void EnsureDragGhost()
    {
        if (_root == null) return;

        if (_dragGhost == null)
        {
            _dragGhost = new Image
            {
                pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit,
                style =
                {
                    position = Position.Absolute,
                    width = 96,
                    height = 96,
                    opacity = 0.9f,
                    display = DisplayStyle.None
                }
            };
        }

        if (_dragGhost.parent != _root)
        {
            _dragGhost.RemoveFromHierarchy();
            _root.Add(_dragGhost);
        }
    }

    private bool IsOverCheckoutZone(Vector2 panelPointerPos)
    {
        if (_checkoutScrollView != null && _checkoutScrollView.worldBound.Contains(panelPointerPos))
            return true;
        if (_contentPane != null && _contentPane.worldBound.Contains(panelPointerPos))
            return true;
        return false;
    }

    private void BindSlotContainers()
    {
        _slotContainers.Clear();
        if (_mutationsList == null) return;

        List<VisualElement> ordered = new();
        foreach (SlotType slot in _slotOrder)
        {
            var named = _mutationsList.Q<VisualElement>($"{slot}SlotIcon");
            if (named != null) ordered.Add(named);
        }

        if (ordered.Count == 0)
        {
            foreach (var child in _mutationsList.Children())
                ordered.Add(child);
        }

        for (int i = 0; i < _slotOrder.Length && i < ordered.Count; i++)
        {
            SlotType slot = _slotOrder[i];
            VisualElement container = ordered[i];
            container.userData = slot;
            _slotContainers[slot] = container;
            SetupSlotDragHandlers(container, slot);
        }
    }

    private void RefreshMenuContents()
    {
        CancelDrag();

        if (CreatureManager.Instance != null)
        {
            capturedAliens = CreatureManager.Instance.GetCapturedAliens();
        }

        PruneAliensWithNoNewMutations();

        // Inject Test Alien Prefab if assigned and list is empty
        if (capturedAliens.Count == 0 && testAlienPrefab != null && HasAnyLockedMutation(testAlienPrefab))
        {
            capturedAliens.Add(testAlienPrefab);
        }

        if (capturedAliens.Count > 0)
        {
            SetPopulatedUIVisible(true);
            _currentAlienIndex = Mathf.Clamp(_currentAlienIndex, 0, capturedAliens.Count - 1);
            LoadAlienPreview(_currentAlienIndex);
        }
        else
        {
            ShowEmptyBarnState();
        }
    }

    private void PruneAliensWithNoNewMutations()
    {
        for (int i = capturedAliens.Count - 1; i >= 0; i--)
        {
            Creature alien = capturedAliens[i];
            if (alien == null)
            {
                capturedAliens.RemoveAt(i);
                continue;
            }

            if (!HasAnyLockedMutation(alien))
            {
                RemoveAlienFromBarn(alien);
                capturedAliens.RemoveAt(i);
            }
        }
    }

    private void SetPopulatedUIVisible(bool visible)
    {
        DisplayStyle display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (_creaturePreviewContainer != null) _creaturePreviewContainer.style.display = display;
        if (_creature3DRender != null) _creature3DRender.style.display = display;
        if (_mutationsList != null) _mutationsList.style.display = display;
        if (_selectMutationsHeaderLabel != null) _selectMutationsHeaderLabel.style.display = display;
        if (_checkoutContainer != null) _checkoutContainer.style.display = display;
        if (_checkoutScrollView != null) _checkoutScrollView.style.display = display;
        if (_prevAlienButton != null) _prevAlienButton.style.display = display;
        if (_nextAlienButton != null) _nextAlienButton.style.display = display;
    }

    private void OnBackButtonClicked(ClickEvent evt) => CloseMenu();
    private void OnPrevAlienClicked(ClickEvent evt) => CycleAlien(-1);
    private void OnNextAlienClicked(ClickEvent evt) => CycleAlien(1);
    private void OnCancelButtonClicked(ClickEvent evt) => ClearSelectedMutations();
    private void OnUnlockButtonClicked(ClickEvent evt) => ConfirmUnlock();

    private void CycleAlien(int direction)
    {
        if (capturedAliens.Count == 0) return;
        CancelDrag();
        _currentAlienIndex = (_currentAlienIndex + direction + capturedAliens.Count) % capturedAliens.Count;
        LoadAlienPreview(_currentAlienIndex);
    }

    public void LoadAlienPreview(int index)
    {
        _currentAlienIndex = index;
        _selectedSlotsToUnlock.Clear();
        _alienEquippedMutations.Clear();

        Creature alien = capturedAliens[_currentAlienIndex];
        if (_alienHeaderLabel != null)
            _alienHeaderLabel.text = FormatAlienDisplayName(alien);
        if (_aliensInBarnCountLabel != null)
            _aliensInBarnCountLabel.text = $"{_currentAlienIndex + 1}/{capturedAliens.Count}";

        ReadAlienMutations(alien, _alienEquippedMutations);
        PopulateAlienMutationIcons();
        RebuildPreviewInstance();
        RefreshCheckoutScrollView();
    }

    private static string FormatAlienDisplayName(Creature alien)
    {
        if (alien == null) return "Unknown Alien";
        string raw = !string.IsNullOrWhiteSpace(alien.CreatureName) ? alien.CreatureName : alien.GetCreatureName();
        return raw.Replace('_', ' ').Trim();
    }

    private void ReadAlienMutations(Creature alien, Dictionary<SlotType, MutationData> targetDict)
    {
        targetDict.Clear();
        if (alien == null) return;

        if (alien.TryGetComponent<CreatureMutator>(out var mutator))
        {
            foreach (SlotType slot in _slotOrder)
            {
                MutationData equipped = mutator.GetEquippedMutation(slot);
                if (equipped != null)
                {
                    targetDict[slot] = equipped;
                }
            }

            if (targetDict.Count == 0)
            {
                foreach (var mutation in mutator.GetConfiguredStartingMutations())
                {
                    if (mutation == null) continue;
                    foreach (SlotType slot in _slotOrder)
                    {
                        if ((mutation.allowedSlots & slot) != 0 && !targetDict.ContainsKey(slot))
                        {
                            targetDict[slot] = mutation;
                            break;
                        }
                    }
                }
            }
        }

        if (targetDict.Count == 0 && alien == testAlienPrefab && testFallbackMutations.Count > 0)
        {
            foreach (var mutation in testFallbackMutations)
            {
                if (mutation == null) continue;
                foreach (SlotType slot in _slotOrder)
                {
                    if ((mutation.allowedSlots & slot) != 0 && !targetDict.ContainsKey(slot))
                    {
                        targetDict[slot] = mutation;
                        break;
                    }
                }
            }
        }
    }

    private bool HasAnyLockedMutation(Creature alien)
    {
        var temp = new Dictionary<SlotType, MutationData>();
        ReadAlienMutations(alien, temp);

        foreach (var mutation in temp.Values)
        {
            if (mutation == null) continue;
            if (MutationUnlockManager.Instance == null || !MutationUnlockManager.Instance.IsUnlocked(mutation))
                return true;
        }
        return false;
    }

    private bool CanSelectSlotForExtraction(SlotType slot)
    {
        if (!_alienEquippedMutations.TryGetValue(slot, out var mutation) || mutation == null)
            return false;
        if (_selectedSlotsToUnlock.Contains(slot))
            return false;
        if (MutationUnlockManager.Instance != null && MutationUnlockManager.Instance.IsUnlocked(mutation))
            return false;
        if (IsMutationStagedInAnySlot(mutation))
            return false;
        return true;
    }

    private void PopulateAlienMutationIcons()
    {
        if (_mutationsList == null) return;

        foreach (SlotType slot in _slotOrder)
        {
            if (!_slotContainers.TryGetValue(slot, out var container)) continue;

            Image imageElement = container.Q<Image>();
            VisualElement bgTarget = container.Q<VisualElement>("Icon")
                                  ?? container.Q<VisualElement>("MutationImage");

            bool hasMutation = _alienEquippedMutations.TryGetValue(slot, out var mutation);

            if (hasMutation)
            {
                Sprite displaySprite = mutation.prefabSprite != null
                    ? mutation.prefabSprite
                    : GameConstants.Instance.DefaultSprite;

                if (imageElement != null)
                {
                    imageElement.image = null;
                    imageElement.sprite = displaySprite;
                    imageElement.style.display = DisplayStyle.Flex;
                }
                if (bgTarget != null)
                {
                    bgTarget.style.backgroundImage = new StyleBackground(displaySprite);
                }
            }
            else if (imageElement != null)
            {
                imageElement.style.display = DisplayStyle.Flex;
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
                container.tooltip = $"{mutation.mutationName} ({slot}) — Drag into checkout or click ({mutation.mutationCost} Tech)";
            }
        }
    }

    private void SetSlotIconVisualState(
        VisualElement container,
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

        container.EnableInClassList("slot-staged", selected);

        float borderWidth = selected ? 3f : 0f;
        container.style.borderBottomWidth = borderWidth;
        container.style.borderTopWidth = borderWidth;
        container.style.borderLeftWidth = borderWidth;
        container.style.borderRightWidth = borderWidth;
        container.style.borderBottomColor = new Color(0.43f, 0.86f, 0.51f, 1f);
        container.style.borderTopColor = new Color(0.43f, 0.86f, 0.51f, 1f);
        container.style.borderLeftColor = new Color(0.43f, 0.86f, 0.51f, 1f);
        container.style.borderRightColor = new Color(0.43f, 0.86f, 0.51f, 1f);
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

    // Dragging FROM an alien's mutation slot box -> must be dropped into the right-hand checkout pane
    private void SetupSlotDragHandlers(VisualElement box, SlotType slot)
    {
        box.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || capturedAliens.Count == 0) return;
            if (!_alienEquippedMutations.TryGetValue(slot, out var mutation) || mutation == null) return;

            if (!_selectedSlotsToUnlock.Contains(slot) && !CanSelectSlotForExtraction(slot))
                return;

            BeginPointerTracking(box, evt.pointerId, evt.position, mutation, sourceSlot: slot, fromCheckout: false);
            evt.StopPropagation();
        });

        box.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_dragSourceSlot != slot || _draggingFromCheckout || !box.HasPointerCapture(evt.pointerId)) return;
            HandlePointerMove(evt.position);
        });

        box.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (_dragSourceSlot != slot || _draggingFromCheckout) return;
            if (box.HasPointerCapture(evt.pointerId))
                box.ReleasePointer(evt.pointerId);

            bool wasDragging = _isDragging;
            CancelDrag();

            if (wasDragging)
            {
                // Only extract/stage if dropped inside the right-hand checkout pane
                if (IsOverCheckoutZone(evt.position) && !_selectedSlotsToUnlock.Contains(slot))
                {
                    SetSlotSelectedForUnlock(slot, true);
                }
            }
            else
            {
                // Click fallback: toggle selection
                ToggleSelectSlotForUnlock(slot);
            }
        });

        box.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (_dragSourceSlot == slot && !_draggingFromCheckout)
                CancelDrag();
        });
    }

    // Dragging or clicking a staged card inside CheckoutScrollView to remove it
    private void SetupCheckoutCardHandlers(VisualElement card, SlotType slot, MutationData mutation)
    {
        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            BeginPointerTracking(card, evt.pointerId, evt.position, mutation, sourceSlot: slot, fromCheckout: true);
            evt.StopPropagation();
        });

        card.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_dragSourceSlot != slot || !_draggingFromCheckout || !card.HasPointerCapture(evt.pointerId)) return;
            HandlePointerMove(evt.position);
        });

        card.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (_dragSourceSlot != slot || !_draggingFromCheckout) return;
            if (card.HasPointerCapture(evt.pointerId))
                card.ReleasePointer(evt.pointerId);

            bool wasDragging = _isDragging;
            CancelDrag();

            if (wasDragging)
            {
                // Dragged out of CheckoutScrollView -> unstage
                if (!IsOverCheckoutZone(evt.position))
                {
                    SetSlotSelectedForUnlock(slot, false);
                }
            }
            else
            {
                // Click fallback -> unstage
                SetSlotSelectedForUnlock(slot, false);
            }
        });

        card.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (_dragSourceSlot == slot && _draggingFromCheckout)
                CancelDrag();
        });
    }

    private void BeginPointerTracking(
        VisualElement target,
        int pointerId,
        Vector2 startPos,
        MutationData mutation,
        SlotType sourceSlot,
        bool fromCheckout)
    {
        _draggingMutation = mutation;
        _dragSourceSlot = sourceSlot;
        _draggingFromCheckout = fromCheckout;
        _dragStartPos = startPos;
        _isDragging = false;
        target.CapturePointer(pointerId);
    }

    private void HandlePointerMove(Vector2 panelPointerPos)
    {
        if (!_isDragging && Vector2.Distance(panelPointerPos, _dragStartPos) >= DragThreshold)
        {
            _isDragging = true;
            EnsureDragGhost();

            if (_dragGhost != null)
            {
                _dragGhost.image = null;
                _dragGhost.sprite = _draggingMutation != null && _draggingMutation.prefabSprite != null
                    ? _draggingMutation.prefabSprite
                    : GameConstants.Instance.DefaultSprite;
                _dragGhost.style.display = DisplayStyle.Flex;
                _dragGhost.BringToFront();
            }
        }

        if (_isDragging)
        {
            if (_dragGhost != null && _root != null)
            {
                Vector2 localPos = _root.WorldToLocal(panelPointerPos);
                _dragGhost.style.left = localPos.x - 48f;
                _dragGhost.style.top = localPos.y - 48f;
            }

            UpdateDropZoneHighlight(panelPointerPos);
        }
    }

    private void UpdateDropZoneHighlight(Vector2 panelPointerPos)
    {
        if (_checkoutScrollView == null) return;

        if (!_draggingFromCheckout && _dragSourceSlot.HasValue)
        {
            bool overCheckout = IsOverCheckoutZone(panelPointerPos);
            _checkoutScrollView.EnableInClassList("slot-drop-valid", true);
            _checkoutScrollView.EnableInClassList("slot-drop-hover", overCheckout);
        }
        else
        {
            _checkoutScrollView.RemoveFromClassList("slot-drop-valid");
            _checkoutScrollView.RemoveFromClassList("slot-drop-hover");
        }
    }

    private void CancelDrag()
    {
        _draggingMutation = null;
        _dragSourceSlot = null;
        _draggingFromCheckout = false;
        _isDragging = false;

        if (_dragGhost != null)
            _dragGhost.style.display = DisplayStyle.None;

        if (_checkoutScrollView != null)
        {
            _checkoutScrollView.RemoveFromClassList("slot-drop-valid");
            _checkoutScrollView.RemoveFromClassList("slot-drop-hover");
        }
    }

    private void ToggleSelectSlotForUnlock(SlotType slot)
    {
        SetSlotSelectedForUnlock(slot, !_selectedSlotsToUnlock.Contains(slot));
    }

    private void SetSlotSelectedForUnlock(SlotType slot, bool selected)
    {
        if (!_alienEquippedMutations.ContainsKey(slot)) return;

        if (selected)
        {
            if (!CanSelectSlotForExtraction(slot)) return;
            _selectedSlotsToUnlock.Add(slot);
        }
        else
        {
            if (!_selectedSlotsToUnlock.Remove(slot)) return;
        }

        PopulateAlienMutationIcons();
        RebuildPreviewInstance();
        RefreshCheckoutScrollView();
    }

    private void RebuildPreviewInstance()
    {
        if (capturedAliens.Count == 0 || previewSpawnPoint == null)
        {
            ClearPreviewInstanceAndTexture();
            return;
        }

        if (_previewInstance != null)
            Destroy(_previewInstance);

        Creature alien = capturedAliens[_currentAlienIndex];

        previewSpawnPoint.localRotation = Quaternion.identity;

        bool wasSpawnActive = previewSpawnPoint.gameObject.activeSelf;
        previewSpawnPoint.gameObject.SetActive(false);

        _previewInstance = Instantiate(
            alien.gameObject,
            previewSpawnPoint.position,
            previewSpawnPoint.rotation,
            previewSpawnPoint
        );
        _previewInstance.transform.localPosition = Vector3.zero;
        _previewInstance.transform.localRotation = Quaternion.identity;

        SanitizePreviewClone(_previewInstance);

        previewSpawnPoint.gameObject.SetActive(wasSpawnActive);

        if (_previewInstance.TryGetComponent<CreatureMutator>(out var previewMutator))
        {
            if (alien.TryGetComponent<CreatureMutator>(out var sourceMutator))
            {
                previewMutator.CopySocketsFrom(sourceMutator, alien.transform, _previewInstance.transform);
                previewMutator.ClearAllSocketChildrenImmediate();
                previewMutator.InitializeForPreview();
            }

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

    private void ClearPreviewInstanceAndTexture()
    {
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
            _previewInstance = null;
        }

        if (previewCamera != null)
            previewCamera.enabled = false;

        if (previewRenderTexture != null)
        {
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = previewRenderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }
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

            Sprite sprite = mutation.prefabSprite != null
                ? mutation.prefabSprite
                : GameConstants.Instance.DefaultSprite;

            Image cardImage = card.Q<Image>();
            if (cardImage != null)
            {
                cardImage.image = null;
                cardImage.sprite = sprite;
                cardImage.style.backgroundImage = StyleKeyword.None;
            }
            else
            {
                VisualElement iconBox = card.Q<VisualElement>("Icon") ?? card.Q<VisualElement>("PartIcon");
                if (iconBox != null)
                    iconBox.style.backgroundImage = new StyleBackground(sprite);
            }

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

            SetupCheckoutCardHandlers(card, slot, mutation);

            _checkoutScrollView.Add(card);
        }

        bool hasSelection = _selectedSlotsToUnlock.Count > 0;
        bool canAfford = CanAffordTechnology(totalCost);

        if (_costLabel != null)
        {
            _costLabel.text = totalCost.ToString();
            _costLabel.style.color = (!hasSelection || canAfford)
                ? new Color(0.65f, 0.35f, 1f, 1f)
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

        // Purge ALL captured aliens in the barn that now have zero unresearched mutations remaining
        PruneAliensWithNoNewMutations();

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

    private static void RemoveAlienFromBarn(Creature alien)
    {
        if (alien == null) return;
        bool isPrefabAsset = !alien.gameObject.scene.IsValid();
        if (isPrefabAsset) return;

        if (CreatureManager.Instance != null)
        {
            CreatureManager.Instance.UnregisterCapturedAlien(alien);
            CreatureManager.Instance.Unregister(alien);
        }
        Destroy(alien.gameObject);
    }

    private void ShowEmptyBarnState()
    {
        ClearPreviewInstanceAndTexture();

        _alienEquippedMutations.Clear();
        _selectedSlotsToUnlock.Clear();

        SetPopulatedUIVisible(false);

        if (_alienHeaderLabel != null) _alienHeaderLabel.text = "No Aliens in Barn";
        if (_aliensInBarnCountLabel != null) _aliensInBarnCountLabel.text = "0/0";

        RefreshCheckoutScrollView();
    }

    public void CloseMenu()
    {
        CancelDrag();
        ClearPreviewInstanceAndTexture();
        _selectedSlotsToUnlock.Clear();

        if (barnUIDocument != null && barnUIDocument.rootVisualElement != null)
            barnUIDocument.rootVisualElement.style.display = DisplayStyle.None;

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
            if (isDragging && !_isDragging && previewSpawnPoint != null)
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
        {
            bob.enabled = false;
            bob.transform.localPosition = Vector3.zero;
            bob.transform.localRotation = Quaternion.identity;
        }

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