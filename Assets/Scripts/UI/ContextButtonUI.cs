using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

[RequireComponent(typeof(Button))]
public class ContextButtonUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI labelText;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    public void Initialize(ContextActionData data, UnityAction onClickCallback)
    {
        if (button == null) button = GetComponent<Button>();

        //Text Content & Color
        if (labelText != null)
        {
            // Override text only if data provides a non-empty string; otherwise keep prefab's default text
            if (data != null && !string.IsNullOrWhiteSpace(data.label))
            {
                labelText.text = data.label;
            }

            // Override text color only if an alpha > 0 color is specified
            if (data != null && data.textColor.a > 0f)
            {
                labelText.color = data.textColor;
            }

            // Visible if either the data or the prefab text is present
            labelText.gameObject.SetActive(!string.IsNullOrEmpty(labelText.text));
        }

        // Icon Content
        if (iconImage != null)
        {
            // Override icon only if an icon is provided; otherwise keep prefab's default sprite
            if (data != null && data.icon != null)
            {
                iconImage.sprite = data.icon;
            }

            // Visible if either custom icon or prefab default icon exists
            iconImage.gameObject.SetActive(iconImage.sprite != null);
        }

        // Background & Tint
        if (backgroundImage != null)
        {
            // Override sprite only if custom background is assigned
            if (data != null && data.customBackground != null)
            {
                backgroundImage.sprite = data.customBackground;
            }

            // Override color only if an alpha > 0 color is specified; prevents turning transparent
            if (data != null && data.buttonTint.a > 0f)
            {
                backgroundImage.color = data.buttonTint;
            }
        }

        // Click Callback
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClickCallback != null)
            {
                button.onClick.AddListener(onClickCallback);
            }
        }
    }
}