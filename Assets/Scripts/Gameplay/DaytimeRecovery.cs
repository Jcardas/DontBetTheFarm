using UnityEngine;
using UnityEngine.SceneManagement;

// Daytime recovery from playtest feedback: at daybreak every downed farm animal gets back up with 1 HP, and while
// it is day every standing farm unit slowly heals. Reads TimeManager only (it is being reworked; don't depend on more).
// Spawns itself in any scene that has a TimeManager, so no scene or prefab edit is needed.
public class DaytimeRecovery : MonoBehaviour
{
    public const float SecondsPerHealthPoint = 2f; // daytime regen: 1 HP every 2 s
    public const int ReviveHealth = 1;

    bool? wasDay;
    float nextTick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void HookSceneLoads()
    {
        SceneManager.sceneLoaded -= SpawnFor;
        SceneManager.sceneLoaded += SpawnFor;
    }

    static void SpawnFor(Scene scene, LoadSceneMode mode)
    {
        if (FindAnyObjectByType<TimeManager>() == null || FindAnyObjectByType<DaytimeRecovery>() != null) return;
        new GameObject(nameof(DaytimeRecovery)).AddComponent<DaytimeRecovery>();
    }

    void Update()
    {
        if (TimeManager.Instance == null) return;
        bool day = TimeManager.Instance.IsDayTime();

        if (day && wasDay == false) ReviveDowned();
        wasDay = day;

        if (!day || Time.time < nextTick) return;
        nextTick = Time.time + SecondsPerHealthPoint;
        foreach (Selectable unit in Selectable.All)
        {
            Creature c = unit != null ? unit.Creature : null;
            if (c == null || c.Down || c.Team != GameConstants.Team.Farm || !c.gameObject.activeInHierarchy) continue;
            if (c.CurrentHealth < c.CalculatedMaxHealth) c.HealAmount(1);
        }
    }

    static void ReviveDowned()
    {
        foreach (Selectable unit in Selectable.All)
        {
            if (unit == null || unit.Creature is not Animal animal) continue;
            if (!animal.Down || animal.Team != GameConstants.Team.Farm || !animal.gameObject.activeInHierarchy || UFO.IsClaimed(animal)) continue;
            animal.ReviveWithHealth(ReviveHealth);
        }
    }
}
