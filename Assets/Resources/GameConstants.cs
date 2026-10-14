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
    public struct TeamConfig
    {
        public Team team;
        public string displayName;
        public Color teamColor;
    }

    [Header("Global Team Visuals/Settings")]
    [SerializeField] private TeamConfig[] teamDefinitions;

    [Header("Farmer Settings")]
    [SerializeField] private CreatureStats baseFarmerStats;

    [Header("Animal Settings")]
    [SerializeField] private CreatureStats baseAnimalStats;

    [Header("Alien Settings")]
    [SerializeField] private CreatureStats baseAlienStats;

    public CreatureStats BaseFarmer => baseFarmerStats;
    public CreatureStats BaseAnimal => baseAnimalStats;
    public CreatureStats BaseAlien => baseAlienStats;

    public Color GetTeamColor(Team type)
    {
        if (teamDefinitions != null)
        {
            for (int i = 0; i < teamDefinitions.Length; i++)
            {
                if (teamDefinitions[i].team == type) return teamDefinitions[i].teamColor;
            }
        }
        return Color.white;
    }
}