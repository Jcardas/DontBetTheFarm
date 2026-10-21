using UnityEngine;

public class AnimalPen : Buildable
{
    [Header("Animal Pen Settings")]
    private int MaxAnimals; // Arbitrary limit for the maximum number of animals in the pen
    
    private FarmAnimal[] animalsInPen; // Array to hold the animals in the pen
    
    public override (ResourceType, int) CostAndResource => 
  
        // GameConstants base material and base cost for wheat field
        (GameConstants.Instance.BaseAnimalPen.ResourceType, GameConstants.Instance.BaseAnimalPen.BuildingCost);


    protected override void Reset()
    {
        base.Reset();
        MaxAnimals = GameConstants.Instance.MaxAnimalsInPen;
        animalsInPen = new FarmAnimal[GameConstants.Instance.MaxAnimalsInPen]; // Initialize the array with the maximum number of animals
    }
}
