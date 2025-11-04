using Godot;
using System;
using System.Collections.Generic;

namespace SimCore.Core
{
    /// <summary>
    /// Simplified dig brain with sector-based logic.
    /// State machine: Idle → FindHighest → PlanPath → Travel → Dig → Dump → Repeat
    /// </summary>
    public enum DigState
    {
        Idle,
        FindingHighest,
        PlanningPath,
        TravelingToDig,
        Digging,
        PlanningDumpPath,
        TravelingToDump,
        Dumping
    }

    public class SimplifiedDigBrain
    {
        // Robot identity
        public int RobotId { get; private set; }
        public string RobotName { get; private set; }
        
        // Sector assignment
        public float ThetaMin { get; private set; }
        public float ThetaMax { get; private set; }
        public float MaxRadius { get; private set; }
        
        // Home position (center)
        public Vector3 HomePosition { get; private set; }
        
        // State tracking
        public DigState CurrentState { get; private set; } = DigState.Idle;
        public Vector3 CurrentTarget { get; private set; } = Vector3.Zero;
        public List<Vector3> CurrentPath { get; private set; } = new();
        
        // Payload tracking
        public float Payload { get; private set; } = 0f;
        public float MaxPayload => SimulationConfig.RobotLoadCapacity;
        public float PayloadPercent => (Payload / MaxPayload) * 100f;
        
        // Statistics
        public int DigsCompleted { get; private set; } = 0;
        public float TotalDirtMoved { get; private set; } = 0f;
        
        // Dig site parameters
        private const float DIG_RADIUS = 1.0f;
        private const float DIG_DEPTH = 0.03f;
        private const float DIG_AMOUNT = 0.01f; // m³ per dig tick

        public SimplifiedDigBrain(
            int robotId,
            float thetaMin,
            float thetaMax,
            float maxRadius,
            Vector3 homePosition)
        {
            RobotId = robotId;
            RobotName = $"Robot-{robotId}";
            ThetaMin = thetaMin;
            ThetaMax = thetaMax;
            MaxRadius = maxRadius;
            HomePosition = homePosition;
        }

        /// <summary>
        /// Find the highest point in this robot's sector
        /// </summary>
        public Vector3 FindHighestInSector(TerrainDisk terrain, int samples = 20)
        {
            Vector3 highest = Vector3.Zero;
            float highestY = float.MinValue;

            // Shrink the sector slightly to avoid boundary lines
            // This prevents robots from getting stuck on the sector boundary geometry
            float boundaryBuffer = 0.15f; // radians (~8.6 degrees)
            float thetaMinInner = ThetaMin + boundaryBuffer;
            float thetaMaxInner = ThetaMax - boundaryBuffer;
            
            // Make sure we don't invert the range
            if (thetaMinInner >= thetaMaxInner)
            {
                thetaMinInner = (ThetaMin + ThetaMax) / 2f - boundaryBuffer * 0.5f;
                thetaMaxInner = (ThetaMin + ThetaMax) / 2f + boundaryBuffer * 0.5f;
            }

            // Sample points across the sector (avoiding exact boundaries)
            for (int a = 0; a < samples; a++)
            {
                float t = samples > 1 ? (float)a / (samples - 1) : 0.5f;
                float theta = Mathf.Lerp(thetaMinInner, thetaMaxInner, t);
                
                // Sample at different radii
                for (int r = 1; r <= 5; r++)
                {
                    float rad = MaxRadius * r / 5f;
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

            return highest != Vector3.Zero ? highest : new Vector3(Mathf.Cos(thetaMinInner + (thetaMaxInner - thetaMinInner) / 2) * MaxRadius * 0.5f, 0, Mathf.Sin(thetaMinInner + (thetaMaxInner - thetaMinInner) / 2) * MaxRadius * 0.5f);
        }

        /// <summary>
        /// Update state machine
        /// </summary>
        public void Update(double delta, Vector3 currentPosition, bool atTarget, bool pathComplete, TerrainDisk terrain)
        {
            switch (CurrentState)
            {
                case DigState.Idle:
                    // Start searching for next dig site
                    CurrentState = DigState.FindingHighest;
                    break;

                case DigState.FindingHighest:
                    // Find highest point in sector
                    CurrentTarget = FindHighestInSector(terrain);
                    CurrentState = DigState.PlanningPath;
                    break;

                case DigState.PlanningPath:
                    // Path planned externally, just wait for path to be set
                    if (CurrentPath.Count > 0)
                    {
                        CurrentState = DigState.TravelingToDig;
                    }
                    break;

                case DigState.TravelingToDig:
                    // Check if at target
                    if (atTarget || pathComplete)
                    {
                        CurrentState = DigState.Digging;
                    }
                    break;

                case DigState.Digging:
                    // Check if full or done digging
                    if (Payload >= MaxPayload)
                    {
                        CurrentState = DigState.PlanningDumpPath;
                        CurrentTarget = HomePosition;
                        CurrentPath.Clear();
                    }
                    break;

                case DigState.PlanningDumpPath:
                    // Path planned externally
                    if (CurrentPath.Count > 0)
                    {
                        CurrentState = DigState.TravelingToDump;
                    }
                    break;

                case DigState.TravelingToDump:
                    // Check if at home
                    if (atTarget || pathComplete)
                    {
                        CurrentState = DigState.Dumping;
                    }
                    break;

                case DigState.Dumping:
                    // Empty payload
                    TotalDirtMoved += Payload;
                    Payload = 0f;
                    DigsCompleted++;
                    CurrentState = DigState.Idle;
                    CurrentPath.Clear();
                    break;
            }
        }

        /// <summary>
        /// Add dirt to payload (during digging)
        /// </summary>
        public void AddPayload(float amount)
        {
            Payload = Mathf.Min(Payload + amount, MaxPayload);
        }

        /// <summary>
        /// Set planned path
        /// </summary>
        public void SetPath(List<Vector3> path)
        {
            CurrentPath = new List<Vector3>(path);
        }

        /// <summary>
        /// Get human-readable status
        /// </summary>
        public string GetStatus()
        {
            return CurrentState switch
            {
                DigState.Idle => "Idle",
                DigState.FindingHighest => "Scanning...",
                DigState.PlanningPath => "Planning path...",
                DigState.TravelingToDig => "Moving to dig site",
                DigState.Digging => "Digging",
                DigState.PlanningDumpPath => "Planning return...",
                DigState.TravelingToDump => "Returning home",
                DigState.Dumping => "Dumping",
                _ => "Unknown"
            };
        }
    }
}
