using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class PenAnimalsTab
{
    private const int HealFallbackCost = 10;
    private const int MutateHayPerSlot = 10;
    private const float DragThreshold = 8f;
    private static readonly SlotType[] Slots = { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail };

    private readonly PenUIController _ui;
    private readonly CreaturePreviewBooth _booth;
    private readonly VisualTreeAsset _iconTemplate;
    private readonly VisualElement _root;

    // A null value in _staged explicitly represents a staged removal of an equipped mutation
    private readonly Dictionary<SlotType, MutationData> _staged = new();
    private readonly Dictionary<SlotType, VisualElement> _slotBoxes = new();
    private readonly Dictionary<SlotType, Sprite> _defaultSlotSprites = new();

    private int _index;

    // Drag-and-drop state
    private Image _dragGhost;
    private MutationData _draggingMutation;
    private SlotType? _dragSourceSlot; // Null when dragging from unlocked inventory
    private Vector2 _dragStartPos;
    private bool _isDragging;

    private readonly Label _countLbl, _nameLbl, _hpLbl, _healCostLbl, _hayCostLbl, _techCostLbl;
    private readonly Label _lvlLbl, _hpStatLbl, _spdLbl, _meleeLbl, _rangedLbl;
    private readonly VisualElement _hpFill, _slotsBox, _gridContainer, _selectedCard;
    private readonly Button _healBtn, _cancelBtn, _mutateBtn;
    private readonly Dictionary<SlotType, (Label name, Label desc)> _slotText = new();

    public PenAnimalsTab(PenUIController ui, VisualElement root, CreaturePreviewBooth booth, VisualTreeAsset iconTemplate)
    {
        _ui = ui;
        _root = root;
        _booth = booth;
        _iconTemplate = iconTemplate;

        var preview = root.Q("CreaturePreview") ?? root;
        _booth?.BindViewport(preview.Q("Creature3DRender") ?? root.Q("Creature3DRender"));

        var prevBtn = preview.Q<Button>("Back");
        var nextBtn = preview.Q<Button>("Forward");
        if (prevBtn != null) prevBtn.clicked += () => Cycle(-1);
        if (nextBtn != null) nextBtn.clicked += () => Cycle(1);

        _countLbl = root.Q<Label>("AnimalCountLabel");
        _nameLbl = root.Q<Label>("AnimalNameLabel");
        _hpLbl = root.Q<Label>("HPValueLabel");
        _hpFill = root.Q("HPFillBar");
        _healBtn = root.Q<Button>("HealButton");
        _healCostLbl = root.Q<Label>("HealCostLabel");
        _slotsBox = root.Q("EquippedSlotsContainer");
        _hayCostLbl = root.Q<Label>("MutationHayCost");
        _techCostLbl = root.Q<Label>("MutationTechCost");
        _cancelBtn = root.Q<Button>("CancelMutateButton");
        _mutateBtn = root.Q<Button>("ConfirmMutateButton");
        _selectedCard = root.Q("SelectedMutationCard");

        var scroll = root.Q<ScrollView>("UnlockedMutationsScroll");
        _gridContainer = root.Q("UnlockedMutationsGrid") ?? scroll?.contentContainer;
        _gridContainer?.Clear();

        _lvlLbl = root.Q<Label>("LevelValue");
        _hpStatLbl = root.Q<Label>("MaxHealthValue");
        _spdLbl = root.Q<Label>("SpeedValue");
        _meleeLbl = root.Q<Label>("MeleeDamageValue");
        _rangedLbl = root.Q<Label>("RangedDamageValue");

        foreach (var s in Slots)
        {
            _slotText[s] = (root.Q<Label>($"{s}MutationName"), root.Q<Label>($"{s}MutationDesc"));
            var box = _slotsBox?.Q($"{s}SlotIcon");
            if (box != null)
            {
                _slotBoxes[s] = box;
                box.userData = s;

                // Cache the slot's original placeholder icon (head, back, limbs, tail) from UXML
                var img = box.Q<Image>();
                if (img != null)
                    _defaultSlotSprites[s] = img.sprite;

                SetupSlotHandlers(box, s);
            }
        }

        // Floating drag ghost
        _dragGhost = new Image
        {
            pickingMode = PickingMode.Ignore,
            style =
            {
                position = Position.Absolute,
                width = 96,
                height = 96,
                opacity = 0.85f,
                display = DisplayStyle.None
            }
        };
        _root.Add(_dragGhost);

        if (_healBtn != null) _healBtn.clicked += Heal;
        if (_cancelBtn != null) _cancelBtn.clicked += () => { _staged.Clear(); Refresh(); };
        if (_mutateBtn != null) _mutateBtn.clicked += ApplyMutations;
    }

    public void ResetState()
    {
        CancelDrag();
        _index = 0;
        _staged.Clear();
    }

    private void Cycle(int dir)
    {
        if (_ui.Animals.Count == 0) return;
        CancelDrag();
        _staged.Clear();
        _index = (_index + dir + _ui.Animals.Count) % _ui.Animals.Count;
        Refresh();
    }

    public void Refresh()
    {
        _ui.SyncAnimals();
        if (_ui.Animals.Count == 0)
        {
            if (_countLbl != null) _countLbl.text = $"0/{_ui.Capacity}";
            if (_nameLbl != null) _nameLbl.text = "No Animals in Pen";
            _booth?.ClearLive();
            ShowDetails(null);
            return;
        }

        _index = Mathf.Clamp(_index, 0, _ui.Animals.Count - 1);
        Animal animal = _ui.Animals[_index];
        animal.TryGetComponent<CreatureMutator>(out var mutator);

        if (_countLbl != null) _countLbl.text = $"{_index + 1}/{_ui.Capacity}";
        if (_nameLbl != null) _nameLbl.text = animal.GetCreatureName();

        // Health
        float ratio = animal.MaxHealth > 0 ? Mathf.Clamp01(animal.CurrentHealth / animal.MaxHealth) : 1f;
        if (_hpFill != null) _hpFill.style.width = Length.Percent(ratio * 100f);
        if (_hpLbl != null) _hpLbl.text = $"HP: {Mathf.CeilToInt(animal.CurrentHealth)}/{Mathf.CeilToInt(animal.MaxHealth)}";
        int healCost = animal.HealCost > 0 ? animal.HealCost : HealFallbackCost;
        if (_healCostLbl != null) _healCostLbl.text = healCost.ToString();
        _healBtn?.SetEnabled(animal.CurrentHealth < animal.MaxHealth && ResourceManager.Count(ResourceType.Hay) >= healCost);

        // 4 Equipped Slots
        foreach (var slot in Slots)
        {
            if (!_slotBoxes.TryGetValue(slot, out var box)) continue;

            var m = Effective(mutator, slot);
            var img = box.Q<Image>();
            if (img != null)
            {
                _defaultSlotSprites.TryGetValue(slot, out var slotPlaceholder);
                img.sprite = m?.prefabSprite != null ? m.prefabSprite : slotPlaceholder;
            }

            box.RemoveFromClassList("slot-active");
            box.EnableInClassList("slot-staged", _staged.ContainsKey(slot));
            box.EnableInClassList("slot-staged-remove", _staged.TryGetValue(slot, out var stagedVal) && stagedVal == null);
        }

        // Unlocked Mutations Horizontal Scroll Grid
        if (_gridContainer != null)
        {
            _gridContainer.Clear();
            if (MutationUnlockManager.Instance != null && _iconTemplate != null)
            {
                foreach (var m in MutationUnlockManager.Instance.UnlockedMutations.Where(x => x != null))
                {
                    var item = _iconTemplate.Instantiate();
                    item.style.width = 128;
                    item.style.height = 128;
                    item.style.flexShrink = 0;

                    var img = item.Q<Image>();
                    if (img != null)
                        img.sprite = m.prefabSprite != null ? m.prefabSprite : GameConstants.Instance.DefaultSprite;

                    bool canEquipAnywhere = Slots.Any(s => CanEquipInSlot(m, s, mutator));
                    item.style.opacity = canEquipAnywhere ? 1f : 0.45f;

                    item.RegisterCallback<PointerEnterEvent>(_ => { if (!_isDragging) ShowDetails(m); });
                    SetupInventoryDragHandlers(item, m, mutator);
                    _gridContainer.Add(item);
                }
            }
        }

        // Projected Stats & Equipped List
        int bonusHp = 0, bonusDmg = 0;
        float bonusSpd = 0f;
        foreach (var s in Slots)
        {
            var m = Effective(mutator, s);
            if (_slotText[s].name != null) _slotText[s].name.text = m != null ? m.mutationName : "None";
            if (_slotText[s].desc != null) _slotText[s].desc.text = m != null ? $"→ {Describe(m)}" : "→ Empty slot";
            if (m != null) { bonusHp += m.bonusMaxHealth; bonusDmg += m.bonusAttackDamage; bonusSpd += m.bonusMoveSpeed; }
        }
        if (_lvlLbl != null) _lvlLbl.text = "1";
        if (_hpStatLbl != null) _hpStatLbl.text = Mathf.RoundToInt(animal.MaxHealth + bonusHp).ToString();
        if (_spdLbl != null) _spdLbl.text = (animal.CalculatedMoveSpeed + bonusSpd).ToString("0.#");
        if (_meleeLbl != null) _meleeLbl.text = Mathf.RoundToInt(animal.CalculatedAttackDamage + bonusDmg).ToString();
        if (_rangedLbl != null) _rangedLbl.text = "0";

        // Cost Box & 3D Preview
        var addedMutations = _staged.Values.Where(m => m != null).ToList();
        int hay = addedMutations.Count * MutateHayPerSlot;
        int tech = addedMutations.Sum(m => m.mutationCost);

        if (_hayCostLbl != null) _hayCostLbl.text = hay.ToString();
        if (_techCostLbl != null) _techCostLbl.text = tech.ToString();
        _cancelBtn?.SetEnabled(_staged.Count > 0);
        _mutateBtn?.SetEnabled(_staged.Count > 0 && ResourceManager.Count(ResourceType.Hay) >= hay && ResourceManager.Count(ResourceType.Technology) >= tech);

        // Show first equipped/staged mutation or default prompt
        var defaultPreview = Slots.Select(s => Effective(mutator, s)).FirstOrDefault(m => m != null);
        ShowDetails(defaultPreview);
        _booth?.ShowLive(animal, s => Effective(mutator, s));
    }

    // Dragging FROM the unlocked mutations bar
    private void SetupInventoryDragHandlers(VisualElement item, MutationData mutation, CreatureMutator mutator)
    {
        item.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            BeginPointerTracking(item, evt.pointerId, evt.position, mutation, sourceSlot: null);
            ShowDetails(mutation);
        });

        item.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_draggingMutation != mutation || _dragSourceSlot != null || !item.HasPointerCapture(evt.pointerId)) return;
            HandlePointerMove(evt.position, mutator);
        });

        item.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (_draggingMutation != mutation || _dragSourceSlot != null) return;
            if (item.HasPointerCapture(evt.pointerId))
                item.ReleasePointer(evt.pointerId);

            bool wasDragging = _isDragging;
            CancelDrag();

            if (wasDragging)
            {
                SlotType? hoveredSlot = GetSlotUnderPointer(evt.position);
                if (hoveredSlot.HasValue && CanEquipInSlot(mutation, hoveredSlot.Value, mutator))
                {
                    StageMutation(hoveredSlot.Value, mutation, mutator);
                    Refresh();
                }
            }
            else
            {
                // Click fallback: equip into the first valid allowed slot
                var targetSlot = Slots.Cast<SlotType?>().FirstOrDefault(s => CanEquipInSlot(mutation, s.Value, mutator));
                if (targetSlot.HasValue)
                {
                    StageMutation(targetSlot.Value, mutation, mutator);
                    Refresh();
                }
            }
        });

        item.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (_draggingMutation == mutation && _dragSourceSlot == null)
                CancelDrag();
        });
    }

    // Hover & Dragging FROM an equipped/staged slot
    private void SetupSlotHandlers(VisualElement box, SlotType slot)
    {
        box.RegisterCallback<PointerEnterEvent>(_ =>
        {
            if (_isDragging || _ui.Animals.Count == 0) return;
            _ui.Animals[_index].TryGetComponent<CreatureMutator>(out var mutator);
            ShowDetails(Effective(mutator, slot));
        });

        box.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || _ui.Animals.Count == 0) return;
            _ui.Animals[_index].TryGetComponent<CreatureMutator>(out var mutator);
            var current = Effective(mutator, slot);

            if (current == null) return;

            BeginPointerTracking(box, evt.pointerId, evt.position, current, sourceSlot: slot);
            ShowDetails(current);
        });

        box.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_dragSourceSlot != slot || !box.HasPointerCapture(evt.pointerId)) return;
            _ui.Animals[_index].TryGetComponent<CreatureMutator>(out var mutator);
            HandlePointerMove(evt.position, mutator);
        });

        box.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (_dragSourceSlot != slot) return;
            if (box.HasPointerCapture(evt.pointerId))
                box.ReleasePointer(evt.pointerId);

            var mutation = _draggingMutation;
            bool wasDragging = _isDragging;
            CancelDrag();

            if (!wasDragging || _ui.Animals.Count == 0 || mutation == null) return;

            _ui.Animals[_index].TryGetComponent<CreatureMutator>(out var mutator);
            SlotType? hoveredSlot = GetSlotUnderPointer(evt.position);

            // Dropped back onto the same slot -> do nothing
            if (hoveredSlot == slot) return;

            // Dropped onto a DIFFERENT slot -> move it only if allowed and creature supports that slot
            if (hoveredSlot.HasValue)
            {
                if (CanEquipInSlot(mutation, hoveredSlot.Value, mutator))
                {
                    UnstageOrRemoveSlot(slot, mutator);
                    StageMutation(hoveredSlot.Value, mutation, mutator);
                    Refresh();
                }
                return;
            }

            // Dropped outside any slot (off the creature) -> unequip/remove
            UnstageOrRemoveSlot(slot, mutator);
            Refresh();
        });

        box.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (_dragSourceSlot == slot)
                CancelDrag();
        });
    }

    private void BeginPointerTracking(VisualElement target, int pointerId, Vector2 startPos, MutationData mutation, SlotType? sourceSlot)
    {
        _draggingMutation = mutation;
        _dragSourceSlot = sourceSlot;
        _dragStartPos = startPos;
        _isDragging = false;
        target.CapturePointer(pointerId);
    }

    private void HandlePointerMove(Vector2 worldPointerPos, CreatureMutator mutator)
    {
        if (!_isDragging && Vector2.Distance(worldPointerPos, _dragStartPos) >= DragThreshold)
        {
            _isDragging = true;
            _dragGhost.sprite = _draggingMutation.prefabSprite != null
                ? _draggingMutation.prefabSprite
                : GameConstants.Instance.DefaultSprite;
            _dragGhost.style.display = DisplayStyle.Flex;
            _dragGhost.BringToFront();
        }

        if (_isDragging)
        {
            UpdateGhostPosition(worldPointerPos);
            HighlightDropTargets(_draggingMutation, mutator, worldPointerPos);
        }
    }

    private void UpdateGhostPosition(Vector2 worldPointerPos)
    {
        Vector2 localPos = _root.WorldToLocal(worldPointerPos);
        _dragGhost.style.left = localPos.x - 48f;
        _dragGhost.style.top = localPos.y - 48f;
    }

    private void HighlightDropTargets(MutationData mutation, CreatureMutator mutator, Vector2 worldPointerPos)
    {
        SlotType? hovered = GetSlotUnderPointer(worldPointerPos);
        foreach (var kv in _slotBoxes)
        {
            if (_dragSourceSlot == kv.Key)
            {
                kv.Value.RemoveFromClassList("slot-drop-valid");
                kv.Value.RemoveFromClassList("slot-drop-invalid");
                kv.Value.RemoveFromClassList("slot-drop-hover");
                continue;
            }

            bool allowed = IsSlotAllowed(mutation, kv.Key, mutator);
            bool valid = CanEquipInSlot(mutation, kv.Key, mutator);

            kv.Value.EnableInClassList("slot-drop-valid", valid);
            kv.Value.EnableInClassList("slot-drop-invalid", !allowed);
            kv.Value.EnableInClassList("slot-drop-hover", valid && hovered == kv.Key);
        }
    }

    private SlotType? GetSlotUnderPointer(Vector2 worldPointerPos)
    {
        foreach (var kv in _slotBoxes)
        {
            if (kv.Value.worldBound.Contains(worldPointerPos))
                return kv.Key;
        }
        return null;
    }

    private static bool IsSlotAllowed(MutationData m, SlotType slot, CreatureMutator mutator) =>
        m != null && (mutator != null ? mutator.CanEquip(m, slot) : (m.allowedSlots & slot) != 0);

    private bool CanEquipInSlot(MutationData m, SlotType slot, CreatureMutator mutator) =>
        IsSlotAllowed(m, slot, mutator) && Effective(mutator, slot) != m;

    private void StageMutation(SlotType slot, MutationData mutation, CreatureMutator mutator)
    {
        if (!IsSlotAllowed(mutation, slot, mutator)) return;

        if (mutator != null && mutator.GetEquippedMutation(slot) == mutation)
            _staged.Remove(slot);
        else
            _staged[slot] = mutation;
    }

    private void UnstageOrRemoveSlot(SlotType slot, CreatureMutator mutator)
    {
        var currentlyEquipped = mutator?.GetEquippedMutation(slot);

        if (_staged.ContainsKey(slot) && _staged[slot] != null)
        {
            _staged.Remove(slot);
        }
        else if (currentlyEquipped != null)
        {
            _staged[slot] = null;
        }
    }

    private void CancelDrag()
    {
        _draggingMutation = null;
        _dragSourceSlot = null;
        _isDragging = false;
        if (_dragGhost != null)
            _dragGhost.style.display = DisplayStyle.None;

        foreach (var box in _slotBoxes.Values)
        {
            box.RemoveFromClassList("slot-drop-valid");
            box.RemoveFromClassList("slot-drop-invalid");
            box.RemoveFromClassList("slot-drop-hover");
        }
    }

    private MutationData Effective(CreatureMutator mut, SlotType slot) =>
        _staged.TryGetValue(slot, out var m) ? m : mut?.GetEquippedMutation(slot);

    private void Heal()
    {
        if (_ui.Animals.Count == 0) return;
        var a = _ui.Animals[_index];
        bool healed = _ui.ActivePen != null
            ? _ui.ActivePen.TryHealSingleAnimal(a)
            : (a.CurrentHealth < a.MaxHealth && ResourceManager.Spend(ResourceType.Hay, a.HealCost > 0 ? a.HealCost : HealFallbackCost));
        if (healed) { if (_ui.ActivePen == null) a.HealToFull(); Refresh(); }
    }

    private void ApplyMutations()
    {
        if (_ui.Animals.Count == 0 || _staged.Count == 0) return;

        var addedMutations = _staged.Values.Where(m => m != null).ToList();
        int hay = addedMutations.Count * MutateHayPerSlot;
        int tech = addedMutations.Sum(m => m.mutationCost);
        if (ResourceManager.Count(ResourceType.Hay) < hay || ResourceManager.Count(ResourceType.Technology) < tech) return;

        ResourceManager.Spend(ResourceType.Hay, hay);
        ResourceManager.Spend(ResourceType.Technology, tech);

        var animal = _ui.Animals[_index];
        if (animal.TryGetComponent<CreatureMutator>(out var mut))
        {
            foreach (var kv in _staged)
            {
                if (kv.Value == null)
                {
                    mut.UnequipMutation(kv.Key);
                }
                else if (mut.CanEquip(kv.Value, kv.Key))
                {
                    mut.EquipMutation(kv.Value, kv.Key);
                }
            }
        }

        _booth?.InvalidatePortrait(animal);
        _staged.Clear();
        Refresh();
    }

    private void ShowDetails(MutationData m)
    {
        if (_selectedCard == null) return;
        var img = _selectedCard.Q<Image>();
        if (img != null)
            img.sprite = m?.prefabSprite != null ? m.prefabSprite : GameConstants.Instance.DefaultSprite;

        var costSection = _selectedCard.Q("Cost");
        if (costSection != null) costSection.style.display = DisplayStyle.None;

        var labels = _selectedCard.Query<Label>().ToList();
        if (labels.Count < 5) return;
        labels[0].style.fontSize = 20;
        labels[0].style.whiteSpace = WhiteSpace.Normal;
        labels[0].text = m != null ? m.mutationName : "Select a Mutation";
        labels[2].text = m != null ? string.Join(", ", Slots.Where(s => (m.allowedSlots & s) != 0)) : "None";
        labels[4].text = m != null ? Describe(m) : "Hover or drag a mutation";
    }

    private static string Describe(MutationData m)
    {
        var list = new List<string>();
        if (m.bonusMaxHealth != 0) list.Add($"{m.bonusMaxHealth:+#;-#} Max Health");
        if (m.damageResistance != 0) list.Add($"{m.damageResistance:+#;-#}% Resistance");
        if (m.bonusMoveSpeed != 0) list.Add($"{m.bonusMoveSpeed:+#.##;-#.##}x Speed");
        if (m.bonusAttackDamage != 0) list.Add($"{m.bonusAttackDamage:+#;-#} Damage");
        if (m.bonusAttackRange != 0) list.Add($"{m.bonusAttackRange * 100f:+#;-#}% Range");
        if (m.bonusAttackSpeed != 0) list.Add($"{m.bonusAttackSpeed:+#.##;-#.##} Attack Speed");
        if (m.overridesAttack) list.Add("Replaces Basic Attack");
        return list.Count > 0 ? string.Join(", ", list) : "Special capability";
    }
}