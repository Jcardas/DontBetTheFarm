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
    public const int DayLength = hoursInDay + hoursInNight;

    public bool timePaused = false;         // Is time paused?
    public int currentDay = 0;              // Current day count
    public float lengthOfHour = 10f;        // Length of time an hour takes in seconds
    public float timePassed = 0;

 
    public float starEmissionRate = 1000f;

    bool isDayTimeFlag = false;

    public Transform celestialRig;
    public Light sunLight;
    public Light moonLight;
    public ParticleSystem starSystem;


    void Start()
    {
        timePassed = 60f;
    }

    void Update()
    {
        if (!timePaused)
        {
            timePassed += Time.deltaTime;

            if (celestialRig != null)
            {
                float rotationX = getClockDegrees() - 180f;
                celestialRig.rotation = Quaternion.Euler(rotationX, -30f, 0);
            }

            int currentHour = (int)getHour();

            if (currentHour >= DAY_START_TIME && currentHour < NIGHT_START_TIME && !isDayTimeFlag)
            {
                setDayTime();
            }

            if ((currentHour >= NIGHT_START_TIME || currentHour < DAY_START_TIME) && isDayTimeFlag)
            {
                setNightTime();
            }

            UpdateStarSystem();
        }
    }

    void setDayTime()
    {
        currentDay++;
        Debug.Log("Day " + currentDay + " has started.");

        isDayTimeFlag = true;
        NightLightOff();
        DayLightOn();
    }

    void setNightTime()
    {
        Debug.Log("Night has started.");

        isDayTimeFlag = false;
        DayLightOff();
        NightLightOn();
    }

    void DayLightOn()
    {
        if (sunLight != null)
        {
            sunLight.intensity = 1f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 1f;
        }
    }

    void DayLightOff()
    {
        if (sunLight != null)
        {
            sunLight.intensity = 0f;
            sunLight.shadows = LightShadows.None;
            sunLight.shadowStrength = 0f;
        }
    }

    void NightLightOn()
    {
        if (moonLight != null)
        {
            moonLight.intensity = 1f;
            moonLight.shadows = LightShadows.Soft;
            moonLight.shadowStrength = 1f;
        }
    }

    void NightLightOff()
    {
        if (moonLight != null)
        {
            moonLight.intensity = 0f;
            moonLight.shadows = LightShadows.None;
            moonLight.shadowStrength = 0f;
        }
    }

    // Determine if it is dayime (btwn 6am and 6pm) or nighttime (btwn 6pm and 6am)
    void UpdateStarSystem()
    { 
        if (starSystem != null)
        {
            var emission = starSystem.emission;
            float currentEmission = emission.rateOverTime.constant;
            float targetEmission = isDayTimeFlag ? 0f : starEmissionRate;

            // Lerp gradually moves the current emission toward the target emission
            emission.rateOverTime = Mathf.Lerp(currentEmission, targetEmission, Time.deltaTime * 0.5f);
        }
    }

    // ACCESSORS AND MUTATORS
    public float getHour()
    {
        return (timePassed / lengthOfHour) % DayLength;
    }

    public float getClockDegrees()
    {
        return (getHour() / DayLength) * CIRCLE_DEGREES;
    }
}
