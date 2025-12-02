using Godot;
using System;
using DigSim3D.App;
using DigSim3D.Services;

namespace DigSim3D.Domain
{
    [Tool]
    public partial class CylinderObstacle : Obstacle3D
    {
        // Backing fields
        private float _radius = 1.0f;
        private float _height = 2.0f;
        private Color _debugColor = new Color(1, 0, 0, 0.3f);

        // Exported properties with setters that update preview in-editor
        [Export]
        public float Radius
        {
            get => _radius;
            set
            {
                _radius = value;
                if (Engine.IsEditorHint()) CreateOrUpdateMesh();
            }
        }

        [Export]
        public float Height
        {
            get => _height;
            set
            {
                _height = value;
                if (Engine.IsEditorHint()) CreateOrUpdateMesh();
            }
        }

        [Export]
        public new Color DebugColor
        {
            get => _debugColor;
            set
            {
                _debugColor = value;
                if (Engine.IsEditorHint()) CreateOrUpdateMesh();
            }
        }

        private MeshInstance3D _meshInstance = null!;
        private CollisionShape3D _collisionShape = null!;

        private Vector3 TopCenter => GlobalPosition + new Vector3(0, Height / 2f, 0);
        private Vector3 BottomCenter => GlobalPosition - new Vector3(0, Height / 2f, 0);

        public override void _Ready()
        {
            // Let _Process run in editor so live preview works
            if (Engine.IsEditorHint())
                SetProcess(true);

            // Create the mesh so it'll show in both editor and game
            CreateOrUpdateMesh();
            AddToGroup("Obstacles");

        }

        public override void _Process(double delta)
        {
            // Only keep updating while in the editor
            if (!Engine.IsEditorHint())
                return;

            // Keep preview in sync (cheap enough here)
            CreateOrUpdateMesh();
        }

        private void CreateOrUpdateMesh()
        {
            // If not found, create one and add it as a child
            if (_meshInstance == null)
            {
                _meshInstance = GetNodeOrNull<MeshInstance3D>("EditorMesh");       

                if (_meshInstance == null)
                {
                    GD.PushError("[CylinderObstacle] MeshInstance3D node not found as child 'EditorMesh'. Create one.");
                    return;
                }
            } 
            // Build mesh & material
            var mesh = new CylinderMesh
            {
                TopRadius = Radius,
                BottomRadius = Radius,
                Height = Height,
                RadialSegments = 32
            };

            _meshInstance.Mesh = mesh;

            var mat = new StandardMaterial3D
            {
                AlbedoColor = DebugColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };

            _meshInstance.MaterialOverride = mat;
            _meshInstance.Visible = true;

            if (_collisionShape == null)
            {
                _collisionShape = GetNodeOrNull<CollisionShape3D>("CylinderBody/CylinderCollisionShape");
                if (_collisionShape == null)
                {
                    GD.PushError("[CylinderObstacle] CollisionShape3D node not found as child 'CylinderBody/CylinderCollisionShape'.");
                    return;
                }
            }

            _collisionShape.Shape = new CylinderShape3D
            {
                Radius = Radius,
                Height = Height
            };
        }

        // --- Collision methods unchanged (kept for completeness) ---
        public override bool ContainsPoint(Vector3 point)
        {
            Vector3 flatPoint = new Vector3(point.X, GlobalPosition.Y, point.Z);
            float horizontalDist = flatPoint.DistanceTo(GlobalPosition);
            bool withinRadius = horizontalDist <= Radius;
            bool withinHeight = (point.Y >= BottomCenter.Y && point.Y <= TopCenter.Y);
            return withinRadius && withinHeight;
        }

        public override bool IntersectsSegment(Vector3 start, Vector3 end)
        {
            // quick AABB reject
            if (!SegmentIntersectsAabb(start, end))
                return false;

            Vector2 p1 = new Vector2(start.X, start.Z);
            Vector2 p2 = new Vector2(end.X, end.Z);
            Vector2 center = new Vector2(GlobalPosition.X, GlobalPosition.Z);

            Vector2 d = p2 - p1;
            Vector2 f = p1 - center;

            float a = d.Dot(d);
            float b = 2 * f.Dot(d);
            float c = f.Dot(f) - Radius * Radius;

            float discriminant = b * b - 4 * a * c;
            if (discriminant < 0)
                return false;

            discriminant = Mathf.Sqrt(discriminant);
            float t1 = (-b - discriminant) / (2 * a);
            float t2 = (-b + discriminant) / (2 * a);

            bool hit = false;

            if (t1 >= 0 && t1 <= 1)
            {
                float y1 = Mathf.Lerp(start.Y, end.Y, t1);
                if (y1 >= BottomCenter.Y && y1 <= TopCenter.Y)
                    hit = true;
            }

            if (t2 >= 0 && t2 <= 1)
            {
                float y2 = Mathf.Lerp(start.Y, end.Y, t2);
                if (y2 >= BottomCenter.Y && y2 <= TopCenter.Y)
                    hit = true;
            }

            return hit;
        }

        private bool SegmentIntersectsAabb(Vector3 start, Vector3 end)
        {
            Aabb aabb = new Aabb(BottomCenter - new Vector3(Radius, 0, Radius),
                                    new Vector3(2 * Radius, Height, 2 * Radius));

            Vector3 dir = end - start;
            Vector3 invDir = new Vector3(
                dir.X != 0 ? 1f / dir.X : float.PositiveInfinity,
                dir.Y != 0 ? 1f / dir.Y : float.PositiveInfinity,
                dir.Z != 0 ? 1f / dir.Z : float.PositiveInfinity
            );

            float tMin = 0f;
            float tMax = 1f;

            for (int i = 0; i < 3; i++)
            {
                float aabbMin = i == 0 ? aabb.Position.X : (i == 1 ? aabb.Position.Y : aabb.Position.Z);
                float aabbMax = aabbMin + (i == 0 ? aabb.Size.X : (i == 1 ? aabb.Size.Y : aabb.Size.Z));
                float s = i == 0 ? start.X : (i == 1 ? start.Y : start.Z);
                float dInv = i == 0 ? invDir.X : (i == 1 ? invDir.Y : invDir.Z);

                float t1 = (aabbMin - s) * dInv;
                float t2 = (aabbMax - s) * dInv;

                if (t1 > t2) (t1, t2) = (t2, t1);

                tMin = Math.Max(tMin, t1);
                tMax = Math.Min(tMax, t2);

                if (tMin > tMax)
                    return false;
            }

            return true;
        }


        // --- Added for planners ---
        public Vector3 Center => GlobalPosition;

    }
}