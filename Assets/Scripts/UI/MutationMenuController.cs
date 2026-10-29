using UnityEngine;
using UnityEngine.InputSystem;

public class MutationMenuController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject mutationMenuPanel;
    public Transform leftAnimalContent;
    public Transform rightInventoryContent;

    [Header("Prefabs")]
    public GameObject animalRowPrefab;
    public GameObject inventoryItemPrefab;

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

    private void ToggleMenu()
    {
        bool isActive = !mutationMenuPanel.activeSelf;
        mutationMenuPanel.SetActive(isActive);

        if (isActive)
        {
            RefreshMenu();
        }
    }

    private void RefreshMenu()
    {
        ClearContainer(leftAnimalContent);
        ClearContainer(rightInventoryContent);

        PopulateAnimalList();
        PopulateInventory();
    }

    private void PopulateAnimalList()
    {
        for (int i = 0; i < 5; i++)
        {
            GameObject newAnimal = Instantiate(animalRowPrefab, leftAnimalContent, false);
            newAnimal.name = "AnimalRow_" + i;
        }

        // Force Unity to calculate the Layout Groups immediately
        Canvas.ForceUpdateCanvases();

        // Log the exact pixel size the container forced onto the first row
        if (leftAnimalContent.childCount > 0)
        {
            RectTransform firstRow = leftAnimalContent.GetChild(0).GetComponent<RectTransform>();
            Debug.Log($"LEFT ROW RENDERED SIZE: Width = {firstRow.rect.width}, Height = {firstRow.rect.height}");
        }
    }

    private void PopulateInventory()
    {
        for (int i = 0; i < 12; i++)
        {
            GameObject newItem = Instantiate(inventoryItemPrefab, rightInventoryContent, false);
            newItem.name = "InventoryItem_" + i;
        }

        Canvas.ForceUpdateCanvases();

        if (rightInventoryContent.childCount > 0)
        {
            RectTransform firstItem = rightInventoryContent.GetChild(0).GetComponent<RectTransform>();
            Debug.Log($"RIGHT ITEM RENDERED SIZE: Width = {firstItem.rect.width}, Height = {firstItem.rect.height}");
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