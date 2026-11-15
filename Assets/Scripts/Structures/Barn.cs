using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The farmer can hide in the barn: right-click it with him picked and he runs to the doors and goes inside.
// Right-clicking the barn while he is in offers the barn's menu or taking him out; if the barn is knocked down he
// comes out on his own. With nobody inside, the barn's menu is the mutation menu.
[RequireComponent(typeof(BarnHangar))]
public class Barn : Buildable, IContextInteractable
{
    Farmer heading; // on his way to the doors
    Farmer inside;
    
    [SerializeField] static BarnResearchUIController barnResearchUIController;

    public bool HasFarmer => inside != null;

    // The ground in front of the doors.
    public Vector3 Door
    {
        get
        {
            Vector3 threshold = GetComponent<BarnHangar>().ThresholdPosition;
            return NavMesh.SamplePosition(threshold, out NavMeshHit hit, 5f, NavMesh.AllAreas) ? hit.position : threshold;
        }
    }

    // He goes in once he reaches the doors, unless he is ordered somewhere else first.
    public void TakeIn(Farmer farmer) => heading = farmer;

    void Update()
    {
        if (heading == null) return;
        var agent = heading.GetComponent<NavMeshAgent>();
        Vector3 door = Door;
        if (heading.Down || Down || Flat(agent.destination - door) > 1f)
        {
            heading = null;
            return;
        }
        if (Flat(heading.transform.position - door) > 1f) return;
        inside = heading;
        heading = null;
        inside.gameObject.SetActive(false);
    }

    void LetOut()
    {
        if (inside == null) return;
        inside.transform.position = Door;
        inside.gameObject.SetActive(true);
        inside = null;
    }

    protected override void HandleDestroyed()
    {
        base.HandleDestroyed();
        LetOut();
    }

    static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    static void OpenMenu()
    {
        if (barnResearchUIController == null)
            barnResearchUIController = FindAnyObjectByType<BarnResearchUIController>(FindObjectsInactive.Include);

        if (barnResearchUIController != null)
        {
            barnResearchUIController.OpenMenu();
        }
        else
        {
            Debug.LogWarning("[Barn] BarnResearchUIController not found in scene!");
        }
    }

    #region IContextInteractable Implementation

    public IReadOnlyList<ContextActionData> GetContextActions()
    {
        var actions = new List<ContextActionData>();

        actions.Add(new ContextActionData("Open Barn", OpenMenu));

        // Only show the "Take Farmer Out" action if there's a farmer inside the barn
        if(HasFarmer)
        {
            actions.Add(new ContextActionData("Take Farmer Out", LetOut));
        }

        return actions;
    }

    #endregion
}
