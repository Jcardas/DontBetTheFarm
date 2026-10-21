using UnityEngine;

public class AnimalPen : Buildable
{
    private int MaxAnimals = 5; // Arbitrary limit for the maximum number of animals in the pen
    
    private FarmAnimal[] animalsInPen; // Array to hold the animals in the pen

    protected override void Reset()
    {
        base.Reset();
        maxHealth = GameConstants.Instance.BaseBuilding.MaxHealth; 
        buildingCost = GameConstants.Instance.BaseBuilding.BuildingCost;
        animalsInPen = new FarmAnimal[GameConstants.Instance.MaxAnimalsInPen]; // Initialize the array with the maximum number of animals
    }
    
    
}
