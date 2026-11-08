using UnityEngine;
using UnityEngine.InputSystem;

public class MutationMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject mutationMenuPanel;
    public Transform rightMutationContent;

    [Header("Prefabs")]
    public GameObject mutationCardPrefab;

    void Start()
    {
        mutationMenuPanel.SetActive(true);

        OnSlotSelected(0);
        Canvas.ForceUpdateCanvases();

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
            OnSlotSelected(0);
            Canvas.ForceUpdateCanvases();
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

        // Forces the layout to calculate instantly when a tab is clicked
        Canvas.ForceUpdateCanvases();
    }

    private void PopulateMutations(int slotIndex)
    {
        for (int i = 0; i < 15; i++)
        {
            if (mutationCardPrefab != null)
            {
                GameObject newCard = Instantiate(mutationCardPrefab, rightMutationContent, false);
                newCard.name = "MutationCard_" + slotIndex + "_" + i;

                // DIAGNOSTICS: Only log the first card so we don't spam the console
                if (i == 0)
                {
                    RectTransform rt = newCard.GetComponent<RectTransform>();
                    Debug.Log($"--- HUNTING CARD 0 ---");
                    Debug.Log($"Local Position (Inside Container): {rt.localPosition}");
                    Debug.Log($"World Position (On Screen): {rt.position}");
                    Debug.Log($"Scale: {rt.localScale}");
                    Debug.Log($"Size: {rt.rect.width} x {rt.rect.height}");
                    Debug.Log($"Layer: {LayerMask.LayerToName(newCard.layer)}");
                }
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