using Godot;

namespace DigSim3D.Domain
{
    /// <summary>
    /// Tracks the dig state of a single robot.
    /// Includes current payload, target position, and task state.
    /// </summary>
    public sealed class DigState
    {
        public enum TaskState
        {
            Idle,                   // Waiting for task
            TravelingToDigSite,     // Moving to dig target
            Digging,                // At dig site, accumulating load
            TravelingToDump,        // Moving back to origin to dump
            Dumping,                // At origin, unloading payload
            Complete                // Finished - no more valid dig targets
        }

        /// <summary> Current amount of dirt in payload (cubic meters) </summary>
        public float CurrentPayload { get; set; } = 0f;

        /// <summary> Maximum payload capacity (cubic meters) </summary>
        public float MaxPayload { get; set; } = 5.0f;

        /// <summary> Current dig target position in world space </summary>
        public Vector3 CurrentDigTarget { get; set; } = Vector3.Zero;

    /// <summary> Approach yaw for the dig target </summary>
    public float CurrentDigYaw { get; set; } = 0f;

    /// <summary> Initial terrain height when we started digging at current site (meters) </summary>
    public float InitialDigHeight { get; set; } = 0f;

    /// <summary> Volume excavated at the current dig site (in-situ m³, before swell) </summary>
    public float CurrentSiteVolumeExcavated { get; set; } = 0f;

    /// <summary> Is the current dig site fully excavated (volume target reached)? </summary>
    public bool CurrentSiteComplete { get; set; } = false;

    /// <summary> Current state </summary>
    public TaskState State { get; set; } = TaskState.Idle;
        /// <summary> Total times robot has dumped at origin </summary>
        public int DumpCount { get; set; } = 0;

        public bool IsPayloadFull => CurrentPayload >= MaxPayload * 0.95f;
    }
}
