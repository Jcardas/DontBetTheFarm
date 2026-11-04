using UnityEngine;
using UnityEngine.InputSystem;

public class MutationMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject mutationMenuPanel;
    public Transform rightMutationContent; // The Content object inside your BodyPartPanel > Scroll View

    [Header("Prefabs")]
    public GameObject mutationCardPrefab; // The new Figma-style mutation card we will build tomorrow

    void Start()
    {
        mutationMenuPanel.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        bool isActive = !mutationMenuPanel.activeSelf;
        mutationMenuPanel.SetActive(isActive);

        if (isActive)
        {
            // When the menu opens, default to showing the first slot (e.g., Head)
            OnSlotSelected(0);
        }
    }

    // The Back Button in your new UI will call this method
    public void CloseMenu()
    {
        mutationMenuPanel.SetActive(false);
    }

    // The 4 slot buttons (Head, Arms, Body, Legs) will call this via Unity Events
    public void OnSlotSelected(int slotIndex)
    {
        ClearContainer(rightMutationContent);
        PopulateMutations(slotIndex);
    }

    private void PopulateMutations(int slotIndex)
    {
        // Tomorrow we can update this to pull real game data based on the slotIndex.
        // For now, it spawns 5 placeholder cards so you can test the Scroll View.
        for (int i = 0; i < 5; i++)
        {
            if (mutationCardPrefab != null)
            {
                GameObject newCard = Instantiate(mutationCardPrefab, rightMutationContent, false);
                newCard.name = "MutationCard_" + slotIndex + "_" + i;
            }
        }
    }

    private void ClearContainer(Transform container)
    {
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }
}