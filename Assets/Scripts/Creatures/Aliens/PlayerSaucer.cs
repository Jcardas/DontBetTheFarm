using System.Collections.Generic;
using UnityEngine;

// The player's saucer. It hovers at home (where it is placed, beside the barn) while aliens are still standing.
// A few seconds after the last one goes down it sweeps the field: beams up every knocked-out alien, brings them all
// home in one trip, and each one becomes research (Technology). Its lights and beam are blue so it never reads as an
// enemy.
public class PlayerSaucer : UFO
{
    [Tooltip("Seconds with no enemy left standing before it goes out to collect.")]
    [SerializeField] float calmDelay = 3f;
    [Tooltip("Research each knocked-out alien is worth.")]
    [SerializeField] int researchPerAlien = 1;

    readonly List<Creature> cargo = new();
    Vector3 home;
    bool sweeping;
    float calmSince = -1f;

    protected override void Awake()
    {
        base.Awake();
        home = transform.position - Vector3.up * hoverHeight;
        exitPoint = home;
        PaintBlue();
    }

    protected override void OnIdle()
    {
        if (!sweeping)
        {
            FlyTo(home);
            sweeping = CalmLongEnough();
            return;
        }

        Creature next = NearestDownedAlien();
        if (next != null) Claim(next);
        else if (cargo.Count > 0) state = State.Carry; // home with the catch
        else sweeping = false;
    }

    // Stacks the catch under the hull and goes for the next body.
    protected override void OnAboard()
    {
        victim.transform.localPosition = storedOffset + Vector3.down * 0.5f * cargo.Count;
        cargo.Add(victim);
        victim = null;
        state = State.Idle;
    }

    // Home: every alien aboard becomes research.
    protected override void Deliver()
    {
        foreach (Creature c in cargo)
        {
            Release(c);
            Destroy(c.gameObject);
        }
        if (ResourceManager.Instance != null && cargo.Count > 0)
            ResourceManager.Instance.Deposit(ResourceType.Technology, cargo.Count * researchPerAlien);
        cargo.Clear();
        sweeping = false;
        state = State.Idle;
    }

    // True once no enemy has been standing for calmDelay seconds and there is a knocked-out alien to collect.
    bool CalmLongEnough()
    {
        if (CreatureManager.Instance == null) return false;
        bool fighting = false, bodies = false;
        foreach (Creature c in CreatureManager.Instance.GetEnemies(GameConstants.Team.Farm))
        {
            if (!c.Down) fighting = true;
            else if (c is Alien && !IsClaimed(c)) bodies = true;
        }
        if (fighting || !bodies)
        {
            calmSince = -1f;
            return false;
        }
        if (calmSince < 0f) calmSince = Time.time;
        if (Time.time - calmSince < calmDelay) return false;
        calmSince = -1f;
        return true;
    }

    Creature NearestDownedAlien()
    {
        if (CreatureManager.Instance == null) return null;
        Creature best = null;
        foreach (Creature c in CreatureManager.Instance.GetEnemies(GameConstants.Team.Farm))
            if (c is Alien && c.Down && !IsClaimed(c) &&
                (best == null || Vector3.Distance(transform.position, c.Position) < Vector3.Distance(transform.position, best.Position)))
                best = c;
        return best;
    }

    // Justin's model with blue lights, spotlight and beam.
    void PaintBlue()
    {
        Color blue = new(0.15f, 0.55f, 1f);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", blue);
        block.SetColor("_EmissionColor", blue);
        foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            if (r.sharedMaterials.Length > 1) r.SetPropertyBlock(block, 1); // slot 1 is UFO Lights
        foreach (var l in GetComponentsInChildren<Light>(true)) l.color = blue;
        foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = new Color(blue.r, blue.g, blue.b, main.startColor.color.a);
        }
    }
}
