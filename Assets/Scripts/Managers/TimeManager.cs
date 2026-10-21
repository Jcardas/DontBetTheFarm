using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public const int CIRCLE_DEGREES = 360;
    public const int hoursInDay = 12;       // 12 hours of day
    public const int hoursInNight = 12;     // 12 hours of night
    public const int DAY_START_TIME = 6;    // 6am
    public const int NIGHT_START_TIME = 18; // 6pm

    public float lengthOfHour = 10f;        // Length of time an hour takes in seconds
    public const int DayLength = hoursInDay + hoursInNight;
    public float timePassed = 0;
    float currentTime = 0;

    public Transform celestialRig;
    public Light sunLight;
    public Light moonLight;

    public ParticleSystem starSystem;
    public float starEmissionRate = 100f;


    void Update()
    {
        timePassed += Time.deltaTime;

        if(celestialRig != null)
        {
            float rotationX = getClockDegrees() - 180f;
            celestialRig.rotation = Quaternion.Euler(rotationX, -30f, 0);
            UpdateLightIntensities();
        }

        currentTime = timePassed % lengthOfHour;
    }

    public float getHour()
    {
        return (timePassed / lengthOfHour) % DayLength;
    }

    public float getClockDegrees()
    {
        return (getHour() / DayLength) * CIRCLE_DEGREES;
    }

    // Determine if it is daytime (btwn 6am and 6pm) or nighttime (btwn 6pm and 6am)
    bool isDayTime()
    {
        return getHour() >= DAY_START_TIME && getHour() < NIGHT_START_TIME;
    }

    // Determine if it is datime (btwn 6am and 6pm) or nighttime (btwn 6pm and 6am)
    void UpdateLightIntensities()
    {
        if (sunLight == null || moonLight == null)
        {
            return;
        }

        if (isDayTime())
        {
            // Daytime: Sun is on, Moon is off
            sunLight.intensity = 1f;
            moonLight.intensity = 0f;

            sunLight.shadows = LightShadows.Soft;
            moonLight.shadows = LightShadows.None;

            sunLight.shadowStrength = 1f;
            moonLight.shadowStrength = 0f;
        }
        else
        {
            // Nighttime: Sun is off, Moon is on
            sunLight.intensity = 0f;
            moonLight.intensity = 1f;

            sunLight.shadows = LightShadows.None;
            moonLight.shadows = LightShadows.Soft;

            sunLight.shadowStrength = 0f;
            moonLight.shadowStrength = 1f;
        }

        if (starSystem != null)
        {
            var emission = starSystem.emission;
            float currentEmission = emission.rateOverTime.constant;
            float targetEmission = isDayTime() ? 0f : starEmissionRate;

            // Lerp gradually moves the current emission toward the target emission
            emission.rateOverTime = Mathf.Lerp(currentEmission, targetEmission, Time.deltaTime * 0.5f);
        }
    }
}
