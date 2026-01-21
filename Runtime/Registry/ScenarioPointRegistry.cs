using System;
using System.Collections.Generic;
using UnityEngine;

namespace MegaCrush.Spawner.Scenario
{
    [DisallowMultipleComponent]
    public sealed class ScenarioPointRegistry : MonoBehaviour
    {
        [Header("Debug")]
        public bool DebugLogs;

        private readonly List<ScenarioPoint> _points = new();
        private readonly Dictionary<string, ScenarioPoint> _byId = new();

        private readonly List<ScenarioPoint> _bots = new();
        private readonly List<ScenarioPoint> _objectives = new();
        private readonly List<ScenarioPoint> _pickups = new();

        public IReadOnlyList<ScenarioPoint> Points => _points;

        public event Action PointsChanged;

        public bool Register(ScenarioPoint p)
        {
            if (!p) return false;
            if (_points.Contains(p)) return false;

            _points.Add(p);

            var id = p.PointId;
            if (!string.IsNullOrEmpty(id) && !_byId.ContainsKey(id))
                _byId.Add(id, p);
            else if (DebugLogs && !string.IsNullOrEmpty(id))
                Debug.LogWarning($"[ScenarioPointRegistry] Duplicate PointId '{id}'. First wins.", p);

            AddToBucket(p);

            PointsChanged?.Invoke();

            if (DebugLogs)
                Debug.Log($"[ScenarioPointRegistry] Registered '{id}'. total={_points.Count}", p);

            return true;
        }

        public bool Unregister(ScenarioPoint p)
        {
            if (!p) return false;

            bool removed = _points.Remove(p);

            var id = p.PointId;
            if (!string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out var mapped) && mapped == p)
                _byId.Remove(id);

            RemoveFromBucket(p);

            if (removed)
            {
                PointsChanged?.Invoke();

                if (DebugLogs)
                    Debug.Log($"[ScenarioPointRegistry] Unregistered '{id}'. total={_points.Count}", this);
            }

            return removed;
        }

        public bool TryGetById(string pointId, out ScenarioPoint p)
        {
            if (!string.IsNullOrEmpty(pointId) && _byId.TryGetValue(pointId, out p) && p)
                return true;

            p = null;
            return false;
        }

        public IReadOnlyList<ScenarioPoint> GetPoints(ScenarioPoint.Category category)
        {
            return category switch
            {
                ScenarioPoint.Category.Bots => _bots,
                ScenarioPoint.Category.Objectives => _objectives,
                ScenarioPoint.Category.Pickups => _pickups,
                _ => _bots
            };
        }

        private void AddToBucket(ScenarioPoint p)
        {
            switch (p.PointCategory)
            {
                case ScenarioPoint.Category.Bots: _bots.Add(p); break;
                case ScenarioPoint.Category.Objectives: _objectives.Add(p); break;
                case ScenarioPoint.Category.Pickups: _pickups.Add(p); break;
            }
        }

        private void RemoveFromBucket(ScenarioPoint p)
        {
            switch (p.PointCategory)
            {
                case ScenarioPoint.Category.Bots: _bots.Remove(p); break;
                case ScenarioPoint.Category.Objectives: _objectives.Remove(p); break;
                case ScenarioPoint.Category.Pickups: _pickups.Remove(p); break;
            }
        }
    }
}
