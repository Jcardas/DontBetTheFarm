using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class PenAnimalsTab
{
    private const int HealFallbackCost = 10;
    private const int MutateHayPerSlot = 10;
    private static readonly SlotType[] Slots = { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail };

    private readonly PenUIController _ui;
    private readonly CreaturePreviewBooth _booth;
    private readonly VisualTreeAsset _iconTemplate;
    private readonly Dictionary<SlotType, MutationData> _staged = new();

    private int _index;
    private SlotType _activeSlot = SlotType.Head;

    private readonly Label _countLbl, _nameLbl, _hpLbl, _healCostLbl, _hayCostLbl, _techCostLbl;
    private readonly Label _lvlLbl, _hpStatLbl, _spdLbl, _meleeLbl, _rangedLbl;
    private readonly VisualElement _hpFill, _slotsBox, _gridContainer, _selectedCard;
    private readonly Button _healBtn, _cancelBtn, _mutateBtn;
    private readonly Dictionary<SlotType, (Label name, Label desc)> _slotText = new();

    public PenAnimalsTab(PenUIController ui, VisualElement root, CreaturePreviewBooth booth, VisualTreeAsset iconTemplate)
    {
        _ui = ui;
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
            _slotText[s] = (root.Q<Label>($"{s}MutationName"), root.Q<Label>($"{s}MutationDesc"));

        if (_healBtn != null) _healBtn.clicked += Heal;
        if (_cancelBtn != null) _cancelBtn.clicked += () => { _staged.Clear(); Refresh(); };
        if (_mutateBtn != null) _mutateBtn.clicked += ApplyMutations;
    }

    public void ResetState() { _index = 0; _staged.Clear(); }

    private void Cycle(int dir)
    {
        if (_ui.Animals.Count == 0) return;
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
        if (_slotsBox != null)
        {
            var boxes = _slotsBox.Query<TemplateContainer>().ToList();
            for (int i = 0; i < Slots.Length && i < boxes.Count; i++)
            {
                SlotType slot = Slots[i];
                var box = boxes[i];
                box.userData = slot;
                box.UnregisterCallback<ClickEvent>(OnSlotClick);
                box.RegisterCallback<ClickEvent>(OnSlotClick);

                var m = Effective(mutator, slot);
                var img = box.Q<Image>();
                if (img != null)
                    img.sprite = m?.prefabSprite != null ? m.prefabSprite : GameConstants.Instance.DefaultSprite;

                box.EnableInClassList("slot-active", slot == _activeSlot);
                box.EnableInClassList("slot-staged", _staged.ContainsKey(slot));
            }
        }

        // Unlocked Mutations Horizontal Scroll Grid
        if (_gridContainer != null)
        {
            _gridContainer.Clear();
            var currentEquipped = Effective(mutator, _activeSlot);
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

                    bool valid = (m.allowedSlots & _activeSlot) != 0 && currentEquipped != m;
                    item.SetEnabled(valid);
                    item.style.opacity = valid ? 1f : 0.35f;
                    item.RegisterCallback<PointerEnterEvent>(_ => ShowDetails(m));
                    if (valid) item.RegisterCallback<ClickEvent>(_ => { _staged[_activeSlot] = m; Refresh(); });
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
        int hay = _staged.Count * MutateHayPerSlot;
        int tech = _staged.Values.Sum(m => m.mutationCost);
        if (_hayCostLbl != null) _hayCostLbl.text = hay.ToString();
        if (_techCostLbl != null) _techCostLbl.text = tech.ToString();
        _cancelBtn?.SetEnabled(_staged.Count > 0);
        _mutateBtn?.SetEnabled(_staged.Count > 0 && ResourceManager.Count(ResourceType.Hay) >= hay && ResourceManager.Count(ResourceType.Technology) >= tech);

        ShowDetails(Effective(mutator, _activeSlot));
        _booth?.ShowLive(animal, s => Effective(mutator, s));
    }

    private void OnSlotClick(ClickEvent evt)
    {
        if (evt.currentTarget is VisualElement el && el.userData is SlotType s) { _activeSlot = s; Refresh(); }
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
        int hay = _staged.Count * MutateHayPerSlot;
        int tech = _staged.Values.Sum(m => m.mutationCost);
        if (ResourceManager.Count(ResourceType.Hay) < hay || ResourceManager.Count(ResourceType.Technology) < tech) return;

        ResourceManager.Spend(ResourceType.Hay, hay);
        ResourceManager.Spend(ResourceType.Technology, tech);

        var animal = _ui.Animals[_index];
        if (animal.TryGetComponent<CreatureMutator>(out var mut))
            foreach (var kv in _staged) mut.EquipMutation(kv.Value, kv.Key);

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
        labels[4].text = m != null ? Describe(m) : "Hover or click a mutation below";
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