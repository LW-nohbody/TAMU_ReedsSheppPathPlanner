// res://Scripts/Services/Scheduling/RadialScheduler.cs
using System;
using System.Collections.Generic;
using Godot;
using DigSim3D.App;
using DigSim3D.Domain;
using DigSim3D.App.Vehicles;

namespace DigSim3D.Services
{
    public sealed class RadialScheduler
    {
        /// <summary>
        /// Original API – unchanged behavior (no obstacle logic).
        /// </summary>
        public IReadOnlyList<(Vector3 digPos, float approachYaw)> PlanFirstDigTargets(
            IReadOnlyList<VehicleAgent> vehicles,
            TerrainDisk terrain,
            Vector3 center,
            DigScoring cfg,
            float keepoutR = 2.0f,
            bool randomizeOrder = true)
        {
            // Calls the new overload with empty obstacles + zero inflation for exact compatibility.
            return PlanFirstDigTargets(
                vehicles, terrain, center, cfg,
                keepoutR, randomizeOrder,
                obstacles: null, inflation: 0f
            );
        }

        /// <summary>
        /// New API – identical to original, plus obstacle keep-out using the SAME inflation as your planner/grid.
        /// </summary>
        public IReadOnlyList<(Vector3 digPos, float approachYaw)> PlanFirstDigTargets(
            IReadOnlyList<VehicleAgent> vehicles,
            TerrainDisk terrain,
            Vector3 center,
            DigScoring cfg,
            float keepoutR,
            bool randomizeOrder,
            List<Obstacle3D>? obstacles,
            float inflation)
        {
            obstacles ??= new List<Obstacle3D>(0);

            int n = Math.Max(1, vehicles.Count);
            var result = new (Vector3, float)[n];
            var reserved = new List<Vector3>(n);

            // Decide an assignment order (optional shuffle)
            var order = new List<int>(n);
            for (int i = 0; i < n; i++) order.Add(i);

            if (randomizeOrder)
            {
                var rng = new RandomNumberGenerator();
                rng.Randomize();
                for (int i = n - 1; i > 0; --i)
                {
                    int j = (int)rng.RandiRange(0, i);
                    (order[i], order[j]) = (order[j], order[i]);
                }
            }

            float SectorTheta0(int k) => k * Mathf.Tau / n;
            float SectorTheta1(int k) => (k + 1) * Mathf.Tau / n;

            foreach (int k in order)
            {
                float theta0 = SectorTheta0(k);
                float theta1 = SectorTheta1(k);

                var car = vehicles[k];
                var carPos = car.currentVehicle.GlobalTransform.Origin;
                var carFwd = (-car.currentVehicle.GlobalTransform.Basis.Z).WithY(0).Normalized();

                float bestScore = float.NegativeInfinity;
                Vector3 bestP = carPos;
                float bestYaw = 0f;
                bool foundAny = false;

                // Calculate wall buffer zone
                float arenaRadius = terrain.Radius;
                const float WallBufferMeters = 0.1f; // 0.1m wall buffer
                float maxAllowedRadius = arenaRadius - WallBufferMeters;
                
                const float ObstacleBufferMeters = 0.5f; // 0.5m obstacle buffer

                for (int a = 0; a < cfg.ArcSteps; a++)
                {
                    float t = (a + 0.5f) / cfg.ArcSteps;
                    float theta = Mathf.Lerp(theta0, theta1, t);
                    Vector3 dir = new(Mathf.Cos(theta), 0, Mathf.Sin(theta));

                    for (int r = 0; r < cfg.RadialSteps; r++)
                    {
                        float u = (r + 0.5f) / cfg.RadialSteps;
                        float R = Mathf.Lerp(cfg.InnerR, cfg.OuterR, u);
                        
                        // Check if too far from center (beyond safe wall buffer zone)
                        if (R > maxAllowedRadius) continue;
                        
                        Vector3 xz = center + dir * R;

                        // Terrain sample
                        if (!terrain.SampleHeightNormal(xz, out var hit, out var _))
                            continue;

                        // Vehicle-to-vehicle keepout (unchanged)
                        bool tooClose = false;
                        for (int m = 0; m < reserved.Count; m++)
                        {
                            if ((hit - reserved[m]).Length() < keepoutR) { tooClose = true; break; }
                        }
                        if (tooClose) continue;

                        // Manual obstacle check - skip if inside obstacle buffer zone
                        bool tooCloseToObstacle = false;
                        if (obstacles != null)
                        {
                            foreach (var obstacle in obstacles)
                            {
                                if (obstacle is CylinderObstacle cyl)
                                {
                                    Vector2 candidateXZ = new Vector2(xz.X, xz.Z);
                                    Vector2 obstacleXZ = new Vector2(cyl.GlobalPosition.X, cyl.GlobalPosition.Z);
                                    float distToObstacle = candidateXZ.DistanceTo(obstacleXZ);
                                    
                                    // Check if inside obstacle + buffer
                                    if (distToObstacle < (cyl.Radius + ObstacleBufferMeters))
                                    {
                                        tooCloseToObstacle = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (tooCloseToObstacle) continue;

                        // Score (unchanged: height only)
                        float score = cfg.WHeight * hit.Y;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestP = new Vector3(xz.X, hit.Y, xz.Z);
                            // approach along the ray from car to spot (unchanged)
                            Vector3 approach = (xz - carPos).WithY(0).Normalized();
                            bestYaw = Mathf.Atan2(approach.Z, approach.X);
                            foundAny = true;
                        }
                    }
                }

                // If a sector had zero valid candidates (rare), fall back to car position to keep behavior predictable.
                if (!foundAny)
                {
                    bestP = carPos;
                    bestYaw = Mathf.Atan2(carFwd.Z, carFwd.X);
                }

                result[k] = (bestP, bestYaw);
                reserved.Add(bestP); // reserve for next picks (unchanged)
            }

            return result;
        }
    }
}