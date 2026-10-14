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
                    Debug.LogError(
                        "[GameConstants] Failed to load GameConstants from Resources! Ensure 'GameConstants.asset' exists inside a 'Resources' folder.");
                }
            }

            return _instance;
        }
    }

    [System.Serializable]
    public struct CharacterStats
    {
        [SerializeField] private float speed;
        [SerializeField] private int maxHealth;
        [SerializeField] private float attackSpeed;

        public float Speed => speed;
        public int MaxHealth => maxHealth;
        public float AttackSpeed => attackSpeed;
    }

    [Header("Farmer Settings")] [SerializeField]
    private CharacterStats farmerStats;

    [Header("Animal Settings")] [SerializeField]
    private CharacterStats baseCreatureStats;

    [Header("Alien Settings")] [SerializeField]
    private CharacterStats alienStats;

    // Public Getters
    public CharacterStats Farmer => farmerStats;
    public CharacterStats BaseCreature => baseCreatureStats;
    public CharacterStats Alien => alienStats;
}