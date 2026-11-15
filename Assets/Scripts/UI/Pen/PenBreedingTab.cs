using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PenBreedingTab
{
    private const float DragThreshold = 8f;
    private static readonly SlotType[] Slots = { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail };

    // Theme colors matching InteractionUI.uss
    private static readonly Color CardSelectedBg     = new Color(42f / 255f, 72f / 255f, 53f / 255f, 0.95f);
    private static readonly Color CardDefaultBg      = new Color(22f / 255f, 38f / 255f, 32f / 255f, 0.75f);
    private static readonly Color CardSelectedBorder = new Color(110f / 255f, 220f / 255f, 130f / 255f, 1f);
    private static readonly Color CardDefaultBorder  = new Color(1f, 1f, 1f, 0.18f);

    private readonly PenUIController _ui;
    private readonly VisualElement _root;
    private readonly CreaturePreviewBooth _booth;
    private readonly VisualTreeAsset _cardTemplate;
    private readonly VisualElement _slotA, _slotB;
    private readonly Button _breedBtn;
    private readonly Label _costLbl, _countLbl;
    private readonly ScrollView _rosterScroll;

    private Animal _parentA, _parentB;

    // Drag-and-drop state
    private VisualElement _dragGhost;
    private Animal _draggingAnimal;
    private int? _dragSourceSlot; // null = roster, 0 = ParentSlotA, 1 = ParentSlotB
    private Vector2 _dragStartPos;
    private bool _isDragging;

    public PenBreedingTab(PenUIController ui, VisualElement root, CreaturePreviewBooth booth, VisualTreeAsset cardTemplate)
    {
        _ui = ui;
        _root = root;
        _booth = booth;
        _cardTemplate = cardTemplate;

        _slotA = root.Q("ParentSlotA");
        _slotB = root.Q("ParentSlotB");
        _breedBtn = root.Q<Button>("BreedButton");
        _costLbl = root.Q<Label>("BreedCostLabel");
        _countLbl = root.Q<Label>("PenRosterCountLabel");
        _rosterScroll = root.Q<ScrollView>("AnimalsInPenScrollbar");

        // Floating drag ghost matching the card portrait
        _dragGhost = new VisualElement
        {
            pickingMode = PickingMode.Ignore,
            style =
            {
                position = Position.Absolute,
                width = 175,
                height = 185,
                opacity = 0.88f,
                display = DisplayStyle.None,
                backgroundColor = CardSelectedBg,
                unityBackgroundScaleMode = ScaleMode.ScaleToFit,
                borderTopWidth = 2, borderBottomWidth = 2, borderLeftWidth = 2, borderRightWidth = 2,
                borderTopColor = CardSelectedBorder, borderBottomColor = CardSelectedBorder,
                borderLeftColor = CardSelectedBorder, borderRightColor = CardSelectedBorder,
                borderTopLeftRadius = 10, borderTopRightRadius = 10,
                borderBottomLeftRadius = 10, borderBottomRightRadius = 10
            }
        };
        _root.Add(_dragGhost);

        _breedBtn.clicked += () =>
        {
            if (_ui.ActivePen?.BreedPair(_parentA, _parentB) != null)
            {
                ResetState();
                Refresh();
            }
        };
    }

    public void ResetState()
    {
        CancelDrag();
        _parentA = null;
        _parentB = null;
    }

    public void Refresh()
    {
        _ui.SyncAnimals();

        // Deselect any parent that left the pen or was knocked out
        if (_parentA != null && (!Contains(_parentA) || _parentA.Down)) _parentA = null;
        if (_parentB != null && (!Contains(_parentB) || _parentB.Down)) _parentB = null;

        _countLbl.text = $"{_ui.Animals.Count}/{_ui.Capacity}";

        int displayCost = _ui.ActivePen != null
            ? _ui.ActivePen.CalculateBreedingCost(_parentA, _parentB)
            : (_parentA != null ? _parentA.BreedingCost : (_parentB != null ? _parentB.BreedingCost : 0));
        
        int actualCost = displayCost;
        bool parentsAlive = _parentA != null && _parentB != null && !_parentA.Down && !_parentB.Down;
        bool canBreed = parentsAlive
                        && _ui.ActivePen != null
                        && _ui.ActivePen.CanBreedPair(_parentA, _parentB, out actualCost);

        _costLbl.text = (canBreed ? actualCost : displayCost).ToString();
        _breedBtn.SetEnabled(canBreed);

        FillParentSlot(_slotA, _parentA, slotIndex: 0);
        FillParentSlot(_slotB, _parentB, slotIndex: 1);

        _rosterScroll.Clear();
        for (int i = 0; i < _ui.Capacity; i++)
        {
            if (i < _ui.Animals.Count)
            {
                var animal = _ui.Animals[i];
                bool isSelected = animal == _parentA || animal == _parentB;
                var card = BuildCard(animal, isSelected, fill: false);
                card.style.marginRight = 10;
                card.style.flexShrink = 0;

                if (animal.Down)
                {
                    card.style.opacity = 0.4f;
                    card.tooltip = $"{animal.GetCreatureName()} is knocked out and cannot breed";
                }
                else
                {
                    SetupCardDragHandlers(card, animal, sourceSlot: null);
                }

                _rosterScroll.Add(card);
            }
            else
            {
                var empty = _cardTemplate.Instantiate();
                var emptyRoot = empty.Q("AnimalCardRoot") ?? empty;
                empty.style.marginRight = 10;
                empty.style.flexShrink = 0;
                empty.style.opacity = 0.55f;

                emptyRoot.style.backgroundColor = CardDefaultBg;
                emptyRoot.style.borderTopWidth = emptyRoot.style.borderBottomWidth =
                    emptyRoot.style.borderLeftWidth = emptyRoot.style.borderRightWidth = 2f;
                emptyRoot.style.borderTopColor = emptyRoot.style.borderBottomColor =
                    emptyRoot.style.borderLeftColor = emptyRoot.style.borderRightColor = CardDefaultBorder;

                var nameLbl = empty.Q<Label>("AnimalNameLabel");
                if (nameLbl != null) nameLbl.style.display = DisplayStyle.None;

                var miniRow = empty.Q("MiniMutationsRow");
                if (miniRow != null) miniRow.style.display = DisplayStyle.None;

                var portrait = empty.Q("AnimalPortrait");
                if (portrait != null)
                {
                    portrait.Clear();
                    portrait.style.backgroundImage = StyleKeyword.None;
                    portrait.style.flexGrow = 1;
                    portrait.style.width = Length.Percent(100);
                    portrait.style.height = Length.Percent(100);
                    portrait.style.justifyContent = Justify.Center;
                    portrait.style.alignItems = Align.Center;

                    var qMark = new Label("?")
                    {
                        style =
                        {
                            fontSize = 64,
                            color = new Color(1f, 1f, 1f, 0.7f),
                            unityFontStyleAndWeight = FontStyle.Bold,
                            unityTextAlign = TextAnchor.MiddleCenter,
                            marginLeft = 0, marginRight = 0, marginTop = 0, marginBottom = 0,
                            paddingLeft = 0, paddingRight = 0, paddingTop = 0, paddingBottom = 0
                        }
                    };
                    portrait.Add(qMark);
                }

                empty.SetEnabled(false);
                _rosterScroll.Add(empty);
            }
        }
    }

    private void FillParentSlot(VisualElement container, Animal parent, int slotIndex)
    {
        container.Clear();
        if (parent == null) return;

        var card = BuildCard(parent, selected: true, fill: true);
        SetupCardDragHandlers(card, parent, sourceSlot: slotIndex);
        container.Add(card);
    }

    private void SetupCardDragHandlers(VisualElement card, Animal animal, int? sourceSlot)
    {
        card.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || animal == null || animal.Down) return;
            _draggingAnimal = animal;
            _dragSourceSlot = sourceSlot;
            _dragStartPos = evt.position;
            _isDragging = false;
            card.CapturePointer(evt.pointerId);
        });

        card.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (_draggingAnimal != animal || _dragSourceSlot != sourceSlot || !card.HasPointerCapture(evt.pointerId))
                return;

            if (!_isDragging && Vector2.Distance(evt.position, _dragStartPos) >= DragThreshold)
            {
                _isDragging = true;
                var tex = _booth?.GetPortrait(animal);
                _dragGhost.style.backgroundImage = tex != null ? new StyleBackground(tex) : StyleKeyword.None;
                _dragGhost.style.display = DisplayStyle.Flex;
                _dragGhost.BringToFront();
            }

            if (_isDragging)
            {
                Vector2 localPos = _root.WorldToLocal(evt.position);
                _dragGhost.style.left = localPos.x - 87.5f;
                _dragGhost.style.top = localPos.y - 92.5f;
                HighlightParentDropTargets(animal, evt.position);
            }
        });

        card.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (_draggingAnimal != animal || _dragSourceSlot != sourceSlot) return;
            if (card.HasPointerCapture(evt.pointerId))
                card.ReleasePointer(evt.pointerId);

            bool wasDragging = _isDragging;
            CancelDrag();

            if (!wasDragging)
            {
                // Click fallback: clicking a slot removes it; clicking roster toggles it
                if (sourceSlot == 0) _parentA = null;
                else if (sourceSlot == 1) _parentB = null;
                else ToggleParent(animal);
                Refresh();
                return;
            }

            int? targetSlot = GetParentSlotUnderPointer(evt.position);
            if (targetSlot == 0)
            {
                AssignToSlot(animal, targetSlotIndex: 0);
                Refresh();
            }
            else if (targetSlot == 1)
            {
                AssignToSlot(animal, targetSlotIndex: 1);
                Refresh();
            }
            else if (sourceSlot.HasValue)
            {
                // Dragged out of a parent slot onto empty space/roster -> unassign
                if (sourceSlot.Value == 0) _parentA = null;
                else _parentB = null;
                Refresh();
            }
        });

        card.RegisterCallback<PointerCaptureOutEvent>(_ =>
        {
            if (_draggingAnimal == animal && _dragSourceSlot == sourceSlot)
                CancelDrag();
        });
    }

    private void AssignToSlot(Animal animal, int targetSlotIndex)
    {
        if (animal == null || animal.Down) return;

        if (targetSlotIndex == 0)
        {
            if (_parentB == animal) _parentB = _parentA; // Swap if dragged from Slot B to Slot A
            _parentA = animal;
        }
        else
        {
            if (_parentA == animal) _parentA = _parentB; // Swap if dragged from Slot A to Slot B
            _parentB = animal;
        }
    }

    private int? GetParentSlotUnderPointer(Vector2 worldPointerPos)
    {
        if (_slotA != null && _slotA.worldBound.Contains(worldPointerPos)) return 0;
        if (_slotB != null && _slotB.worldBound.Contains(worldPointerPos)) return 1;
        return null;
    }

    private void HighlightParentDropTargets(Animal dragged, Vector2 worldPointerPos)
    {
        int? hovered = GetParentSlotUnderPointer(worldPointerPos);
        UpdateSingleSlotHighlight(_slotA, 0, dragged, _parentB, hovered);
        UpdateSingleSlotHighlight(_slotB, 1, dragged, _parentA, hovered);
    }

    private void UpdateSingleSlotHighlight(VisualElement slotEl, int slotIndex, Animal dragged, Animal otherParent, int? hovered)
    {
        if (slotEl == null) return;
        if (_dragSourceSlot == slotIndex)
        {
            slotEl.RemoveFromClassList("slot-drop-valid");
            slotEl.RemoveFromClassList("slot-drop-invalid");
            slotEl.RemoveFromClassList("slot-drop-hover");
            return;
        }

        bool compatible = otherParent == null || otherParent == dragged ||
                          otherParent.GetCreatureName() == dragged.GetCreatureName();

        slotEl.EnableInClassList("slot-drop-valid", compatible);
        slotEl.EnableInClassList("slot-drop-invalid", !compatible);
        slotEl.EnableInClassList("slot-drop-hover", compatible && hovered == slotIndex);
    }

    private void CancelDrag()
    {
        _draggingAnimal = null;
        _dragSourceSlot = null;
        _isDragging = false;
        if (_dragGhost != null)
            _dragGhost.style.display = DisplayStyle.None;

        foreach (var slotEl in new[] { _slotA, _slotB })
        {
            if (slotEl == null) continue;
            slotEl.RemoveFromClassList("slot-drop-valid");
            slotEl.RemoveFromClassList("slot-drop-invalid");
            slotEl.RemoveFromClassList("slot-drop-hover");
        }
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

        var nameLbl = card.Q<Label>("AnimalNameLabel");
        if (nameLbl != null)
        {
            nameLbl.style.display = DisplayStyle.Flex;
            nameLbl.text = animal.Down ? $"{animal.GetCreatureName()} (Down)" : animal.GetCreatureName();
        }

        var tex = _booth?.GetPortrait(animal);
        if (tex != null)
            card.Q("AnimalPortrait").style.backgroundImage = new StyleBackground(tex);

        var miniSlots = card.Query("MiniMutationSlot").ToList();
        animal.TryGetComponent<CreatureMutator>(out var mut);
        for (int i = 0; i < Slots.Length && i < miniSlots.Count; i++)
        {
            var m = mut?.GetEquippedMutation(Slots[i]);
            miniSlots[i].style.backgroundImage = m?.prefabSprite != null
                ? new StyleBackground(m.prefabSprite)
                : StyleKeyword.None;
        }

        root.style.backgroundColor = selected ? CardSelectedBg : CardDefaultBg;
        root.style.borderTopWidth = root.style.borderBottomWidth =
            root.style.borderLeftWidth = root.style.borderRightWidth = 2f;
        root.style.borderTopColor = root.style.borderBottomColor =
            root.style.borderLeftColor = root.style.borderRightColor =
                selected ? CardSelectedBorder : CardDefaultBorder;

        return card;
    }

    private void ToggleParent(Animal a)
    {
        if (a == null || a.Down) return;

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