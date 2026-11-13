using System;
using System.Collections.Generic;
using UnityEngine;

// Sends an enemy saucer for each knocked-out farm creature once it has been down for `delay` seconds, up to
// `maxSaucers` at a time. Each saucer comes out of the mothership nearest its target and takes it back there.
public class AbductionManager : MonoBehaviour
{
    [SerializeField] UFO saucerPrefab;
    [Tooltip("Seconds a farm creature lies knocked out before a saucer comes for it.")]
    [SerializeField] float delay = 4f;
    [SerializeField] int maxSaucers = 1;

    readonly Dictionary<Creature, float> downSince = new();
    readonly List<UFO> saucers = new();

    void Update()
    {
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
}
