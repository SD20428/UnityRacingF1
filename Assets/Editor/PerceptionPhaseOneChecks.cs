using System;
using UnityEngine;
using UnityEditor;

namespace Overdrive.EditorTools
{
    // Deterministic acceptance checks, callable from the menu or Pipeline eval.
    public static class PerceptionPhaseOneChecks
    {
        public static string RunLive()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play mode required");
            var race = UnityEngine.Object.FindFirstObjectByType<RaceManager>();
            int checkedCars = 0, seenPlayer = 0;
            foreach (var entry in race.Racers)
            {
                if (!entry.driver) continue;
                var sensor = entry.driver.Perception;
                if (!sensor) throw new Exception("AI sensor missing");
                var car = entry.vehicle;
                Vector3 position = car.Body.position, velocity = car.Body.linearVelocity;
                float throttle = car.Throttle, brake = car.Brake, steer = car.Steering;
                sensor.Sample();
                if (!sensor.ReferenceValid) throw new Exception("Invalid reference: " + car.name);
                foreach (var item in sensor.Observations)
                {
                    if (item.vehicle == car || !item.vehicle.isActiveAndEnabled) throw new Exception("Invalid observed car");
                    if (item.vehicle == race.player) seenPlayer++;
                }
                if (car.Body.position != position || car.Body.linearVelocity != velocity ||
                    car.Throttle != throttle || car.Brake != brake || car.Steering != steer)
                    throw new Exception("Perception changed controls/physics");
                bool wasEnabled = entry.driver.enablePerception;
                try
                {
                    entry.driver.enablePerception = false; sensor.Sample();
                    if (sensor.HasLead || sensor.ReferenceValid || sensor.Observations.Count != 0)
                        throw new Exception("Feature flag did not clear observations");
                }
                finally { entry.driver.enablePerception = wasEnabled; sensor.Sample(); }
                checkedCars++;
            }
            if (checkedCars == 0 || seenPlayer == 0 || race.player.GetComponent<VehiclePerception>())
                throw new Exception("AI/player coverage failed");
            return $"PASS: {checkedCars} AI sensors; player observed by {seenPlayer}; self exclusion, controls unchanged, runtime flag verified";
        }
        [MenuItem("Overdrive/AI/Verify Phase 1 geometry")]
        public static string Run()
        {
            int checks = 0;
            Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception(name); checks++; };
            var root = new GameObject("Phase1 temporary geometry");
            root.SetActive(false);
            try
            {
                var path = root.AddComponent<WaypointPath>();
                path.points = new Transform[4];
                Vector3[] positions = { Vector3.zero, new Vector3(0,0,100), new Vector3(4,0,100), new Vector3(4,0,0) };
                for (int i = 0; i < positions.Length; i++)
                {
                    var point = new GameObject("point").transform;
                    point.SetParent(root.transform); point.position = positions[i]; path.points[i] = point;
                }
                var progress = root.AddComponent<CircuitProgress>(); progress.checkpoints = path.points;
                var adapter = new TrackReferenceAdapter(path);
                check(adapter.TryProject(progress, new Vector3(1,0,20), out var own), "Valid projection");
                check(Mathf.Abs(own.distance - 20) < .001f && Mathf.Abs(own.lateral - 1) < .001f, "Metric s/d");
                check(adapter.TryProject(progress, new Vector3(4,0,20), out var branch) && branch.segment == 0,
                    "Checkpoint continuity must beat nearby hairpin branch");
                check(Mathf.Abs(TrackReferenceAdapter.SignedDistance(205, 2, 208) - 5) < .001f, "Forward lap wrap");
                check(Mathf.Abs(TrackReferenceAdapter.SignedDistance(2, 205, 208) + 5) < .001f, "Rear lap wrap");
                check(!adapter.TryProject(progress, new Vector3(float.NaN,0,0), out _), "Reject NaN");
                check(float.IsNaN(TrackReferenceAdapter.SignedDistance(0,1,0)), "Reject zero length");
                progress.checkpoints = new Transform[4];
                check(!adapter.TryProject(progress, Vector3.zero, out _), "Reject different circuit");
                check(!new TrackReferenceAdapter(null).Valid, "Reject missing path");
                string result = $"PASS: {checks} Phase 1 geometry checks";
                Debug.Log(result); return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
