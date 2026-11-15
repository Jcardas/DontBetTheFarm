using UnityEngine;
using UnityEngine.UIElements;

public enum MutationSlotType { Head, Back, Limbs, Tail }
public enum MutationSlotState { Locked, Empty, Filled }

public class MutationSlotController
{
    private VisualElement _root;
    private VisualElement _iconElement;
    private VisualTreeAsset _filledMutationUxml; // Your friend's UXML
    private VisualElement _instantiatedFilledUI; // The spawned clone of your friend's UXML

    // We now pass your friend's UXML template into the constructor
    public MutationSlotController(VisualElement root, VisualTreeAsset filledMutationUxml)
    {
        _root = root;
        _filledMutationUxml = filledMutationUxml;
        _iconElement = _root.Q<VisualElement>("mutation-icon");
    }

    public void UpdateSlotState(MutationSlotState state, Sprite placeholderSprite)
    {
        if (_iconElement == null) return;

        // Clear out any previously spawned filled UI so they don't stack up
        if (_instantiatedFilledUI != null)
        {
            _iconElement.Remove(_instantiatedFilledUI);
            _instantiatedFilledUI = null;
        }

        if (state == MutationSlotState.Filled)
        {
            // 1. Hide the placeholder background image
            _iconElement.style.backgroundImage = null;

            // 2. Spawn your friend's UXML
            if (_filledMutationUxml != null)
            {
                _instantiatedFilledUI = _filledMutationUxml.Instantiate();

                // Optional: Ensure it fills the slot space
                _instantiatedFilledUI.style.width = Length.Percent(100);
                _instantiatedFilledUI.style.height = Length.Percent(100);

                // 3. Add it as a child to your mutation-icon container
                _iconElement.Add(_instantiatedFilledUI);
            }
        }
        else
        {
            // For Locked or Empty, just show your standard placeholder sprite
            _iconElement.style.backgroundImage = new StyleBackground(placeholderSprite);
        }
    }
}