using UnityEngine;

namespace MegaCrush.Spawner.Scenario
{
    [DisallowMultipleComponent]
    public sealed class ScenarioPoint : MonoBehaviour
    {
        public enum Category
        {
            Bots,
            Objectives,
            Pickups
        }

        [SerializeField] private string pointId;
        [SerializeField] private Category category = Category.Bots;
        [SerializeField] private SpawnHintPoint hint;

        public string PointId => pointId;
        public Category PointCategory => category;
        public SpawnHintPoint Hint => hint;

#if UNITY_EDITOR
        private void Reset()
        {
            if (!hint) hint = GetComponent<SpawnHintPoint>();
        }
#endif
    }
}