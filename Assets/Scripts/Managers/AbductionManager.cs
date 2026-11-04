using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Sends an enemy saucer for each knocked-out farm creature no saucer has claimed yet, once it has been down for
// `delay` seconds, up to `maxSaucers` at a time. Each saucer comes in from the map-edge point nearest its target
// and leaves through the same point.
// At each nightfall the animals the saucers took come back at the wave spawn points on the aliens' side and march on
// the farm. The farmer never comes back.
public class AbductionManager : MonoBehaviour
{
    [SerializeField] UFO enemySaucerPrefab;
    [Tooltip("Where enemy saucers come in and leave, around the map edge.")]
    [SerializeField] Transform[] edgePoints;
    [SerializeField] float delay = 4f;
    [SerializeField] int maxSaucers = 3;
    [SerializeField] float spawnHeight = 3.5f;
    [Tooltip("Its clock decides nightfall. Found in the scene when left empty.")]
    [SerializeField] TimeManager time;
    [Tooltip("Returned animals appear at its spawn points. Found in the scene when left empty.")]
    [SerializeField] WaveManager waves;

    readonly Dictionary<Creature, float> downSince = new();
    readonly List<UFO> saucers = new();
    readonly List<Creature> stale = new();
    readonly List<Creature> returned = new();
    bool wasNight;

    void Awake()
    {
        if (time == null) time = FindFirstObjectByType<TimeManager>();
        if (waves == null) waves = FindFirstObjectByType<WaveManager>();
    }

    void Update()
    {
        ReturnAtNightfall();
        MarchReturned();
        saucers.RemoveAll(s => s == null);
        if (CreatureManager.Instance == null || enemySaucerPrefab == null || edgePoints.Length == 0) return;

        foreach (Creature c in CreatureManager.Instance.GetEnemies(GameConstants.Team.Alien))
        {
            if (c == null || !c.Down || UFO.IsClaimed(c)) continue;
            if (!downSince.TryGetValue(c, out float since)) downSince[c] = since = Time.time;
            if (Time.time - since >= delay && saucers.Count < maxSaucers) Spawn(c);
        }

        stale.Clear();
        foreach (Creature c in downSince.Keys)
            if (c == null || !c.Down || UFO.IsClaimed(c)) stale.Add(c);
        foreach (Creature c in stale) downSince.Remove(c);
    }

    void ReturnAtNightfall()
    {
        if (time == null) return;
        float hour = time.getHour();
        bool night = hour >= TimeManager.NIGHT_START_TIME || hour < TimeManager.DAY_START_TIME;
        if (night && !wasNight) ReturnAbducted();
        wasNight = night;
    }

    // Every abducted animal comes back as an enemy at the wave spawn points.
    public void ReturnAbducted()
    {
        if (waves == null || waves.spawnPoints == null || waves.spawnPoints.Length == 0) return;
        for (int i = UFO.Abducted.Count - 1; i >= 0; i--)
        {
            if (!(UFO.Abducted[i] is FarmAnimal animal)) continue;
            UFO.Abducted.RemoveAt(i);
            Vector3 at = waves.spawnPoints[Random.Range(0, waves.spawnPoints.Length)].position;
            if (NavMesh.SamplePosition(at, out NavMeshHit hit, 4f, NavMesh.AllAreas)) at = hit.position;

            if (animal.TryGetComponent(out Selectable selectable))
            {
                selectable.Selected = false;
                selectable.enabled = false; // the player can't pick or order it while it's on their side
            }
            animal.transform.position = at;
            animal.gameObject.SetActive(true);
            animal.SetTeam(GameConstants.Team.Alien);
            animal.NavMeshAgent.enabled = true;
            animal.NavMeshAgent.Warp(at);
            animal.HealToFull();
            animal.CreatureBrain.SetGuardPost(at);
            animal.CreatureBrain.IsPassive = false; // a cow taken mid-job can still be marked as working
            returned.Add(animal);
        }
    }

    // A returned animal guards its post like any animal would, which on the spawn platform means doing nothing, so send
    // it at the nearest farm creature whenever it isn't already fighting.
    void MarchReturned()
    {
        returned.RemoveAll(c => c == null || c.Team != GameConstants.Team.Alien);
        if (CreatureManager.Instance == null) return;
        foreach (Creature c in returned)
        {
            if (c.Down || !c.gameObject.activeInHierarchy || c.CreatureBrain.Engaged) continue;
            Creature target = CreatureManager.Instance.GetNearestEnemy(c.Team, c.Position, c.Position, float.MaxValue);
            if (target != null) c.CreatureBrain.Attack(target);
        }
    }

    void Spawn(Creature target)
    {
        Transform edge = edgePoints[0];
        foreach (Transform p in edgePoints)
            if (p && Vector3.Distance(p.position, target.Position) < Vector3.Distance(edge.position, target.Position))
                edge = p;

        UFO saucer = Instantiate(enemySaucerPrefab, edge.position + Vector3.up * spawnHeight, Quaternion.identity);
        saucer.Send(target, edge.position);
        saucers.Add(saucer);
    }
}
