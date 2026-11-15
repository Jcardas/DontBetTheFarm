using UnityEngine;
using UnityEngine.UIElements;

public class PenBreedingTab
{
    private static readonly SlotType[] Slots = { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail };

    private readonly PenUIController _ui;
    private readonly CreaturePreviewBooth _booth;
    private readonly VisualTreeAsset _cardTemplate;
    private readonly VisualElement _slotA, _slotB;
    private readonly Button _breedBtn;
    private readonly Label _costLbl, _countLbl;
    private readonly ScrollView _rosterScroll;

    private Animal _parentA, _parentB;

    public PenBreedingTab(PenUIController ui, VisualElement root, CreaturePreviewBooth booth, VisualTreeAsset cardTemplate)
    {
        _ui = ui;
        _booth = booth;
        _cardTemplate = cardTemplate;

        _slotA = root.Q("ParentSlotA");
        _slotB = root.Q("ParentSlotB");
        _breedBtn = root.Q<Button>("BreedButton");
        _costLbl = root.Q<Label>("BreedCostLabel");
        _countLbl = root.Q<Label>("PenRosterCountLabel");
        _rosterScroll = root.Q<ScrollView>("AnimalsInPenScrollbar");

        _breedBtn.clicked += () =>
        {
            if (_ui.ActivePen?.BreedPair(_parentA, _parentB) != null) { ResetState(); Refresh(); }
        };
    }

    public void ResetState() { _parentA = null; _parentB = null; }

    public void Refresh()
    {
        _ui.SyncAnimals();
        if (_parentA != null && !Contains(_parentA)) _parentA = null;
        if (_parentB != null && !Contains(_parentB)) _parentB = null;

        _countLbl.text = $"{_ui.Animals.Count}/{_ui.Capacity}";
        int cost = _parentA != null ? _parentA.BreedingCost : 10;
        bool canBreed = _ui.ActivePen != null && _ui.ActivePen.CanBreedPair(_parentA, _parentB, out cost);

        _costLbl.text = cost.ToString();
        _breedBtn.SetEnabled(canBreed);

        FillParentSlot(_slotA, _parentA, true);
        FillParentSlot(_slotB, _parentB, false);

        _rosterScroll.Clear();
        for (int i = 0; i < _ui.Capacity; i++)
        {
            if (i < _ui.Animals.Count)
            {
                var animal = _ui.Animals[i];
                var card = BuildCard(animal, animal == _parentA || animal == _parentB, false);
                card.style.marginRight = 10;
                card.style.flexShrink = 0;
                card.RegisterCallback<ClickEvent>(_ => ToggleParent(animal));
                _rosterScroll.Add(card);
            }
            else
            {
                var empty = _cardTemplate.Instantiate();
                empty.style.marginRight = 10;
                empty.style.flexShrink = 0;
                empty.style.opacity = 0.55f;
                empty.Q<Label>("AnimalNameLabel").text = "";
                empty.Q("MiniMutationsRow").style.display = DisplayStyle.None;
                var portrait = empty.Q("AnimalPortrait");
                portrait.style.backgroundImage = StyleKeyword.None;
                portrait.style.justifyContent = Justify.Center;
                portrait.style.alignItems = Align.Center;
                portrait.Add(new Label("?") { style = { fontSize = 64, color = Color.white, unityFontStyleAndWeight = FontStyle.Bold } });
                empty.SetEnabled(false);
                _rosterScroll.Add(empty);
            }
        }
    }

    private void FillParentSlot(VisualElement container, Animal parent, bool isA)
    {
        container.Clear();
        if (parent == null) return;
        var card = BuildCard(parent, true, true);
        card.RegisterCallback<ClickEvent>(_ => { if (isA) _parentA = null; else _parentB = null; Refresh(); });
        container.Add(card);
    }

    private VisualElement BuildCard(Animal animal, bool selected, bool fill)
    {
        var card = _cardTemplate.Instantiate();
        var root = card.Q("AnimalCardRoot") ?? card;
        if (fill)
        {
            card.style.width = root.style.width = Length.Percent(100);
            card.style.height = root.style.height = Length.Percent(100);
            root.style.marginBottom = 0;
        }

        card.Q<Label>("AnimalNameLabel").text = animal.GetCreatureName();
        var tex = _booth?.GetPortrait(animal);
        if (tex != null) card.Q("AnimalPortrait").style.backgroundImage = new StyleBackground(tex);

        var miniSlots = card.Query("MiniMutationSlot").ToList();
        animal.TryGetComponent<CreatureMutator>(out var mut);
        for (int i = 0; i < Slots.Length && i < miniSlots.Count; i++)
        {
            var m = mut?.GetEquippedMutation(Slots[i]);
            miniSlots[i].style.backgroundImage = m?.prefabSprite != null ? new StyleBackground(m.prefabSprite) : StyleKeyword.None;
        }

        root.style.backgroundColor = selected ? Color.white : new Color(1f, 1f, 1f, 0.15f);
        return card;
    }

    private void ToggleParent(Animal a)
    {
        if (_parentA == a) _parentA = null;
        else if (_parentB == a) _parentB = null;
        else if (_parentA == null) _parentA = a;
        else _parentB = a;
        Refresh();
    }

    private bool Contains(Animal target)
    {
        for (int i = 0; i < _ui.Animals.Count; i++)
            if (_ui.Animals[i] == target) return true;
        return false;
    }
}