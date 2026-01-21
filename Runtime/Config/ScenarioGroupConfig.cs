using UnityEngine;

namespace MegaCrush.Spawner.Scenario
{
    [CreateAssetMenu(fileName = "Scenario Group", menuName = "MegaCrush/Spawner/Scenario/Create Scenario Group", order = 10)]
    public sealed class ScenarioGroupConfig : ScriptableObject
    {
        [Header("Identity")]
        public string GroupId = "Bots";

        [Header("Target")]
        [Min(0)] public int TargetCount = 10;

        [Header("Points")]
        public ScenarioPoint.Category PointCategory = ScenarioPoint.Category.Bots;

        [Header("Spawn Entries (fallback)")]
        public SpawnEntry[] Entries;

        [Header("Selection Logic (optional)")]
        public UnityEngine.Object Selector; // must implement IScenarioSpawnSelector

        [Header("Respawn")]
        [Min(0f)] public float RespawnCooldownSeconds = 2f;

        [Header("Placement")]
        [Min(0f)] public float ForcedHintRange = 0f;
        public bool ForcedHintExclusive = true;
    }
}