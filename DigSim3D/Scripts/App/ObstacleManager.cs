// res://Scripts/App/ObstacleManager.cs
using Godot;
using System.Collections.Generic;
using DigSim3D.Domain;
using DigSim3D.Services;

namespace DigSim3D.App
{
    /// <summary>
    /// Thin façade that converts scene nodes (e.g., CylinderObstacle nodes under a container)
    /// into pure data obstacles (DigSim3D.Domain.Obstacle3D) using ObstacleAdapter.
    /// </summary>
    public partial class ObstacleManager : Node3D
    {
        // Set this in the Inspector to your "Obstacles" parent node
        [Export] public NodePath ScanRootPath { get; set; } = new NodePath();

        private Node? _scanRoot;

        public override void _Ready()
        {
            _scanRoot = (!ScanRootPath.IsEmpty) ? GetNodeOrNull<Node>(ScanRootPath) : this;

            if (_scanRoot == null)
            {
                GD.PushWarning("[ObstacleManager] ScanRootPath not set or not found; scanning self (may be empty).");
                _scanRoot = this;
            }
        }

        /// <summary>
        /// Returns scene obstacles as data objects (AABB, Cylinder, etc.).
        /// </summary>
        public List<Obstacle3D> GetObstacles()
        {
            var root = _scanRoot ?? this;
            var data = ObstacleAdapter.ReadFromScene(root);
            GD.Print($"[ObstacleManager] Exported {data.Count} obstacles from scene.");
            return data;
        }
    }
}