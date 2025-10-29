using System;
using System.Collections.Generic;
using Godot;
using DigSim3D.Domain;

namespace DigSim3D.Services
{
    public static class ObstacleClearance
    {
        public static bool IsSiteValid(Vector3 p, List<Obstacle3D> obstacles, float inflation)
        {
            foreach (var o in obstacles)
            {
                if (o.Shape == ObstacleShape.Cylinder)
                {
                    float dx = p.X - o.Center.X;
                    float dz = p.Z - o.Center.Z;
                    float distSq = dx * dx + dz * dz;
                    float min = o.Radius + inflation;
                    if (distSq <= min * min) return false;
                }
                else // AABB projected to XZ with inflation
                {
                    float hx = Math.Max(0f, o.Extents.X) + inflation;
                    float hz = Math.Max(0f, o.Extents.Z) + inflation;
                    if (p.X >= o.Center.X - hx && p.X <= o.Center.X + hx &&
                        p.Z >= o.Center.Z - hz && p.Z <= o.Center.Z + hz)
                        return false;
                }
            }
            return true;
        }

        // Push a point just outside the nearest inflated obstacle
        public static Vector3 SnapOutsideBuffer(Vector3 p, List<Obstacle3D> obstacles, float inflation, float epsilon = 0.05f)
        {
            float bestPenetration = float.NegativeInfinity;
            Obstacle3D? best = null;

            foreach (var o in obstacles)
            {
                if (o.Shape == ObstacleShape.Cylinder)
                {
                    float dx = p.X - o.Center.X;
                    float dz = p.Z - o.Center.Z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    float min = o.Radius + inflation;
                    float pen = min - d; // positive if inside the inflated buffer
                    if (pen > bestPenetration)
                    {
                        bestPenetration = pen;
                        best = o;
                    }
                }
                else
                {
                    float hx = Math.Max(0f, o.Extents.X) + inflation;
                    float hz = Math.Max(0f, o.Extents.Z) + inflation;

                    float dx = 0f;
                    if (p.X < o.Center.X - hx) dx = (o.Center.X - hx) - p.X;
                    else if (p.X > o.Center.X + hx) dx = (o.Center.X + hx) - p.X;

                    float dz = 0f;
                    if (p.Z < o.Center.Z - hz) dz = (o.Center.Z - hz) - p.Z;
                    else if (p.Z > o.Center.Z + hz) dz = (o.Center.Z + hz) - p.Z;

                    // inside inflated AABB if both dx == 0 and dz == 0
                    if (dx == 0f && dz == 0f)
                    {
                        float pen = Mathf.Min(hx, hz); // arbitrary: treat as “strongly inside”
                        if (pen > bestPenetration)
                        {
                            bestPenetration = pen;
                            best = o;
                        }
                    }
                }
            }

            if (best == null || bestPenetration <= 0f)
                return p; // already fine

            if (best.Shape == ObstacleShape.Cylinder)
            {
                Vector2 dir = new Vector2(p.X - best.Center.X, p.Z - best.Center.Z);
                if (dir.LengthSquared() < 1e-6f)
                    dir = new Vector2(1f, 0f); // arbitrary direction if exactly at center

                dir = dir.Normalized();
                float targetR = best.Radius + inflation + epsilon;
                var snapped = new Vector3(best.Center.X + dir.X * targetR, p.Y, best.Center.Z + dir.Y * targetR);
                return snapped;
            }
            else
            {
                float hx = Math.Max(0f, best.Extents.X) + inflation + epsilon;
                float hz = Math.Max(0f, best.Extents.Z) + inflation + epsilon;

                float clampedX = Mathf.Clamp(p.X, best.Center.X - hx, best.Center.X + hx);
                float clampedZ = Mathf.Clamp(p.Z, best.Center.Z - hz, best.Center.Z + hz);

                // push out along the shallowest axis side
                float dxLeft  = Math.Abs((best.Center.X - hx) - p.X);
                float dxRight = Math.Abs((best.Center.X + hx) - p.X);
                float dzNear  = Math.Abs((best.Center.Z - hz) - p.Z);
                float dzFar   = Math.Abs((best.Center.Z + hz) - p.Z);

                float minEdge = MathF.Min(MathF.Min(dxLeft, dxRight), MathF.Min(dzNear, dzFar));
                Vector3 snapped = p;

                if (minEdge == dxLeft)  snapped.X = best.Center.X - hx;
                else if (minEdge == dxRight) snapped.X = best.Center.X + hx;
                else if (minEdge == dzNear)  snapped.Z = best.Center.Z - hz;
                else                         snapped.Z = best.Center.Z + hz;

                snapped.Y = p.Y;
                return snapped;
            }
        }

        // Small bonus (0..1) for how far outside the inflated boundary the point is (within 2m window)
        public static float ClearanceBonus(Vector3 p, List<Obstacle3D> obstacles, float inflation, float window = 2.0f)
        {
            float minOver = float.PositiveInfinity;

            foreach (var o in obstacles)
            {
                if (o.Shape == ObstacleShape.Cylinder)
                {
                    float dx = p.X - o.Center.X;
                    float dz = p.Z - o.Center.Z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    float over = d - (o.Radius + inflation);
                    if (over < minOver) minOver = over;
                }
                else
                {
                    float hx = Math.Max(0f, o.Extents.X) + inflation;
                    float hz = Math.Max(0f, o.Extents.Z) + inflation;

                    float overX = MathF.Min(MathF.Abs(p.X - (o.Center.X - hx)), MathF.Abs(p.X - (o.Center.X + hx)));
                    float overZ = MathF.Min(MathF.Abs(p.Z - (o.Center.Z - hz)), MathF.Abs(p.Z - (o.Center.Z + hz)));
                    float over = MathF.Min(overX, overZ);
                    if (over < minOver) minOver = over;
                }
            }

            if (float.IsInfinity(minOver)) return 1f;
            return Mathf.Clamp(minOver / window, 0f, 1f);
        }
    }
}