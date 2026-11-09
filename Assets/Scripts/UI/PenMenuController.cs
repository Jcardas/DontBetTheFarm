using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PenMenuController : MonoBehaviour
{
    [Header("Menu References")]
    public GameObject masterMenuRoot;
    public GameObject animalsView;
    public GameObject breedingView;

    [Header("Grid References")]
    public Transform penListGrid;
    public GameObject animalCardPrefab;

    [Header("Tab Buttons")]
    public Image btnAnimalsBackground;
    public Image btnBreedingBackground;

    private Color activeColor;
    private Color inactiveColor;

    public RectTransform tabNavigationContainer;


    void Start()
    {
        ColorUtility.TryParseHtmlString("#2A4835", out activeColor);
        inactiveColor = new Color(0, 0, 0, 0);

        masterMenuRoot.SetActive(true);

        ShowAnimalsTab();

        Canvas.ForceUpdateCanvases();

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
        btnAnimalsBackground.color = activeColor;
        btnBreedingBackground.color = inactiveColor;
    }

    public void ShowBreedingTab()
    {
        animalsView.SetActive(false);
        breedingView.SetActive(true);
        btnAnimalsBackground.color = inactiveColor;
        btnBreedingBackground.color = activeColor;
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

    public void CloseMenu()
    {
        masterMenuRoot.SetActive(false);
    }
}