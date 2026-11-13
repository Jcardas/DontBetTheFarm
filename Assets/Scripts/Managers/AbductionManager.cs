using System;
using System.Collections.Generic;
using UnityEngine;

// Sends an enemy saucer for each knocked-out farm creature once it has been down for `delay` seconds, up to
// `maxSaucers` at a time. Each saucer comes out of the mothership nearest its target and takes it back there.
// The next night the motherships drop the stolen animals back on their spawn platforms as enemies.
public class AbductionManager : MonoBehaviour
{
    public static AbductionManager Instance { get; private set; }

    [SerializeField] UFO saucerPrefab;
    [Tooltip("A collar prefab that is placed on abducted animals when they return as enemies.")]
    [SerializeField] GameObject collarPrefab;
    [Tooltip("Seconds a farm creature lies knocked out before a saucer comes for it.")]
    [SerializeField] float delay = 4f;
    [SerializeField] int maxSaucers = 1;

    readonly Dictionary<Creature, float> downSince = new();
    readonly List<UFO> saucers = new();
    readonly List<Animal> returned = new();
    readonly Dictionary<Animal, GameObject> collars = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        MarchReturned();
        saucers.RemoveAll(s => s == null);
        
        if (CreatureManager.Instance == null || TimeManager.Instance == null || WaveManager.Instance == null || saucerPrefab == null) return;

        foreach (Creature c in CreatureManager.Instance.GetEnemies(GameConstants.Team.Alien))
        {
            if (c == null) continue;
            if (!c.Down || UFO.IsClaimed(c))
            {
                downSince.Remove(c);
                continue;
            }
            if (!downSince.TryGetValue(c, out float since)) downSince[c] = since = Time.time;
            if (Time.time - since >= delay && saucers.Count < maxSaucers && !TimeManager.Instance.IsDayTime()) Send(c);
        }
        
        if (saucers.Count > 0 && TimeManager.Instance.IsDayTime())
        {
            foreach (UFO saucer in saucers)
                if (saucer != null)
                {
                    if (saucer.IsVictimInUFO()) continue; // Already got its target, so let it leave on its own.
                    saucer.Retreat();
                }
        }
    }

    void Send(Creature target)
    {
        Spawner home = null;
        foreach (Spawner ship in WaveManager.Instance.motherships)
            if (ship != null && (home == null || Vector3.Distance(ship.transform.position, target.Position)
                                                 < Vector3.Distance(home.transform.position, target.Position)))
                home = ship;
        if (home == null) return;

        UFO saucer = Instantiate(saucerPrefab, home.transform.position, Quaternion.identity);
        saucer.Send(target, home.transform.position);
        saucers.Add(saucer);
        downSince.Remove(target);
    }

    // Called by a mothership when it reaches its spawn platform: every stolen animal comes back down there as an
    // enemy and joins the wave. The farmer never comes back.
    public void ReturnStolen(SpawnPlatform platform, GameObject beamEffect, WaveManager waves)
    {
        for (int i = UFO.Abducted.Count - 1; i >= 0; i--)
        {
            if (UFO.Abducted[i] is not Animal animal) continue;
            UFO.Abducted.RemoveAt(i);

            Vector3 at = platform.GetRandomSpawnPosition();
            if (beamEffect != null) Instantiate(beamEffect, at + Vector3.down * 2.5f, Quaternion.identity);
            if (animal.TryGetComponent(out Selectable selectable)) selectable.enabled = false; // the player can't order it now

            animal.SetTeam(GameConstants.Team.Alien);
            animal.transform.position = at;
            animal.gameObject.SetActive(true);
            animal.NavMeshAgent.enabled = true;
            animal.NavMeshAgent.Warp(at);
            animal.HealToFull();
            animal.SetDespawnPoint(at);
            waves.RegisterEnemy(animal);
            returned.Add(animal);
        }
    }

    // An animal only fights what comes near its post, so send each returned one at the nearest farm creature.
    // Knocking one out wins it back: it gets up again on the farm's side.
    void MarchReturned()
    {
        returned.RemoveAll(a => a == null);
        for (int i = returned.Count - 1; i >= 0; i--)
        {
            Animal a = returned[i];
            if (!a.Down)
            {
                // On goes the collar the frame after it lands, once its model has stood back up.
                if (!collars.ContainsKey(a)) collars[a] = PutOnCollar(a);
                continue;
            }
            returned.RemoveAt(i);
            if (collars.Remove(a, out GameObject collar) && collar != null) Destroy(collar);
            if (WaveManager.Instance != null) WaveManager.Instance.UnregisterEnemy(a);
            a.SetTeam(GameConstants.Team.Farm);
            
            // TODO: Instead of healing to full, leave the animal downed, the farmer (or a healing capable creature) can heal it back up.
            // a.HealToFull(); 
            // For now this is just commented out, but eventually we will remove
            // all this because the player will have to heal the animal back up to get it back on their side.
            
            a.CreatureBrain.Guard(a.Position); // forget the farm creature it was attacking
            if (a.TryGetComponent(out Selectable selectable)) selectable.enabled = true;
        }

        if (CreatureManager.Instance == null) return;
        foreach (Animal a in returned)
        {
            if (a.CreatureBrain.Engaged || !a.CreatureBrain.enabled) continue;
            Creature target = CreatureManager.Instance.GetNearestEnemy(a.Team, a.Position, a.Position, float.MaxValue);
            if (target != null) a.CreatureBrain.Attack(target);
        }
    }

    // Instantiates the mind-control collar prefab onto the animal's Socket_Neck slot, scaled by Animal.neckSize.
    GameObject PutOnCollar(Animal animal)
    {
        if (collarPrefab == null) return null;

        Transform socket = null;
        foreach (Transform t in animal.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "Socket_Neck")
            {
                socket = t;
                break;
            }
        }

        Transform parent = socket != null ? socket : animal.transform;
        GameObject collar = Instantiate(collarPrefab, parent, false);
        collar.transform.localPosition = Vector3.zero;
        collar.transform.localRotation = Quaternion.identity;
        collar.transform.localScale = Vector3.one * animal.NeckSize;

        return collar;
    }
}
