using System.Collections;
using System.Collections.Generic;
using MegaCrush.Spawner;
using UnityEngine;

namespace MegaCrush.Spawner.Scenario
{
    [DisallowMultipleComponent]
    public sealed class ScenarioSpawnerAddon : MonoBehaviour, IRuntimeSpawnerAddon, IRuntimeSpawnerPrefabSource
    {
        [Header("Scheduling")]
        [Min(0.1f)] public float DriverTickSeconds = 0.5f;
        [Min(0)] public int MaxSpawnsPerTick = 4;

        [Header("Discovery")]
        public bool AutoDiscoverGroups = true;
        public bool AutoDiscoverPoints = true;

        [Header("Config")]
        [Tooltip("If registry is not used, these groups are used (editor-friendly).")]
        public ScenarioGroupConfig[] Groups;

        [Header("Debug")]
        public bool DebugLogs;

        private RuntimeSpawner _spawner;
        private ISpawnExecutor _exec;
        private PopulationTracker _pop;
        private Transform _player;

        private ScenarioPointRegistry _pointRegistry;

        private readonly Dictionary<string, int> _aliveByGroupId = new();
        private readonly Dictionary<string, float> _cooldownUntilByPointId = new();
        private readonly Dictionary<GameObject, string> _pointIdByInstance = new();

        private Coroutine _loop;
        private bool _running;

        public void Init(RuntimeSpawner spawner)
        {
            _spawner = spawner;
            _exec = spawner ? spawner.Executor : null;
            _pop = spawner ? spawner.Population : null;
            _player = spawner ? spawner.PlayerTransform : null;

            _pointRegistry = spawner ? spawner.GetComponent<ScenarioPointRegistry>() : null;

            if (_pop != null)
            {
                _pop.OnSpawnedWithMeta -= OnSpawnedWithMeta;
                _pop.OnDespawned -= OnDespawned;

                _pop.OnSpawnedWithMeta += OnSpawnedWithMeta;
                _pop.OnDespawned += OnDespawned;
            }

            if (DebugLogs)
                Debug.Log("[ScenarioSpawnerAddon] Init complete.", this);
        }

        public void Begin()
        {
            if (_running) return;
            if (_spawner == null || _exec == null || _pop == null) return;

            _running = true;

            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(Loop());

            if (DebugLogs)
                Debug.Log("[ScenarioSpawnerAddon] Begin.", this);
        }

        public void End()
        {
            if (_loop != null)
            {
                StopCoroutine(_loop);
                _loop = null;
            }

            _running = false;

            if (_pop != null)
            {
                _pop.OnSpawnedWithMeta -= OnSpawnedWithMeta;
                _pop.OnDespawned -= OnDespawned;
            }

            if (DebugLogs)
                Debug.Log("[ScenarioSpawnerAddon] End.", this);
        }

        public IEnumerable<SpawnEntry> EnumerateSpawnEntries()
        {
            if (Groups == null) yield break;

            for (int i = 0; i < Groups.Length; i++)
            {
                var g = Groups[i];
                if (!g || g.Entries == null) continue;

                for (int e = 0; e < g.Entries.Length; e++)
                    if (g.Entries[e]) yield return g.Entries[e];
            }
        }

        public IEnumerable<GameObject> EnumeratePrefabs()
        {
            var seen = new HashSet<GameObject>();

            if (Groups == null) yield break;

            for (int i = 0; i < Groups.Length; i++)
            {
                var g = Groups[i];
                if (!g || g.Entries == null) continue;

                for (int e = 0; e < g.Entries.Length; e++)
                {
                    var entry = g.Entries[e];
                    if (!entry) continue;

                    foreach (var prefab in RuntimeSpawnerPrefabUtil.EnumerateValidPrefabs(entry))
                        if (prefab && seen.Add(prefab))
                            yield return prefab;
                }
            }
        }

        private void OnSpawnedWithMeta(GameObject go, SpawnMeta meta)
        {
            if (meta.Source != SpawnSource.FixedPoints) return;
            if (string.IsNullOrEmpty(meta.SourceName)) return;

            _aliveByGroupId.TryGetValue(meta.SourceName, out var n);
            _aliveByGroupId[meta.SourceName] = n + 1;
        }

        private void OnDespawned(GameObject go, SpawnMeta meta)
        {
            if (meta.Source != SpawnSource.FixedPoints) return;
            if (string.IsNullOrEmpty(meta.SourceName)) return;

            _aliveByGroupId.TryGetValue(meta.SourceName, out var n);
            _aliveByGroupId[meta.SourceName] = Mathf.Max(0, n - 1);

            // release occupancy/cooldown bookkeeping if we stored a point id
            if (go && _pointIdByInstance.TryGetValue(go, out var pid))
            {
                _pointIdByInstance.Remove(go);

                // cooldown is handled by point id (stable even if hint component is pooled/reused)
                // We'll apply respawn cooldown in the tick when selecting; here we just ensure we
                // don't carry stale "occupied" state.
            }
        }

