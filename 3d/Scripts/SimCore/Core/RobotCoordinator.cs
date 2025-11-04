using Godot;
using System.Collections.Generic;
using System.Linq;

namespace SimCore.Core
{
    /// <summary>
    /// Coordinates multiple robots to avoid collisions and ensure full terrain coverage.
    /// Each robot registers its current dig target, and others avoid that area.
    /// </summary>
    public class RobotCoordinator
    {
        private readonly Dictionary<int, DigClaim> _activeClaims = new();
        private readonly float _minSeparation;

        public RobotCoordinator(float minSeparationMeters = 3.0f)
        {
            _minSeparation = minSeparationMeters;
        }

        /// <summary>
        /// Robot claims a dig site. Returns true if claim successful.
        /// </summary>
        public bool ClaimDigSite(int robotId, Vector3 position, float radius)
        {
            // Check if too close to any existing claim
            foreach (var claim in _activeClaims.Values)
            {
                if (claim.RobotId == robotId) continue; // Can update own claim
                
                float distance = position.DistanceTo(claim.Position);
                float minDist = _minSeparation + radius + claim.Radius;
                
                if (distance < minDist)
                {
                    return false; // Too close to another robot's dig site
                }
            }

            // Claim successful
            _activeClaims[robotId] = new DigClaim(robotId, position, radius);
            return true;
        }

        /// <summary>
        /// Release robot's claim (when done digging or moving to new site)
        /// </summary>
        public void ReleaseClaim(int robotId)
        {
            _activeClaims.Remove(robotId);
        }

        /// <summary>
        /// Get best dig point avoiding other robots' claims
        /// </summary>
        public Vector3 GetBestDigPoint(
            int robotId,
            TerrainDisk terrain,
            float thetaMin,
            float thetaMax,
            float maxRadius,
            int samples = 32)
        {
            var candidates = new List<(Vector3 pos, float height)>();

            // Sample points in sector
            for (int a = 0; a < samples; a++)
            {
                float t = (float)a / (samples - 1);
                float theta = Mathf.Lerp(thetaMin, thetaMax, t);

                for (int r = 1; r <= 5; r++)
                {
                    float rad = maxRadius * r / 5f;
                    Vector3 pt = new Vector3(
                        Mathf.Cos(theta) * rad,
                        0,
                        Mathf.Sin(theta) * rad
                    );

                    if (terrain.SampleHeightNormal(pt, out var hitPos, out var _))
                    {
                        // Check if this point is too close to other robots' claims
                        bool tooClose = false;
                        foreach (var claim in _activeClaims.Values)
                        {
                            if (claim.RobotId == robotId) continue;
                            
                            float dist = pt.DistanceTo(claim.Position);
                            // Increased separation distance to avoid collision clusters
                            if (dist < _minSeparation + claim.Radius + 0.5f)
                            {
                                tooClose = true;
                                break;
                            }
                        }

                        if (!tooClose)
                        {
                            candidates.Add((new Vector3(pt.X, 0, pt.Z), hitPos.Y));
                        }
                    }
                }
            }

            // Return highest point that's not claimed
            if (candidates.Count > 0)
            {
                var best = candidates.OrderByDescending(c => c.height).First();
                return best.pos;
            }

            // Fallback: return various positions in sector if no good spot found
            // This gives multiple options instead of always the same fallback
            float fallbackRadius = maxRadius * 0.6f;  // Reduced from 0.8f to be safer
            float midTheta = (thetaMin + thetaMax) / 2f;
            
            // Try offset angles to vary fallback location
            float offsetTheta = midTheta + (Mathf.Sin(Godot.GD.Randf() * Mathf.Tau) * 0.3f);
            
            return new Vector3(
                Mathf.Cos(offsetTheta) * fallbackRadius,
                0,
                Mathf.Sin(offsetTheta) * fallbackRadius
            );
        }

        /// <summary>
        /// Get all active dig claims (for visualization)
        /// </summary>
        public IEnumerable<DigClaim> GetActiveClaims() => _activeClaims.Values;

        public struct DigClaim
        {
            public int RobotId { get; }
            public Vector3 Position { get; }
            public float Radius { get; }

            public DigClaim(int robotId, Vector3 position, float radius)
            {
                RobotId = robotId;
                Position = position;
                Radius = radius;
            }
        }
    }
}
