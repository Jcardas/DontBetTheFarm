using UnityEngine;

// Shows a creature fighting for the side it didn't start on, beyond its colour. An alien the player's saucer won over
// wears the farmer's straw hat; a stolen animal the aliens sent back stands on a red ring. Creature.SetTeam calls it.
public static class SideMark
{
    const string HatName = "Farm Hat";
    static GameObject hatPrefab;

    public static void Refresh(Creature c)
    {
        bool wonOver = c is Alien && c.Team == GameConstants.Team.Farm;
        bool turned = c is FarmAnimal && c.Team == GameConstants.Team.Alien;

        Transform hat = c.transform.Find(HatName);
        if (wonOver && hat == null) hat = PutOnHat(c);
        if (hat) hat.gameObject.SetActive(wonOver);

        if (!c.TryGetComponent(out SelectionHighlight highlight))
        {
            if (!turned) return;
            highlight = c.gameObject.AddComponent<SelectionHighlight>();
        }
        highlight.Turned = turned;
    }

    // The farmer's own hat (made from his prefab), sat on top of the body and sized to it.
    static Transform PutOnHat(Creature c)
    {
        if (hatPrefab == null) hatPrefab = Resources.Load<GameObject>("SideMark/FarmHat");
        if (hatPrefab == null) return null;
        Physics.SyncTransforms(); // the body was just set down from the saucer; its collider bounds lag a frame behind
        Bounds body = c.Body != null ? c.Body.bounds : new Bounds(c.transform.position, Vector3.one);
        Transform hat = Object.Instantiate(hatPrefab, c.transform).transform;
        hat.name = HatName;
        hat.position = new Vector3(body.center.x, body.max.y - 0.05f, body.center.z);
        float size = Mathf.Clamp(2f * Mathf.Max(body.extents.x, body.extents.z) / 0.6f, 0.6f, 2f);
        Vector3 parent = c.transform.lossyScale;
        hat.localScale = new Vector3(size / parent.x, size / parent.y, size / parent.z);
        return hat;
    }
}
