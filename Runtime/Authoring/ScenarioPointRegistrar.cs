using UnityEngine;

namespace MegaCrush.Spawner.Scenario
{
    [DisallowMultipleComponent]
    public sealed class ScenarioPointRegistrar : MonoBehaviour
    {
        private ScenarioPointRegistry _registry;
        private ScenarioPoint _point;

        private void OnEnable()
        {
            _point = GetComponent<ScenarioPoint>();
            if (!_point) return;

            _registry ??= FindFirstObjectByType<ScenarioPointRegistry>();
            if (_registry) _registry.Register(_point);
        }

        private void OnDisable()
        {
            if (_registry && _point) _registry.Unregister(_point);
        }
    }
}