using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ClockScript : MonoBehaviour
{
    public TimeManager timeManager;
    public Image clockFace;
    public TMP_Text dayText;

    void Update()
    {
        clockFace.rectTransform.rotation = Quaternion.Euler(0, 0, -timeManager.getClockDegrees());
        dayText.text = "Day " + timeManager.getDay().ToString();
    }
}
