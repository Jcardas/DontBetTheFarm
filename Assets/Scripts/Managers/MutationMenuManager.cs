using UnityEngine;
using UnityEngine.UIElements;

public class MutationMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private UIDocument menuDocument;
    [SerializeField] private VisualTreeAsset slotTemplate;
    [SerializeField] private VisualTreeAsset filledMutationTemplate;

    private void Start()
    {
        VisualElement root = menuDocument.rootVisualElement;
        VisualElement container = root.Q<VisualElement>("slot-container");

        if (container == null)
        {
            Debug.LogError("Could not find 'slot-container' in the main menu UXML.");
            return;
        }

        // Define the exact 4 slots your game requires
        MutationSlotType[] slotsToSpawn = {
            MutationSlotType.Head,
            MutationSlotType.Back,
            MutationSlotType.Limbs,
            MutationSlotType.Tail
        };

        // Loop through and build the UI
        foreach (var slotType in slotsToSpawn)
        {
            VisualElement newSlotUI = slotTemplate.Instantiate();
            container.Add(newSlotUI);

            MutationSlotController slotController = new MutationSlotController(newSlotUI, filledMutationTemplate);

            // --- REAL GAME DATA INTEGRATION ---
            // TODO: Replace this pseudo-code with your team's actual data manager fetch.
            // Example:
            // MutationData currentMutation = PlayerDataManager.Instance.GetEquippedMutation(slotType);
            // slotController.UpdateSlotState(currentMutation.State, currentMutation.IconSprite);

            // Make the slot clickable
            newSlotUI.RegisterCallback<ClickEvent>(evt =>
            {
                OnSlotClicked(slotType);
            });
        }
    }

    private void OnSlotClicked(MutationSlotType type)
    {
        Debug.Log($"Opening mutation selection list for: {type}");
        // TODO: Add logic here to open a secondary menu showing all unlocked mutations for this body part.
    }
}