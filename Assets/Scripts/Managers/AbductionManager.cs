using System.Collections.Generic;
using UnityEngine;

// Sends an enemy saucer for each knocked-out farm creature no saucer has claimed yet, once it has been down for
// `delay` seconds, up to `maxSaucers` at a time. Each saucer comes in from the map-edge point nearest its target
// and leaves through the same point.
public class AbductionManager : MonoBehaviour
{
    [SerializeField] UFO enemySaucerPrefab;
    [Tooltip("Where enemy saucers come in and leave, around the map edge.")]
    [SerializeField] Transform[] edgePoints;
    [SerializeField] float delay = 4f;
    [SerializeField] int maxSaucers = 3;
    [SerializeField] float spawnHeight = 3.5f;

    readonly Dictionary<Creature, float> downSince = new();
    readonly List<UFO> saucers = new();
    readonly List<Creature> stale = new();

    void Update()
    {
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
