using UnityEngine;

namespace MegaCrush.Spawner.Scenario
{
    public readonly struct ScenarioSelectionContext
    {
        public readonly string GroupId;
        public readonly int Alive;
        public readonly int Target;
        public readonly Vector3 PlayerPos;
        public readonly Pcg32 Rng;

        public ScenarioSelectionContext(string groupId, int alive, int target, Vector3 playerPos, Pcg32 rng)
        {
            GroupId = groupId;
            Alive = alive;
            Target = target;
            PlayerPos = playerPos;
            Rng = rng;
        }
    }

    public interface IScenarioSpawnSelector
    {
        bool TrySelect(ScenarioGroupConfig group, in ScenarioSelectionContext ctx, out SpawnEntry entry);
    }
}