using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

public class PenMenuController : MonoBehaviour
{
    [Header("Menu Documents")]
    public UIDocument animalMenuDoc;
    public UIDocument breedingMenuDoc;

    [Header("Controls")]
    // Customizable in the Inspector. 
    public Key toggleKey = Key.B;

    // Tracks which tab to open
    private enum MenuTab { Animals, Breeding }
    private MenuTab lastActiveTab = MenuTab.Animals; // Defaults to Animals on first load
    private bool isMenuOpen = false;

    private VisualElement animalRoot;
    private VisualElement breedingRoot;

    void Start()
    {
        // 1. Grab the root visual elements
        animalRoot = animalMenuDoc.rootVisualElement;
        breedingRoot = breedingMenuDoc.rootVisualElement;

        // 2. Query and hook up Animal Menu buttons
        var animalBackButton = animalRoot.Q<VisualElement>("Btn_Back");
        var breedingTabBtn = animalRoot.Q<VisualElement>("Tab_Breeding");

        if (animalBackButton != null) animalBackButton.RegisterCallback<ClickEvent>(evt => CloseMenu());
        if (breedingTabBtn != null) breedingTabBtn.RegisterCallback<ClickEvent>(evt => SwitchTab(MenuTab.Breeding));

        // 3. Query and hook up Breeding Menu buttons
        var breedingBackButton = breedingRoot.Q<VisualElement>("Btn_Back");
        var animalTabBtn = breedingRoot.Q<VisualElement>("Tab_Animals");

        if (breedingBackButton != null) breedingBackButton.RegisterCallback<ClickEvent>(evt => CloseMenu());
        if (animalTabBtn != null) animalTabBtn.RegisterCallback<ClickEvent>(evt => SwitchTab(MenuTab.Animals));

        // 4. Hide both menus initially using UI Toolkit's display style
        animalRoot.style.display = DisplayStyle.None;
        breedingRoot.style.display = DisplayStyle.None;
    }

    void Update()
    {
        // Check if a keyboard exists, then check if your specific key was pressed
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            if (isMenuOpen)
                CloseMenu();
            else
                OpenMenu();
        }
    }

    // Made PUBLIC so your future Animal Pen script can easily trigger this via code
    public void OpenMenu()
    {
        isMenuOpen = true;
        SwitchTab(lastActiveTab); // Opens whichever tab was saved last
    }

    public void CloseMenu()
    {
        isMenuOpen = false;
        // Hide both menus instantly
        animalRoot.style.display = DisplayStyle.None;
        breedingRoot.style.display = DisplayStyle.None;
    }

    private void SwitchTab(MenuTab tab)
    {
        lastActiveTab = tab; // Save the state for the next time the menu opens

        if (tab == MenuTab.Animals)
        {
            animalRoot.style.display = DisplayStyle.Flex;
            breedingRoot.style.display = DisplayStyle.None;
        }
        else if (tab == MenuTab.Breeding)
        {
            animalRoot.style.display = DisplayStyle.None;
            breedingRoot.style.display = DisplayStyle.Flex;
        }
    }
}