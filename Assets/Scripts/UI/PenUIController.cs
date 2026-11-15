// using System.Collections.Generic;
// using System.Linq;
// using UnityEngine;
// using UnityEngine.AI;
// using UnityEngine.InputSystem;
// using UnityEngine.UIElements;
//
// public class PenUIController : MonoBehaviour
// {
//     [Header("UI & Sub-Templates")]
//     [SerializeField] private UIDocument penUIDocument;
//     [SerializeField] private VisualTreeAsset mutationIconTemplate;      // MutationIcon.uxml
//     [SerializeField] private VisualTreeAsset breedingCardTemplate;      // BreedingAnimalCard.uxml
//
//     [Header("3D Preview Stage")]
//     [SerializeField] private Transform previewSpawnPoint;
//     [SerializeField] private Camera previewCamera;
//     [SerializeField] private RenderTexture previewRenderTexture;
//
//     [Header("Pen & Economy Settings")]
//     [SerializeField] private int maxPenCapacity = 6;
//     [SerializeField] private int healHayCost = 10;
//     [SerializeField] private int breedHayCost = 10;
//     [SerializeField] private int mutationHayCost = 10;
//
//     [Header("Play Mode Testing (Optional)")]
//     [SerializeField] private List<Creature> testPenAnimals = new();
//
//     private readonly List<Creature> _penAnimals = new();
//     private readonly SlotType[] _slotOrder = { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail };
//
//     // Animals Tab State
//     private int _currentAnimalIndex;
//     private SlotType _activeSlot = SlotType.Head;
//     private readonly Dictionary<SlotType, MutationData> _stagedMutations = new();
//     private GameObject _previewInstance;
//     private int _previewLayer;
//     private bool _isOpen;
//
//     // Breeding Tab State
//     private Creature _parentA;
//     private Creature _parentB;
//
//     // Root & Top Bar
//     private VisualElement _root;
//     private VisualElement _animalsView;
//     private VisualElement _breedingView;
//     private Button _animalsTabBtn;
//     private Button _breedingTabBtn;
//
//     // Animals Tab UI Elements
//     private VisualElement _creature3DRender;
//     private Label _animalCountLabel;
//     private Label _animalNameLabel;
//     private VisualElement _hpFillBar;
//     private Label _hpValueLabel;
//     private Button _healButton;
//     private Label _healCostLabel;
//     private VisualElement _equippedSlotsContainer;
//     private Label _mutationHayCostLabel;
//     private Label _mutationTechCostLabel;
//     private Button _cancelMutateButton;
//     private Button _confirmMutateButton;
//     private VisualElement _unlockedMutationsGrid;
//
//     // Right Details Pane (Stats, Equipped Mutations, Selected Mutation Info)
//     private Label _levelLabel;
//     private Label _maxHealthLabel;
//     private Label _speedLabel;
//     private Label _meleeDamageLabel;
//     private Label _rangedDamageLabel;
//     private readonly Dictionary<SlotType, Label> _equippedNameLabels = new();
//     private readonly Dictionary<SlotType, Label> _equippedDescLabels = new();
//     private Image _selectedMutationIcon;
//     private Label _selectedMutationName;
//     private Label _selectedMutationSlots;
//     private Label _selectedMutationEffect;
//
//     // Breeding Tab UI Elements
//     private VisualElement _parentSlotAContainer;
//     private VisualElement _parentSlotBContainer;
//     private Button _breedButton;
//     private Label _breedCostLabel;
//     private Label _penRosterCountLabel;
//     private VisualElement _penRosterGrid;
//
//     public bool IsOpen => _isOpen;
//
//     private void Awake()
//     {
//         _previewLayer = LayerMask.NameToLayer("UI_Preview");
//     }
//
//     private void Update()
//     {
//         if (!_isOpen) return;
//
//         if (previewSpawnPoint != null && _previewInstance != null && _animalsView != null && _animalsView.style.display == DisplayStyle.Flex)
//         {
//             _previewInstance.transform.Rotate(Vector3.up, 15f * Time.deltaTime, Space.World);
//         }
//
//         if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
//         {
//             CloseMenu();
//         }
//     }
//
//     private void OnRootKeyDown(KeyDownEvent evt)
//     {
//         if (evt.keyCode == KeyCode.Escape)
//         {
//             evt.StopPropagation();
//             CloseMenu();
//         }
//     }
//
//     /// <summary>
//     /// Opens the Pen UI and populates it with the animals currently inside the selected Pen.
//     /// </summary>
//     public void OpenMenu(IEnumerable<Creature> animalsInPen = null)
//     {
//         if (animalsInPen != null)
//         {
//             _penAnimals.Clear();
//             _penAnimals.AddRange(animalsInPen.Where(a => a != null));
//         }
//
//         if (!gameObject.activeSelf)
//             gameObject.SetActive(true);
//
//         _isOpen = true;
//
//         if (_root != null)
//         {
//             _root.style.display = DisplayStyle.Flex;
//             _root.Focus();
//             InitializeMenuData();
//         }
//     }
//
//     public void CloseMenu()
//     {
//         _isOpen = false;
//         _stagedMutations.Clear();
//         _parentA = null;
//         _parentB = null;
//
//         if (previewCamera != null) previewCamera.enabled = false;
//         if (_previewInstance != null) Destroy(_previewInstance);
//
//         if (_root != null)
//             _root.style.display = DisplayStyle.None;
//     }
//
//     private void OnEnable()
//     {
//         if (penUIDocument == null)
//         {
//             Debug.LogError("[PenUIController] penUIDocument is not assigned!");
//             return;
//         }
//
//         _root = penUIDocument.rootVisualElement;
//         if (_root == null) return;
//
//         _root.focusable = true;
//         _root.RegisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
//
//         QueryAllElements();
//         BindStaticCallbacks();
//
//         if (_isOpen)
//         {
//             _root.style.display = DisplayStyle.Flex;
//             InitializeMenuData();
//         }
//         else
//         {
//             _root.style.display = DisplayStyle.None;
//         }
//     }
//
//     private void OnDisable()
//     {
//         if (_root != null)
//         {
//             _root.UnregisterCallback<KeyDownEvent>(OnRootKeyDown, TrickleDown.TrickleDown);
//             _root.Q<Button>("BackButton")?.UnregisterCallback<ClickEvent>(OnBackClicked);
//         }
//
//         _animalsTabBtn?.UnregisterCallback<ClickEvent>(OnAnimalsTabClicked);
//         _breedingTabBtn?.UnregisterCallback<ClickEvent>(OnBreedingTabClicked);
//         _root?.Q<Button>("PrevAnimalButton")?.UnregisterCallback<ClickEvent>(OnPrevAnimalClicked);
//         _root?.Q<Button>("NextAnimalButton")?.UnregisterCallback<ClickEvent>(OnNextAnimalClicked);
//         _healButton?.UnregisterCallback<ClickEvent>(OnHealClicked);
//         _cancelMutateButton?.UnregisterCallback<ClickEvent>(OnCancelMutateClicked);
//         _confirmMutateButton?.UnregisterCallback<ClickEvent>(OnConfirmMutateClicked);
//         _breedButton?.UnregisterCallback<ClickEvent>(OnBreedClicked);
//
//         if (previewCamera != null) previewCamera.enabled = false;
//     }
//
//     private void QueryAllElements()
//     {
//         // Views & Top Bar
//         _animalsView = _root.Q<VisualElement>("AnimalsView");
//         _breedingView = _root.Q<VisualElement>("BreedingView");
//         _animalsTabBtn = _root.Q<Button>("AnimalsTabButton");
//         _breedingTabBtn = _root.Q<Button>("BreedingTabButton");
//
//         // Animals Tab Left Pane
//         _creature3DRender = _root.Q<VisualElement>("Creature3DRender");
//         if (_creature3DRender != null && previewRenderTexture != null)
//         {
//             _creature3DRender.style.backgroundImage =
//                 new StyleBackground(Background.FromRenderTexture(previewRenderTexture));
//             SetupViewportOrbit(_creature3DRender);
//         }
//
//         _animalCountLabel = _root.Q<Label>("AnimalCountLabel");
//         _animalNameLabel = _root.Q<Label>("AnimalNameLabel");
//         _hpFillBar = _root.Q<VisualElement>("HPFillBar");
//         _hpValueLabel = _root.Q<Label>("HPValueLabel");
//         _healButton = _root.Q<Button>("HealButton");
//         _healCostLabel = _root.Q<Label>("HealCostLabel");
//         _equippedSlotsContainer = _root.Q<VisualElement>("EquippedSlotsContainer");
//         _mutationHayCostLabel = _root.Q<Label>("MutationHayCost");
//         _mutationTechCostLabel = _root.Q<Label>("MutationTechCost");
//         _cancelMutateButton = _root.Q<Button>("CancelMutateButton");
//         _confirmMutateButton = _root.Q<Button>("ConfirmMutateButton");
//         _unlockedMutationsGrid = _root.Q<VisualElement>("UnlockedMutationsGrid");
//
//         // Animals Tab Right Pane (Stats & Equipped Mutations)
//         _levelLabel = _root.Q<Label>("LevelValue");
//         _maxHealthLabel = _root.Q<Label>("MaxHealthValue");
//         _speedLabel = _root.Q<Label>("SpeedValue");
//         _meleeDamageLabel = _root.Q<Label>("MeleeDamageValue");
//         _rangedDamageLabel = _root.Q<Label>("RangedDamageValue");
//
//         _equippedNameLabels[SlotType.Head] = _root.Q<Label>("HeadMutationName");
//         _equippedDescLabels[SlotType.Head] = _root.Q<Label>("HeadMutationDesc");
//         _equippedNameLabels[SlotType.Back] = _root.Q<Label>("BackMutationName");
//         _equippedDescLabels[SlotType.Back] = _root.Q<Label>("BackMutationDesc");
//         _equippedNameLabels[SlotType.Limbs] = _root.Q<Label>("LimbsMutationName");
//         _equippedDescLabels[SlotType.Limbs] = _root.Q<Label>("LimbsMutationDesc");
//         _equippedNameLabels[SlotType.Tail] = _root.Q<Label>("TailMutationName");
//         _equippedDescLabels[SlotType.Tail] = _root.Q<Label>("TailMutationDesc");
//
//         var inspectorCard = _root.Q<VisualElement>("SelectedMutationCard");
//         if (inspectorCard != null)
//         {
//             _selectedMutationIcon = inspectorCard.Q<Image>();
//             _selectedMutationName = inspectorCard.Q<Label>("MutationName");
//             _selectedMutationSlots = inspectorCard.Q<Label>("MutationSlots");
//             _selectedMutationEffect = inspectorCard.Q<Label>("MutationEffect");
//         }
//
//         // Breeding Tab
//         _parentSlotAContainer = _root.Q<VisualElement>("ParentSlotA");
//         _parentSlotBContainer = _root.Q<VisualElement>("ParentSlotB");
//         _breedButton = _root.Q<Button>("BreedButton");
//         _breedCostLabel = _root.Q<Label>("BreedCostLabel");
//         _penRosterCountLabel = _root.Q<Label>("PenRosterCountLabel");
//         _penRosterGrid = _root.Q<VisualElement>("PenRosterGrid");
//     }
//
//     private void BindStaticCallbacks()
//     {
//         _root.Q<Button>("BackButton")?.RegisterCallback<ClickEvent>(OnBackClicked);
//         _animalsTabBtn?.RegisterCallback<ClickEvent>(OnAnimalsTabClicked);
//         _breedingTabBtn?.RegisterCallback<ClickEvent>(OnBreedingTabClicked);
//
//         _root.Q<Button>("PrevAnimalButton")?.RegisterCallback<ClickEvent>(OnPrevAnimalClicked);
//         _root.Q<Button>("NextAnimalButton")?.RegisterCallback<ClickEvent>(OnNextAnimalClicked);
//         _healButton?.RegisterCallback<ClickEvent>(OnHealClicked);
//         _cancelMutateButton?.RegisterCallback<ClickEvent>(OnCancelMutateClicked);
//         _confirmMutateButton?.RegisterCallback<ClickEvent>(OnConfirmMutateClicked);
//         _breedButton?.RegisterCallback<ClickEvent>(OnBreedClicked);
//     }
//
//     private void InitializeMenuData()
//     {
//         _penAnimals.RemoveAll(a => a == null);
//
//         if (_penAnimals.Count == 0 && testPenAnimals.Count > 0)
//             _penAnimals.AddRange(testPenAnimals.Where(a => a != null));
//
//         _currentAnimalIndex = 0;
//         _stagedMutations.Clear();
//         _parentA = null;
//         _parentB = null;
//
//         ShowAnimalsTab();
//     }
//
//     // -------------------------------------------------------------------------
//     // Tab Switching
//     // -------------------------------------------------------------------------
//
//     private void ShowAnimalsTab()
//     {
//         if (_animalsView != null) _animalsView.style.display = DisplayStyle.Flex;
//         if (_breedingView != null) _breedingView.style.display = DisplayStyle.None;
//
//         _animalsTabBtn?.AddToClassList("tab-active");
//         _breedingTabBtn?.RemoveFromClassList("tab-active");
//
//         if (previewCamera != null) previewCamera.enabled = _penAnimals.Count > 0;
//         RefreshAnimalsTab();
//     }
//
//     private void ShowBreedingTab()
//     {
//         if (_animalsView != null) _animalsView.style.display = DisplayStyle.None;
//         if (_breedingView != null) _breedingView.style.display = DisplayStyle.Flex;
//
//         _breedingTabBtn?.AddToClassList("tab-active");
//         _animalsTabBtn?.RemoveFromClassList("tab-active");
//
//         if (previewCamera != null) previewCamera.enabled = false;
//         RefreshBreedingTab();
//     }
//
//     private void OnBackClicked(ClickEvent evt) => CloseMenu();
//     private void OnAnimalsTabClicked(ClickEvent evt) => ShowAnimalsTab();
//     private void OnBreedingTabClicked(ClickEvent evt) => ShowBreedingTab();
//     private void OnPrevAnimalClicked(ClickEvent evt) => CycleAnimal(-1);
//     private void OnNextAnimalClicked(ClickEvent evt) => CycleAnimal(1);
//     private void OnHealClicked(ClickEvent evt) => HealCurrentAnimal();
//     private void OnCancelMutateClicked(ClickEvent evt) => CancelStagedMutations();
//     private void OnConfirmMutateClicked(ClickEvent evt) => ApplyStagedMutations();
//     private void OnBreedClicked(ClickEvent evt) => BreedSelectedParents();
//
//     // -------------------------------------------------------------------------
//     // Animals Tab Logic
//     // -------------------------------------------------------------------------
//
//     private void CycleAnimal(int direction)
//     {
//         if (_penAnimals.Count == 0) return;
//
//         _stagedMutations.Clear();
//         _currentAnimalIndex = (_currentAnimalIndex + direction + _penAnimals.Count) % _penAnimals.Count;
//         RefreshAnimalsTab();
//     }
//
//     private void RefreshAnimalsTab()
//     {
//         if (_penAnimals.Count == 0)
//         {
//             if (_animalCountLabel != null) _animalCountLabel.text = $"0/{maxPenCapacity}";
//             if (_animalNameLabel != null) _animalNameLabel.text = "No Animals in Pen";
//             if (_previewInstance != null) Destroy(_previewInstance);
//             return;
//         }
//
//         _currentAnimalIndex = Mathf.Clamp(_currentAnimalIndex, 0, _penAnimals.Count - 1);
//         Creature animal = _penAnimals[_currentAnimalIndex];
//
//         if (_animalCountLabel != null)
//             _animalCountLabel.text = $"{_currentAnimalIndex + 1}/{maxPenCapacity}";
//
//         if (_animalNameLabel != null)
//             _animalNameLabel.text = $"{animal.GetCreatureName()} (Level 1)";
//
//         UpdateHealthBar(animal);
//         PopulateEquippedSlots(animal);
//         PopulateUnlockedMutationsGrid(animal);
//         UpdateStatsAndMutationsPanel(animal);
//         UpdateMutationCostBox();
//         RebuildPreviewInstance(animal);
//     }
//
//     private void UpdateHealthBar(Creature animal)
//     {
//         // Hook these into your Creature's current/max health properties if named differently
//         float currentHp = animal.Health;
//         float maxHp = animal.MaxHealth;
//         float ratio = maxHp > 0f ? Mathf.Clamp01(currentHp / maxHp) : 1f;
//
//         if (_hpFillBar != null)
//             _hpFillBar.style.width = Length.Percent(ratio * 100f);
//
//         if (_hpValueLabel != null)
//             _hpValueLabel.text = $"HP: {Mathf.CeilToInt(currentHp)}/{Mathf.CeilToInt(maxHp)}";
//
//         if (_healCostLabel != null)
//             _healCostLabel.text = healHayCost.ToString();
//
//         bool needsHealing = currentHp < maxHp;
//         bool canAffordHeal = ResourceManager.GetResourceAmount(ResourceType.Hay) >= healHayCost;
//         _healButton?.SetEnabled(needsHealing && canAffordHeal);
//     }
//
//     private void HealCurrentAnimal()
//     {
//         if (_penAnimals.Count == 0) return;
//         Creature animal = _penAnimals[_currentAnimalIndex];
//
//         if (animal.Health >= animal.MaxHealth) return;
//         if (!ResourceManager.Spend(ResourceType.Hay, healHayCost)) return;
//
//         animal.Heal(animal.MaxHealth);
//         UpdateHealthBar(animal);
//     }
//
//     private void PopulateEquippedSlots(Creature animal)
//     {
//         if (_equippedSlotsContainer == null) return;
//
//         var slotBoxes = _equippedSlotsContainer.Query<TemplateContainer>().ToList();
//         animal.TryGetComponent<CreatureMutator>(out var mutator);
//
//         for (int i = 0; i < _slotOrder.Length; i++)
//         {
//             if (i >= slotBoxes.Count) break;
//
//             SlotType slot = _slotOrder[i];
//             TemplateContainer box = slotBoxes[i];
//             box.userData = slot;
//
//             box.UnregisterCallback<ClickEvent>(OnEquippedSlotClicked);
//             box.RegisterCallback<ClickEvent>(OnEquippedSlotClicked);
//
//             MutationData effectiveMutation = GetEffectiveMutationForSlot(mutator, slot);
//             Sprite sprite = effectiveMutation != null && effectiveMutation.prefabSprite != null
//                 ? effectiveMutation.prefabSprite
//                 : GameConstants.Instance.DefaultSprite;
//
//             Image img = box.Q<Image>();
//             if (img != null)
//             {
//                 img.sprite = sprite;
//                 img.style.backgroundImage = StyleKeyword.None;
//             }
//
//             // Highlight active slot with purple border, staged slot with bright tint
//             bool isSelectedSlot = slot == _activeSlot;
//             bool isStaged = _stagedMutations.ContainsKey(slot);
//
//             float border = isSelectedSlot ? 3f : (isStaged ? 2f : 0f);
//             Color borderColor = isSelectedSlot ? new Color(0.75f, 0.35f, 1f, 1f) : Color.white;
//
//             box.style.borderTopWidth = border;
//             box.style.borderBottomWidth = border;
//             box.style.borderLeftWidth = border;
//             box.style.borderRightWidth = border;
//             box.style.borderTopColor = borderColor;
//             box.style.borderBottomColor = borderColor;
//             box.style.borderLeftColor = borderColor;
//             box.style.borderRightColor = borderColor;
//         }
//     }
//
//     private void OnEquippedSlotClicked(ClickEvent evt)
//     {
//         if (evt.currentTarget is VisualElement el && el.userData is SlotType slot)
//         {
//             _activeSlot = slot;
//             RefreshAnimalsTab();
//         }
//     }
//
//     private void PopulateUnlockedMutationsGrid(Creature animal)
//     {
//         if (_unlockedMutationsGrid == null) return;
//         _unlockedMutationsGrid.Clear();
//
//         if (MutationUnlockManager.Instance == null) return;
//
//         animal.TryGetComponent<CreatureMutator>(out var mutator);
//         MutationData currentInActiveSlot = GetEffectiveMutationForSlot(mutator, _activeSlot);
//
//         foreach (MutationData mutation in MutationUnlockManager.Instance.UnlockedMutations)
//         {
//             if (mutation == null) continue;
//
//             VisualElement item = mutationIconTemplate != null
//                 ? mutationIconTemplate.Instantiate()
//                 : new VisualElement();
//
//             Image img = item.Q<Image>();
//             if (img != null)
//             {
//                 img.sprite = mutation.prefabSprite != null
//                     ? mutation.prefabSprite
//                     : GameConstants.Instance.DefaultSprite;
//                 img.style.backgroundImage = StyleKeyword.None;
//             }
//
//             bool fitsActiveSlot = (mutation.allowedSlots & _activeSlot) != 0;
//             bool isAlreadyEquipped = currentInActiveSlot == mutation;
//
//             item.SetEnabled(fitsActiveSlot && !isAlreadyEquipped);
//             item.style.opacity = (fitsActiveSlot && !isAlreadyEquipped) ? 1f : 0.35f;
//             item.tooltip = $"{mutation.mutationName} — {BuildEffectSummary(mutation)}";
//
//             MutationData captured = mutation;
//             item.RegisterCallback<PointerEnterEvent>(_ => ShowMutationDetailsCard(captured));
//             if (fitsActiveSlot && !isAlreadyEquipped)
//             {
//                 item.RegisterCallback<ClickEvent>(_ => StageMutationForActiveSlot(captured));
//             }
//
//             _unlockedMutationsGrid.Add(item);
//         }
//     }
//
//     private void StageMutationForActiveSlot(MutationData mutation)
//     {
//         _stagedMutations[_activeSlot] = mutation;
//         ShowMutationDetailsCard(mutation);
//         RefreshAnimalsTab();
//     }
//
//     private void CancelStagedMutations()
//     {
//         if (_stagedMutations.Count == 0) return;
//         _stagedMutations.Clear();
//         RefreshAnimalsTab();
//     }
//
//     private void ApplyStagedMutations()
//     {
//         if (_penAnimals.Count == 0 || _stagedMutations.Count == 0) return;
//
//         int totalHay = _stagedMutations.Count * mutationHayCost;
//         int totalTech = _stagedMutations.Values.Sum(m => m.mutationCost);
//
//         if (ResourceManager.GetResourceAmount(ResourceType.Hay) < totalHay ||
//             ResourceManager.GetResourceAmount(ResourceType.Technology) < totalTech)
//         {
//             return;
//         }
//
//         ResourceManager.Spend(ResourceType.Hay, totalHay);
//         ResourceManager.Spend(ResourceType.Technology, totalTech);
//
//         Creature animal = _penAnimals[_currentAnimalIndex];
//         if (animal.TryGetComponent<CreatureMutator>(out var mutator))
//         {
//             foreach (var kvp in _stagedMutations)
//             {
//                 mutator.EquipMutation(kvp.Value, kvp.Key);
//             }
//         }
//
//         _stagedMutations.Clear();
//         RefreshAnimalsTab();
//     }
//
//     private void UpdateMutationCostBox()
//     {
//         int count = _stagedMutations.Count;
//         int hayCost = count * mutationHayCost;
//         int techCost = _stagedMutations.Values.Sum(m => m.mutationCost);
//
//         if (_mutationHayCostLabel != null) _mutationHayCostLabel.text = hayCost.ToString();
//         if (_mutationTechCostLabel != null) _mutationTechCostLabel.text = techCost.ToString();
//
//         bool hasStaged = count > 0;
//         bool canAfford = ResourceManager.GetResourceAmount(ResourceType.Hay) >= hayCost &&
//                          ResourceManager.GetResourceAmount(ResourceType.Technology) >= techCost;
//
//         _cancelMutateButton?.SetEnabled(hasStaged);
//         _confirmMutateButton?.SetEnabled(hasStaged && canAfford);
//     }
//
//     private void UpdateStatsAndMutationsPanel(Creature animal)
//     {
//         animal.TryGetComponent<CreatureMutator>(out var mutator);
//
//         int bonusHp = 0;
//         int bonusDamage = 0;
//         float bonusSpeed = 0f;
//
//         foreach (SlotType slot in _slotOrder)
//         {
//             MutationData m = GetEffectiveMutationForSlot(mutator, slot);
//
//             if (_equippedNameLabels.TryGetValue(slot, out var nameLbl) && nameLbl != null)
//                 nameLbl.text = m != null ? m.mutationName : "None";
//
//             if (_equippedDescLabels.TryGetValue(slot, out var descLbl) && descLbl != null)
//                 descLbl.text = m != null ? $"→ {BuildEffectSummary(m)}" : "→ Empty slot";
//
//             if (m != null)
//             {
//                 bonusHp += m.bonusMaxHealth;
//                 bonusDamage += m.bonusAttackDamage;
//                 bonusSpeed += m.bonusMoveSpeed;
//             }
//         }
//
//         if (_levelLabel != null) _levelLabel.text = "1";
//         if (_maxHealthLabel != null) _maxHealthLabel.text = Mathf.RoundToInt(animal.MaxHealth + bonusHp).ToString();
//         if (_speedLabel != null) _speedLabel.text = (10f + bonusSpeed).ToString("0.#");
//         if (_meleeDamageLabel != null) _meleeDamageLabel.text = (10 + bonusDamage).ToString();
//         if (_rangedDamageLabel != null) _rangedDamageLabel.text = "0";
//
//         // Default inspector card to the currently selected slot's mutation
//         MutationData activeMutation = GetEffectiveMutationForSlot(mutator, _activeSlot);
//         if (activeMutation != null)
//             ShowMutationDetailsCard(activeMutation);
//     }
//
//     private void ShowMutationDetailsCard(MutationData m)
//     {
//         if (m == null) return;
//
//         if (_selectedMutationIcon != null)
//         {
//             _selectedMutationIcon.sprite = m.prefabSprite != null
//                 ? m.prefabSprite
//                 : GameConstants.Instance.DefaultSprite;
//         }
//
//         if (_selectedMutationName != null) _selectedMutationName.text = m.mutationName;
//         if (_selectedMutationSlots != null) _selectedMutationSlots.text = FormatAllowedSlots(m.allowedSlots);
//         if (_selectedMutationEffect != null) _selectedMutationEffect.text = BuildEffectSummary(m);
//     }
//
//     private MutationData GetEffectiveMutationForSlot(CreatureMutator mutator, SlotType slot)
//     {
//         if (_stagedMutations.TryGetValue(slot, out var staged))
//             return staged;
//
//         return mutator != null ? mutator.GetEquippedMutation(slot) : null;
//     }
//
//     // -------------------------------------------------------------------------
//     // Breeding Tab Logic
//     // -------------------------------------------------------------------------
//
//     private void RefreshBreedingTab()
//     {
//         if (_penRosterCountLabel != null)
//             _penRosterCountLabel.text = $"{_penAnimals.Count}/{maxPenCapacity}";
//
//         if (_breedCostLabel != null)
//             _breedCostLabel.text = breedHayCost.ToString();
//
//         PopulateParentSlot(_parentSlotAContainer, _parentA, isSlotA: true);
//         PopulateParentSlot(_parentSlotBContainer, _parentB, isSlotA: false);
//         PopulatePenRosterGrid();
//
//         bool canBreed = _parentA != null &&
//                         _parentB != null &&
//                         _parentA != _parentB &&
//                         _penAnimals.Count < maxPenCapacity &&
//                         ResourceManager.GetResourceAmount(ResourceType.Hay) >= breedHayCost;
//
//         _breedButton?.SetEnabled(canBreed);
//     }
//
//     private void PopulateParentSlot(VisualElement container, Creature parent, bool isSlotA)
//     {
//         if (container == null) return;
//         container.Clear();
//
//         if (parent == null) return;
//
//         VisualElement card = BuildAnimalCard(parent, isSelected: true);
//         card.RegisterCallback<ClickEvent>(_ =>
//         {
//             if (isSlotA) _parentA = null;
//             else _parentB = null;
//             RefreshBreedingTab();
//         });
//         container.Add(card);
//     }
//
//     private void PopulatePenRosterGrid()
//     {
//         if (_penRosterGrid == null) return;
//         _penRosterGrid.Clear();
//
//         for (int i = 0; i < maxPenCapacity; i++)
//         {
//             if (i < _penAnimals.Count)
//             {
//                 Creature animal = _penAnimals[i];
//                 bool isParent = animal == _parentA || animal == _parentB;
//
//                 VisualElement card = BuildAnimalCard(animal, isParent);
//                 Creature captured = animal;
//                 card.RegisterCallback<ClickEvent>(_ => ToggleParentSelection(captured));
//                 _penRosterGrid.Add(card);
//             }
//             else
//             {
//                 // Empty pen slot placeholder ('?')
//                 VisualElement emptyCard = breedingCardTemplate != null
//                     ? breedingCardTemplate.Instantiate()
//                     : new VisualElement();
//
//                 emptyCard.SetEnabled(false);
//                 emptyCard.style.opacity = 0.5f;
//                 _penRosterGrid.Add(emptyCard);
//             }
//         }
//     }
//
//     private VisualElement BuildAnimalCard(Creature animal, bool isSelected)
//     {
//         VisualElement card = breedingCardTemplate != null
//             ? breedingCardTemplate.Instantiate()
//             : new VisualElement();
//
//         Label nameLbl = card.Q<Label>("AnimalNameLabel");
//         if (nameLbl != null)
//             nameLbl.text = $"{animal.GetCreatureName()} (Level 1)";
//
//         // Populate the 4 mini mutation squares at the bottom of the card
//         var miniSlots = card.Query<VisualElement>("MiniMutationSlot").ToList();
//         animal.TryGetComponent<CreatureMutator>(out var mutator);
//
//         for (int i = 0; i < _slotOrder.Length && i < miniSlots.Count; i++)
//         {
//             MutationData m = mutator != null ? mutator.GetEquippedMutation(_slotOrder[i]) : null;
//             if (m != null && m.prefabSprite != null)
//             {
//                 miniSlots[i].style.backgroundImage = new StyleBackground(m.prefabSprite);
//             }
//         }
//
//         float border = isSelected ? 2f : 0f;
//         card.style.borderTopWidth = border;
//         card.style.borderBottomWidth = border;
//         card.style.borderLeftWidth = border;
//         card.style.borderRightWidth = border;
//         card.style.borderTopColor = Color.white;
//         card.style.borderBottomColor = Color.white;
//         card.style.borderLeftColor = Color.white;
//         card.style.borderRightColor = Color.white;
//
//         return card;
//     }
//
//     private void ToggleParentSelection(Creature animal)
//     {
//         if (_parentA == animal) _parentA = null;
//         else if (_parentB == animal) _parentB = null;
//         else if (_parentA == null) _parentA = animal;
//         else if (_parentB == null) _parentB = animal;
//         else _parentB = animal;
//
//         RefreshBreedingTab();
//     }
//
//     private void BreedSelectedParents()
//     {
//         if (_parentA == null || _parentB == null || _parentA == _parentB) return;
//         if (_penAnimals.Count >= maxPenCapacity) return;
//         if (!ResourceManager.Spend(ResourceType.Hay, breedHayCost)) return;
//
//         // Spawn offspring near Parent A and inherit random unlocked/parent mutations per slot
//         Vector3 spawnPos = _parentA.transform.position + Vector3.right * 1.5f;
//         GameObject offspringObj = Instantiate(_parentA.gameObject, spawnPos, _parentA.transform.rotation);
//         offspringObj.name = _parentA.GetCreatureName();
//
//         if (offspringObj.TryGetComponent<Creature>(out var offspring))
//         {
//             if (offspringObj.TryGetComponent<CreatureMutator>(out var childMutator))
//             {
//                 _parentA.TryGetComponent<CreatureMutator>(out var mutA);
//                 _parentB.TryGetComponent<CreatureMutator>(out var mutB);
//
//                 foreach (SlotType slot in _slotOrder)
//                 {
//                     MutationData fromA = mutA != null ? mutA.GetEquippedMutation(slot) : null;
//                     MutationData fromB = mutB != null ? mutB.GetEquippedMutation(slot) : null;
//                     MutationData inherited = Random.value < 0.5f ? (fromA ?? fromB) : (fromB ?? fromA);
//
//                     if (inherited != null)
//                         childMutator.EquipMutation(inherited, slot);
//                 }
//             }
//
//             _penAnimals.Add(offspring);
//         }
//
//         _parentA = null;
//         _parentB = null;
//         RefreshBreedingTab();
//     }
//
//     // -------------------------------------------------------------------------
//     // 3D Preview & Formatting Helpers
//     // -------------------------------------------------------------------------
//
//     private void RebuildPreviewInstance(Creature animal)
//     {
//         if (_previewInstance != null)
//             Destroy(_previewInstance);
//
//         if (previewSpawnPoint == null || animal == null) return;
//
//         bool wasSpawnActive = previewSpawnPoint.gameObject.activeSelf;
//         previewSpawnPoint.gameObject.SetActive(false);
//
//         _previewInstance = Instantiate(
//             animal.gameObject,
//             previewSpawnPoint.position,
//             previewSpawnPoint.rotation,
//             previewSpawnPoint
//         );
//
//         SanitizePreviewClone(_previewInstance);
//         previewSpawnPoint.gameObject.SetActive(wasSpawnActive);
//
//         if (_previewInstance.TryGetComponent<CreatureMutator>(out var previewMutator))
//         {
//             animal.TryGetComponent<CreatureMutator>(out var liveMutator);
//             foreach (SlotType slot in _slotOrder)
//             {
//                 MutationData m = GetEffectiveMutationForSlot(liveMutator, slot);
//                 if (m != null) previewMutator.EquipMutation(m, slot);
//                 else previewMutator.UnequipMutation(slot);
//             }
//         }
//
//         SetLayerRecursively(_previewInstance, _previewLayer);
//         if (previewCamera != null) previewCamera.enabled = true;
//     }
//
//     private void SanitizePreviewClone(GameObject clone)
//     {
//         foreach (var brain in clone.GetComponentsInChildren<CreatureBrain>(true))
//             brain.enabled = false;
//
//         foreach (var agent in clone.GetComponentsInChildren<NavMeshAgent>(true))
//             agent.enabled = false;
//
//         foreach (var creature in clone.GetComponentsInChildren<Creature>(true))
//             creature.enabled = false;
//
//         foreach (var selectable in clone.GetComponentsInChildren<Selectable>(true))
//             selectable.enabled = false;
//
//         foreach (var bob in clone.GetComponentsInChildren<ToyBob>(true))
//             bob.enabled = false;
//
//         foreach (var rb in clone.GetComponentsInChildren<Rigidbody>(true))
//             rb.isKinematic = true;
//
//         foreach (var outline in clone.GetComponentsInChildren<Outline>(true))
//             outline.enabled = false;
//
//         foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
//             r.enabled = true;
//     }
//
//     private static void SetLayerRecursively(GameObject obj, int layer)
//     {
//         if (layer < 0) return;
//         obj.layer = layer;
//         foreach (Transform child in obj.transform)
//             SetLayerRecursively(child.gameObject, layer);
//     }
//
//     private void SetupViewportOrbit(VisualElement viewport)
//     {
//         bool isDragging = false;
//         viewport.RegisterCallback<PointerDownEvent>(_ => isDragging = true);
//         viewport.RegisterCallback<PointerUpEvent>(_ => isDragging = false);
//         viewport.RegisterCallback<PointerLeaveEvent>(_ => isDragging = false);
//         viewport.RegisterCallback<PointerMoveEvent>(evt =>
//         {
//             if (isDragging && previewSpawnPoint != null)
//                 previewSpawnPoint.Rotate(Vector3.up, -evt.deltaPosition.x * 0.6f, Space.World);
//         });
//     }
//
//     private string FormatAllowedSlots(SlotType flags)
//     {
//         if (flags == SlotType.None) return "None";
//
//         List<string> names = new();
//         foreach (SlotType slot in _slotOrder)
//         {
//             if ((flags & slot) != 0)
//                 names.Add(slot.ToString());
//         }
//         return names.Count > 0 ? string.Join(", ", names) : flags.ToString();
//     }
//
//     private string BuildEffectSummary(MutationData m)
//     {
//         List<string> effects = new();
//
//         if (m.bonusMaxHealth != 0)
//             effects.Add($"{(m.bonusMaxHealth > 0 ? "+" : "")}{m.bonusMaxHealth} Max Health");
//
//         if (m.damageResistance != 0f)
//             effects.Add($"{(m.damageResistance > 0 ? "+" : "")}{m.damageResistance:0}% Damage Resistance");
//
//         if (m.bonusMoveSpeed != 0f)
//             effects.Add($"{(m.bonusMoveSpeed > 0 ? "+" : "")}{m.bonusMoveSpeed:0.##}x Move Speed");
//
//         if (m.bonusAttackDamage != 0)
//             effects.Add($"{(m.bonusAttackDamage > 0 ? "+" : "")}{m.bonusAttackDamage} Attack Damage");
//
//         if (m.bonusAttackRange != 0f)
//             effects.Add($"{(m.bonusAttackRange > 0 ? "+" : "")}{m.bonusAttackRange * 100f:0}% Attack Range");
//
//         if (m.bonusAttackSpeed != 0f)
//             effects.Add($"{(m.bonusAttackSpeed > 0 ? "+" : "")}{m.bonusAttackSpeed:0.##} Attack Speed");
//
//         if (m.bonusViewRadius != 0f)
//             effects.Add($"{(m.bonusViewRadius > 0 ? "+" : "")}{m.bonusViewRadius:0.#} Vision Radius");
//
//         if (m.grantedCapabilities != 0)
//         {
//             foreach (CreatureCapabilities cap in System.Enum.GetValues(typeof(CreatureCapabilities)))
//             {
//                 if (cap == 0) continue;
//                 if ((m.grantedCapabilities & cap) != 0)
//                     effects.Add($"Grants {FormatEnumName(cap.ToString())}");
//             }
//         }
//
//         if (m.overridesAttack)
//             effects.Add("Replaces Basic Attack");
//
//         return effects.Count > 0 ? string.Join(", ", effects) : "No stat modifiers";
//     }
//
//     private static string FormatEnumName(string rawName)
//     {
//         return System.Text.RegularExpressions.Regex.Replace(rawName, "(\\B[A-Z])", " $1");
//     }
// }