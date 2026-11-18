using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events; // 1. Add this namespace
using TMPro;
using System;

public class ContextActionButton : MonoBehaviour
{
    [SerializeField] private Button buttonComponent;
    [SerializeField] private TMP_Text buttonText;

    public void Setup(string textToDisplay, UnityAction onClickAction)
    {
        // 1. Set the dynamic text (supports TMP sprite tags for your resource icons)
        buttonText.text = textToDisplay;

        // 2. Clear old listeners to prevent double-firing if the button is reused
        buttonComponent.onClick.RemoveAllListeners();

        // 3. Bind the new action from your Barn/Pen/Repair logic
        if (onClickAction != null)
        {
            buttonComponent.onClick.AddListener(onClickAction);
        }
    }
}