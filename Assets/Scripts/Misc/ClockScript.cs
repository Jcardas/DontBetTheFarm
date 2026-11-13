using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClockScript : MonoBehaviour
{
    public TimeManager timeManager;
    public Image clockFace;

    void Update()
    {
        clockFace.rectTransform.rotation = Quaternion.Euler(0, 0, -timeManager.getClockDegrees());
    }
}
