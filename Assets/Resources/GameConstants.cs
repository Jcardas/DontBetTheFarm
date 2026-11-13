using UnityEngine;

[CreateAssetMenu(fileName = "GameConstants", menuName = "Config/GameConstants")]
public class GameConstants : ScriptableObject
{
    private static GameConstants _instance;

    public static GameConstants Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<GameConstants>("GameConstants");
                if (_instance == null)
                {
                    Debug.LogError("[GameConstants] Failed to load GameConstants from Resources!");
                }
            }

            return _instance;
        }
    }

    public enum Team
    {
        Farm,
        Alien,
        Neutral
    }

    public static bool IsHostile(GameConstants.Team a, GameConstants.Team b)
    {
        if (a == b) return false;

        return (a, b) switch
        {
            // Aliens hunt Players, Friendly Animals, and Livestock
            (Team.Alien, Team.Farm) => true,
            (Team.Alien, Team.Neutral) => true,
            (Team.Farm, Team.Alien) => true,
            (Team.Neutral, Team.Alien) => true,

            // Wildlife/Animals do not hunt Players (unless aggressive)
            (Team.Neutral, Team.Farm) => false,
            (Team.Farm, Team.Neutral) => false,

            _ => false
        };

    }

    [System.Serializable]
    public struct CreatureStats
    {
        [SerializeField] private float speed;
        [SerializeField] private int maxHealth;
        [SerializeField] private float attackSpeed;
        [SerializeField] private Team team; // Shows up as a native dropdown menu in the Inspector

        public float Speed => speed;
        public int MaxHealth => maxHealth;
        public float AttackSpeed => attackSpeed;
        public Team Team => team;
    }

    [System.Serializable]
    public struct TeamColorConfig
    {
        public Color selectedColor;
        public Color hiddenColor;
        public Color hoverColor;
    }

    [System.Serializable]
    public struct TeamConfig
    {
        public Team team;
        public string displayName;
        public TeamColorConfig teamColorConfig;
    }

    [Header("Global Team Visuals/Settings")] 
    [SerializeField] private TeamConfig[] teamDefinitions;
    [SerializeField] private float hiddenOutlineWidth = 2f;
    [SerializeField] private float litOutlineWidth = 2f;
    
    public float HiddenOutlineWidth => hiddenOutlineWidth;
    public float LitOutlineWidth => litOutlineWidth;

    [Header("Farmer Settings")] [SerializeField]
    private CreatureStats baseFarmerStats;

    [Header("Animal Settings")] [SerializeField]
    private CreatureStats baseAnimalStats;

    [SerializeField] private int baseBreedingCost; // Cost in hay to breed animals
    [SerializeField] private int baseHealCost; // Cost in hay to heal animals

    [Header("Alien Settings")] [SerializeField]
    private CreatureStats baseAlienStats;

    public CreatureStats BaseFarmer => baseFarmerStats;
    public CreatureStats BaseAnimal => baseAnimalStats;
    public int BaseBreedingCost => baseBreedingCost;
    public int BaseHealCost => baseHealCost;
    public CreatureStats BaseAlien => baseAlienStats;

    [System.Serializable]
    public struct BuildingStats
    {
        [SerializeField] private int maxHealth;
        [SerializeField] private int buildingCost;
        [SerializeField] private ResourceType resourceType;

        public int MaxHealth => maxHealth;
        public int BuildingCost => buildingCost;
        public ResourceType ResourceType => resourceType;

    }

    [Header("Building Settings")] [SerializeField]
    private int MaxNumberOfBuildings;

    [SerializeField] private BuildingStats baseBuildingStats;

    public BuildingStats BaseBuilding => baseBuildingStats;

    [Header("Animal Pen Settings")] [SerializeField]
    private int maxAnimalsInPen;

    [SerializeField] private BuildingStats baseAnimalPenStats;

    public BuildingStats BaseAnimalPen => baseAnimalPenStats;
    public int MaxAnimalsInPen => maxAnimalsInPen;

    [Header("Wheat Field Settings")] [SerializeField]
    private BuildingStats baseWheatFieldStats;

    public BuildingStats BaseWheatField => baseWheatFieldStats;
    
    /// <summary>
    /// Returns the TeamColorConfig for the specified team.
    /// If no configuration is found, a warning is logged and
    /// a default value is returned.
    /// </summary>
    /// <param name="team"></param>
    /// <returns></returns>
    public TeamColorConfig GetTeamColors(Team team)
    {
        if (teamDefinitions != null)
        {
            for (int i = 0; i < teamDefinitions.Length; i++)
            {
                if (teamDefinitions[i].team == team)
                    return teamDefinitions[i].teamColorConfig;
            }
        }

        Debug.LogWarning($"[GameConstants] No TeamConfig found for team: {team}");
        return default;
    }

}