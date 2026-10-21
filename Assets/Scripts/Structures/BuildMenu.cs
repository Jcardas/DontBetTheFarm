
using UnityEngine;
using TMPro;

public class BuildMenu : MonoBehaviour
{
    public GameObject buildMenu;
    public TMP_Text buildButtonText;

    void Start()
    {
        buildMenu.SetActive(false);
        buildButtonText.text = "+ Build";
    }

    void Update()
{
    if (UnityEngine.InputSystem.Keyboard.current != null &&
        UnityEngine.InputSystem.Keyboard.current.bKey.wasPressedThisFrame)
    {
        ToggleBuildMenu();
    }
}

    public void ToggleBuildMenu()
    {
        bool isOpen = !buildMenu.activeSelf;

        buildMenu.SetActive(isOpen);

        buildButtonText.text = isOpen ? "× Close" : "+ Build";
    }

    public void CloseBuildMenu()
    {
        buildMenu.SetActive(false);
        buildButtonText.text = "+ Build";
    }
}
