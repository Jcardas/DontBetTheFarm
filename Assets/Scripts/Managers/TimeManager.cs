using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public const int CIRCLE_DEGREES = 360;  // 360 degrees in a circle
    public const int HOURS_IN_DAY = 12;     // 12 hours of day
    public const int HOURS_IN_NIGHT = 12;   // 12 hours of night
    public const int DAY_START_TIME = 6;    // 6am
    public const int NIGHT_START_TIME = 18; // 6pm
    public const int DAY_LENGTH = HOURS_IN_DAY + HOURS_IN_NIGHT;

    public bool timePaused =    false;      // Is time paused?
    public int currentDay =     1;          // Current day count
    public float lengthOfHour = 10f;        // Length of time an hour takes in seconds
    public float currentTime =   0f;
    public float timePassed =    0f;        // Total time passed in seconds
    public float starEmissionRate = 1000f;

    bool isDayTimeFlag = false;

    public Transform celestialRig;
    public Light sunLight;
    public Light moonLight;
    public ParticleSystem starSystem;
    public WaveManager waveManager;


    void Start()
    {
        // On boot up, time starts at 6am on day 1
        currentTime = DAY_START_TIME;
    }

    void Update()
    {
        if (!timePaused)
        {
            PassTime();

            if (celestialRig != null)
            {
                float rotationX = getClockDegrees() - (CIRCLE_DEGREES / 2);
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

    /**
     * Called Every frame to update the time. It calculates the current time and total time passed based on the length of an hour and the delta time since the last frame.
     */
    void PassTime()
    {
        currentTime += Time.deltaTime / lengthOfHour;
        currentTime %= DAY_LENGTH; // Wrap around to 0 after 24 hours

        timePassed += Time.deltaTime;
    }

    void setDayTime()
    {
        currentDay++;
        Debug.Log("Day " + currentDay + " has started.");

        isDayTimeFlag = true;
        NightLightOff();
        DayLightOn();

        if (waveManager != null)
        {
            waveManager.RetreatWave();
        }
    }

    void setNightTime()
    {
        Debug.Log("Night has started.");

        isDayTimeFlag = false;
        DayLightOff();
        NightLightOn();
        waveManager.SpawnWave(currentDay);
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
        return currentTime;
    }

    public int getDay()
    {
        return currentDay;
    }

    public float getClockDegrees()
    {
        return (getHour() / DAY_LENGTH) * CIRCLE_DEGREES;
    }
}