        private IEnumerator Loop()
        {
            var wait = new WaitForSeconds(DriverTickSeconds);

            while (_running && _spawner && _exec != null)
            {
                int budget = MaxSpawnsPerTick;
                var playerPos = _player ? _player.position : Vector3.zero;

                if (Groups == null || Groups.Length == 0)
                {
                    yield return wait;
                    continue;
                }

                for (int i = 0; i < Groups.Length; i++)
                {
                    if (budget <= 0) break;

                    var g = Groups[i];
                    if (!g) continue;

                    int alive = _aliveByGroupId.TryGetValue(g.GroupId, out var n) ? n : 0;
                    int desired = g.TargetCount;

                    int delta = desired - alive;
                    if (delta <= 0) continue;

                    int toSpawn = Mathf.Min(delta, budget);

                    for (int s = 0; s < toSpawn; s++)
                    {
                        if (!TryPickEntry(g, playerPos, out var entry))
                            break;

                        if (!TryPickPoint(g, out var point, out var pointId))
                            break;

                        // Build SpawnContext
                        var ctx = new SpawnContext
                        {
                            IsGlobal = true,
                            PlayerPos = playerPos,
                            Source = SpawnSource.FixedPoints,
                            SourceName = g.GroupId,
                            HintTags = null,
                            SpawnTag = default
                        };

                        // Force hint via core extension
                        ctx.SetExtension(new ForcedSpawnHintContext(
                            hint: point,
                            exclusive: g.ForcedHintExclusive,
                            range: g.ForcedHintRange));

                        var spawned = _exec.Spawn(entry, ctx);
                        if (!spawned)
                        {
                            // short throttle so we don't thrash the same point repeatedly
                            _cooldownUntilByPointId[pointId] = Time.time + 0.5f;
                            continue;
                        }

                        _pointIdByInstance[spawned] = pointId;
                        _cooldownUntilByPointId[pointId] = Time.time + g.RespawnCooldownSeconds;

                        budget--;
                        if (budget <= 0) break;
                    }
                }

                yield return wait;
            }
        }

        private bool TryPickEntry(ScenarioGroupConfig g, Vector3 playerPos, out SpawnEntry entry)
        {
            entry = null;

            // Deterministic-ish rng per attempt (fine for now; can be improved to per-group tick seed)
            var rng = new Pcg32((uint)Time.frameCount, (uint)g.GetInstanceID());

            var selector = g.Selector as IScenarioSpawnSelector;
            if (selector != null)
            {
                var ctx = new ScenarioSelectionContext(g.GroupId, alive: 0, target: g.TargetCount, playerPos: playerPos, rng: rng);
                if (selector.TrySelect(g, in ctx, out entry) && entry)
                    return true;
            }

            // Fallback: first valid entry
            if (g.Entries != null)
            {
                for (int i = 0; i < g.Entries.Length; i++)
                {
                    var e = g.Entries[i];
                    if (e) { entry = e; return true; }
                }
            }

            return false;
        }

        private bool TryPickPoint(ScenarioGroupConfig g, out SpawnHintPoint hint, out string pointId)
        {
            hint = null;
            pointId = null;

            if (!_pointRegistry)
            {
                // optional fallback for early bring-up: scan scene
                if (!AutoDiscoverPoints) return false;

#if UNITY_2023_1_OR_NEWER
                var points = Object.FindObjectsByType<ScenarioPoint>(FindObjectsSortMode.None);
#else
                var points = Object.FindObjectsOfType<ScenarioPoint>();
#endif
                for (int i = 0; i < points.Length; i++)
                {
                    var p = points[i];
                    if (!p || p.PointCategory != g.PointCategory) continue;
                    if (!p.Hint) continue;

                    if (IsPointReady(p.PointId))
                    {
                        hint = p.Hint;
                        pointId = p.PointId;
                        return true;
                    }
                }

                return false;
            }

            var candidates = _pointRegistry.GetPoints(g.PointCategory);
            if (candidates == null || candidates.Count == 0)
                return false;

            // Simple selection: first ready point (upgrade later to rr/weighted)
            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
                if (!p || !p.Hint) continue;
                if (string.IsNullOrEmpty(p.PointId)) continue;

                if (!IsPointReady(p.PointId))
                    continue;

                hint = p.Hint;
                pointId = p.PointId;
                return true;
            }

            return false;
        }

        private bool IsPointReady(string pointId)
        {
            if (string.IsNullOrEmpty(pointId)) return false;

            if (_cooldownUntilByPointId.TryGetValue(pointId, out var until))
                return Time.time >= until;

            return true;
        }
    }
}
