using UnityEngine;
using UnityEngine.InputSystem;

public class PenMenuController : MonoBehaviour
{
    [Header("Menu References")]
    public GameObject masterMenuRoot;
    public GameObject animalsView;
    public GameObject breedingView;

    [Header("Grid References")]
    public Transform penListGrid; // Drag your PenList_Grid object here
    public GameObject animalCardPrefab; // Drag your saved prefab here

    void Start()
    {
        masterMenuRoot.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            ToggleMasterMenu();
        }
    }

    private void ToggleMasterMenu()
    {
        bool isActive = !masterMenuRoot.activeSelf;
        masterMenuRoot.SetActive(isActive);

        if (isActive)
        {
            ShowAnimalsTab();
            PopulatePenGrid(); // Generate the cards when the menu opens
        }
    }

    public void ShowAnimalsTab()
    {
        animalsView.SetActive(true);
        breedingView.SetActive(false);
        Debug.Log("Tab Switched: Now viewing the ANIMALS menu.");
    }

    public void ShowBreedingTab()
    {
        animalsView.SetActive(false);
        breedingView.SetActive(true);
        Debug.Log("Tab Switched: Now viewing the BREEDING menu.");
    }

    private void PopulatePenGrid()
    {
        // 1. Clear out any old cards first so they don't endlessly duplicate
        foreach (Transform child in penListGrid)
        {
            Destroy(child.gameObject);
        }

        // 2. Spawn 5 test cards (you will later replace this '5' with your actual list of farm animals)
        for (int i = 0; i < 5; i++)
        {
            Instantiate(animalCardPrefab, penListGrid, false);
        }
    }
}