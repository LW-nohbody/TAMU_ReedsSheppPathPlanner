using Godot;
using System;

namespace SimCore.Core
{
    /// <summary>
    /// Simple, smart dig logic: Always dig the highest point in your sector.
    /// This naturally flattens terrain without creating pits that trap robots.
    /// </summary>
    public static class SimpleDigLogic
    {
        // How much to lower per dig operation (meters)
        public const float DIG_AMOUNT = 0.03f;
        
        // Robot dirt capacity (cubic meters)
        public const float ROBOT_CAPACITY = 0.5f;

        /// <summary>
        /// Get dig radius based on robot width (dig area = robot footprint)
        /// </summary>
        public static float GetDigRadius(float robotWidth)
        {
            // Dig radius is approximately robot width (covers robot footprint)
            return robotWidth * 0.6f;  // Slightly smaller than full width for safety
        }

        /// <summary>
        /// Find the highest point in a sector (angular slice of the terrain)
        /// </summary>
        public static Vector3 FindHighestInSector(
            TerrainDisk terrain,
            float thetaMin, 
            float thetaMax, 
            float maxRadius,
            int samples = 32)
        {
            Vector3 highest = Vector3.Zero;
            float highestY = float.MinValue;

            // Sample points in the sector
            for (int a = 0; a < samples; a++)
            {
                float t = (float)a / (samples - 1);
                float theta = Mathf.Lerp(thetaMin, thetaMax, t);
                
                // Sample at different radii
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
                        if (hitPos.Y > highestY)
                        {
                            highestY = hitPos.Y;
                            highest = new Vector3(pt.X, 0, pt.Z);
                        }
                    }
                }
            }

            return highest;
        }

        /// <summary>
        /// Perform a dig at the specified location
        /// Returns the amount of dirt extracted
        /// </summary>
        public static float PerformDig(
            TerrainDisk terrain,
            Vector3 digCenter,
            float currentPayload,
            float capacity,
            float digRadius)
        {
            // Calculate volume of material removed (cylinder: pi * r^2 * h)
            float volume = Mathf.Pi * digRadius * digRadius * DIG_AMOUNT;
            
            // Don't overfill the robot
            float availableCapacity = capacity - currentPayload;
            float actualDig = Mathf.Min(volume, availableCapacity);
            
            if (actualDig > 0)
            {
                // Lower the terrain
                float actualDepth = actualDig / (Mathf.Pi * digRadius * digRadius);
                terrain.LowerArea(digCenter, digRadius, actualDepth);
            }
            
            return actualDig;
        }

        /// <summary>
        /// Check if there's more work to do in this sector
        /// (is the highest point still significantly above zero/flat?)
        /// </summary>
        public static bool HasWorkRemaining(
            TerrainDisk terrain,
            float thetaMin,
            float thetaMax,
            float maxRadius,
            float flatThreshold = 0.05f)
        {
            Vector3 highest = FindHighestInSector(terrain, thetaMin, thetaMax, maxRadius);
            
            if (terrain.SampleHeightNormal(highest, out var hitPos, out var _))
            {
                // Still has work if highest point is above threshold
                return hitPos.Y > flatThreshold;
            }
            
            return false;
        }
    }
}
