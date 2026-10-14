using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A herd is a group of creatures that move together and share a common goal.
/// A herd shares a common pen and a common target, which the herd members will default to attacking if they have no other orders.
/// The herd can be commanded to move to a new location or attack a specific target,
/// and the members of the herd will follow those orders.
/// </summary>
public class Herd : MonoBehaviour
{
    
    // The list of creatures that are members of the herd.
    private List<Creature> herdMembers = new List<Creature>();
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
