using System;
using System.Collections.Generic;
using Godot;
using DigSim3D.Domain;
using DigSim3D.App;
using DigSim3D.Debugging;

namespace DigSim3D.Services
{
    public sealed class RadialScheduler
    {
        public IReadOnlyList<(Vector3 digPos, float approachYaw)> PlanFirstDigTargets(
            IReadOnlyList<VehicleBrain> vehicles,
            TerrainDisk terrain,
            Vector3 center,
            DigScoring cfg,
            float keepoutR = 2.0f,
            bool randomizeOrder = false)
        {
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

            // Helper: sector bounds for car k in the ORIGINAL ring layout
            float SectorTheta0(int k) => k * Mathf.Tau / n;
            float SectorTheta1(int k) => (k + 1) * Mathf.Tau / n;

            foreach (int k in order)
            {
                float theta0 = SectorTheta0(k);
                float theta1 = SectorTheta1(k);

                var car = vehicles[k];
                var carPos = car.Agent.GlobalTransform.Origin;
                var carFwd = (-car.Agent.GlobalTransform.Basis.Z).WithY(0).Normalized();

                float bestScore = float.NegativeInfinity;
                Vector3 bestP = carPos;
                float bestYaw = 0f;

                for (int a = 0; a < cfg.ArcSteps; a++)
                {
                    float t = (a + 0.5f) / cfg.ArcSteps;
                    float theta = Mathf.Lerp(theta0, theta1, t);

                    Vector3 dir = new(Mathf.Cos(theta), 0, Mathf.Sin(theta));
                    for (int r = 0; r < cfg.RadialSteps; r++)
                    {
                        float u = (r + 0.5f) / cfg.RadialSteps;
                        float R = Mathf.Lerp(cfg.InnerR, cfg.OuterR, u);
                        Vector3 xz = center + dir * R;

                        // Terrain sample
                        if (!terrain.SampleHeightNormal(xz, out var hit, out var nrm)) continue;

                        // Keep-out exclusion
                        bool tooClose = false;
                        for (int m = 0; m < reserved.Count; m++)
                        {
                            if ((hit - reserved[m]).Length() < keepoutR) { tooClose = true; break; }
                        }
                        if (tooClose) continue;

                        // Score (height only for now but easy to re-add terms later)
                        float score = cfg.WHeight * hit.Y;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestP = new Vector3(xz.X, hit.Y, xz.Z);
                            // approach along the ray from car to spot
                            Vector3 approach = (xz - carPos).WithY(0).Normalized();
                            bestYaw = Mathf.Atan2(approach.Z, approach.X);
                        }
                    }
                }

                string pathIdSim = DebugPath.Begin("digsim", k, /*sliceId*/ 0);
                DebugPath.Check(pathIdSim, "goal_chosen",
                    ("vehPos", vehicles[k].Agent.GlobalTransform.Origin),
                    ("vehYaw", vehicles[k].Agent.Rotation.Y),
                    ("goalPos", bestP),
                    ("goalYaw", bestYaw),
                    ("radius", bestP.DistanceTo(center)),
                    ("sliceAngle", Mathf.Atan2(bestP.Z - center.Z, bestP.X - center.X)));
                vehicles[k].SetMeta("DebugPathId", pathIdSim);

                result[k] = (bestP, bestYaw);
                reserved.Add(bestP);  // reserve for the next picks
            }

            return result;
        }
    }
}