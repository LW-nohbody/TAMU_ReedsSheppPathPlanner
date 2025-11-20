using Godot;
using System;

namespace DigSim3D.App
{
    [Tool]
    public partial class TerrainDisk : Node3D
    {
        [Export] public float Radius = 15f;
        [Export(PropertyHint.Range, "32,1024,1")] public int Resolution = 256;

        // Terrain look
        [Export] public float Amplitude = 0.5f;
        [Export] public float Frequency = 0.1f;
        [Export] public int Octaves = 2;
        [Export] public float Lacunarity = 2.0f;
        [Export] public float Gain = 0.4f;
        [Export] public int Seed = 1337;
        [Export(PropertyHint.Range, "0,1,0.01")] public float Smooth = 0.6f;
        [Export] public float FloorY = 0.0f;
        [Export] public NodePath FloorNodePath = null!;

        [Export] public Material MaterialOverride = null!;

        // (Optional) put the collider on a specific layer/mask
        [Export] public uint ColliderLayer = 1;
        [Export] public uint ColliderMask = 1;

        // --- children ------------------------------------------------------------
        private MeshInstance3D _meshMI = null!;
        private StaticBody3D _staticBody = null!;
        private CollisionShape3D _colShape = null!;

        // --- cached grid for exact sampling -------------------------------------
        private float[,] _heights = null!;   // size: _N x _N (local-space Y)
        private Vector3[,] _norms = null!;    // per-vertex normals (local space)
        private int _N;
        private float _step;         // world spacing between grid verts

        // Public accessors for external terrain modification
        public float[,] HeightGrid => _heights;
        public int GridResolution => _N;
        public float GridStep => _step;

        // Angle at which terrain settles (usually around 25-35)
        float angleOfReposeDeg = 20.0f;
        // Dropping the minimum angle lower than 15 may cause bugs
        const float MinAngleDeg = 15.0f;


        public override void _Ready()
        {
            EnsureChildren();
            FloorYFromNode();
            Rebuild();
            if (Engine.IsEditorHint()) SetProcess(false);
        }

        // Call this after you change parameters at runtime if needed.
        public void Rebuild()
        {
            // --- noise setup -----------------------------------------------------
            var noise = new FastNoiseLite
            {
                Seed = Seed,
                NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
                Frequency = Frequency
            };
            noise.FractalOctaves = Octaves;
            noise.FractalLacunarity = Lacunarity;
            noise.FractalGain = Gain;

            float NRaw(float x, float z) => noise.GetNoise2D(x, z);

            float Smoothed(float x, float z, float r)
            {
                float c = NRaw(x, z);
                float n1 = NRaw(x + r, z) + NRaw(x - r, z) + NRaw(x, z + r) + NRaw(x, z - r);
                float n2 = NRaw(x + r, z + r) + NRaw(x + r, z - r) + NRaw(x - r, z + r) + NRaw(x - r, z - r);
                float avg = (4f * c + 2f * n1 + 1f * n2) / (4f + 8f + 4f);
                return Mathf.Lerp(c, avg, Mathf.Clamp(Smooth, 0f, 1f));
            }

            // --- grid -------------------------------------------------------------
            int N = Mathf.Max(32, Resolution);
            float size = Radius * 2f;
            float step = size / (N - 1);
            float blurR = step * 2f;

            _N = N;
            _step = step;
            _heights = new float[N, N];

            _norms = new Vector3[N, N];

            bool Inside(float x, float z) => (x * x + z * z) <= (Radius * Radius);

            // heights
            for (int j = 0; j < N; j++)
            {
                float z = -Radius + j * step;
                for (int i = 0; i < N; i++)
                {
                    float x = -Radius + i * step;
                    if (Inside(x, z))
                    {
                        float n = Smoothed(x, z, blurR);
                        float h = Amplitude * n;
                        if (h < FloorY)
                            h = FloorY + (FloorY - h);
                        if (h < 0.011f)
                        {
                            h = 0.011f;
                        }
                        _heights[i, j] = h;
                    }
                    else
                    {
                        _heights[i, j] = float.NaN; // outside disk
                    }
                }
            }

            // normals (central diff)
            for (int j = 0; j < N; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    if (float.IsNaN(_heights[i, j])) { _norms[i, j] = Vector3.Up; continue; }

                    int il = Math.Max(0, i - 1), ir = Math.Min(N - 1, i + 1);
                    int jd = Math.Max(0, j - 1), ju = Math.Min(N - 1, j + 1);

                    float hL = _heights[il, j]; if (float.IsNaN(hL)) hL = _heights[i, j];
                    float hR = _heights[ir, j]; if (float.IsNaN(hR)) hR = _heights[i, j];
                    float hD = _heights[i, jd]; if (float.IsNaN(hD)) hD = _heights[i, j];
                    float hU = _heights[i, ju]; if (float.IsNaN(hU)) hU = _heights[i, j];

                    Vector3 dx = new Vector3(2f * step, hR - hL, 0f);
                    Vector3 dz = new Vector3(0f, hU - hD, 2f * step);

                    _norms[i, j] = dz.Cross(dx).Normalized(); // y-up
                }
            }

            // --- mesh build -------------------------------------------------------
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            for (int j = 0; j < N - 1; j++)
            {
                float z0 = -Radius + j * step;
                float z1 = z0 + step;

                for (int i = 0; i < N - 1; i++)
                {
                    float x0 = -Radius + i * step;
                    float x1 = x0 + step;

                    if (float.IsNaN(_heights[i, j]) || float.IsNaN(_heights[i + 1, j]) ||
                        float.IsNaN(_heights[i, j + 1]) || float.IsNaN(_heights[i + 1, j + 1]))
                        continue;

                    float y00 = _heights[i, j];
                    float y10 = _heights[i + 1, j];
                    float y01 = _heights[i, j + 1];
                    float y11 = _heights[i + 1, j + 1];

                    const float visualFloorEps = 0.0001f;
                    const float visualNudge = 0.005f;

                    if (y00 <= FloorY + visualFloorEps) y00 = FloorY - visualNudge;
                    if (y10 <= FloorY + visualFloorEps) y10 = FloorY - visualNudge;
                    if (y01 <= FloorY + visualFloorEps) y01 = FloorY - visualNudge;
                    if (y11 <= FloorY + visualFloorEps) y11 = FloorY - visualNudge;

                    Vector3 v00 = new Vector3(x0, y00, z0);
                    Vector3 v10 = new Vector3(x1, y10, z0);
                    Vector3 v01 = new Vector3(x0, y01, z1);
                    Vector3 v11 = new Vector3(x1, y11, z1);

                    Vector2 uv00 = new((float)i / (N - 1), (float)j / (N - 1));
                    Vector2 uv10 = new((float)(i + 1) / (N - 1), (float)j / (N - 1));
                    Vector2 uv01 = new((float)i / (N - 1), (float)(j + 1) / (N - 1));
                    Vector2 uv11 = new((float)(i + 1) / (N - 1), (float)(j + 1) / (N - 1));

                    bool flip = ((i + j) & 1) == 1; // checkerboard

                    if (!flip)
                    {
                        st.SetUV(uv00); st.SetNormal(_norms[i, j]); st.AddVertex(v00);
                        st.SetUV(uv10); st.SetNormal(_norms[i + 1, j]); st.AddVertex(v10);
                        st.SetUV(uv01); st.SetNormal(_norms[i, j + 1]); st.AddVertex(v01);

                        st.SetUV(uv10); st.SetNormal(_norms[i + 1, j]); st.AddVertex(v10);
                        st.SetUV(uv11); st.SetNormal(_norms[i + 1, j + 1]); st.AddVertex(v11);
                        st.SetUV(uv01); st.SetNormal(_norms[i, j + 1]); st.AddVertex(v01);
                    }
                    else
                    {
                        st.SetUV(uv00); st.SetNormal(_norms[i, j]); st.AddVertex(v00);
                        st.SetUV(uv11); st.SetNormal(_norms[i + 1, j + 1]); st.AddVertex(v11);
                        st.SetUV(uv01); st.SetNormal(_norms[i, j + 1]); st.AddVertex(v01);

                        st.SetUV(uv00); st.SetNormal(_norms[i, j]); st.AddVertex(v00);
                        st.SetUV(uv10); st.SetNormal(_norms[i + 1, j]); st.AddVertex(v10);
                        st.SetUV(uv11); st.SetNormal(_norms[i + 1, j + 1]); st.AddVertex(v11);
                    }
                }
            }

            st.Index();
            st.GenerateTangents();

            var mesh = st.Commit();
            _meshMI.Mesh = mesh;

            if (MaterialOverride != null)
                _meshMI.SetSurfaceOverrideMaterial(0, MaterialOverride);
            else
            {
                var mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.36f, 0.31f, 0.27f),
                    Roughness = 1.0f,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel
                };
                _meshMI.SetSurfaceOverrideMaterial(0, mat);
            }

            // --- physics collider (trimesh) --------------------------------------
            var faces = mesh.GetFaces(); // PoolVector3Array of all triangle verts
            var concave = new ConcavePolygonShape3D { Data = faces };
            _colShape.Shape = concave;

            _staticBody.CollisionLayer = ColliderLayer;
            _staticBody.CollisionMask = ColliderMask;
            _colShape.Disabled = false;

            int under = 0; float minH = 1e9f, maxH = -1e9f;
            for (int j = 0; j < _N; j++)
                for (int i = 0; i < _N; i++)
                {
                    float h = _heights[i, j];
                    if (h < FloorY - 1e-6f) under++;
                    if (h < minH) minH = h; if (h > maxH) maxH = h;
                }
            GD.Print($"[TerrainDisk] min={minH:F6} max={maxH:F6} floor={FloorY:F6} underFloor={under}");
        }

        // -------------------------------------------------------------------------
        // Public sampler: exact height & normal at a world XZ
        // Returns false if outside the disk.
        // -------------------------------------------------------------------------
        public bool SampleHeightNormal(Vector3 worldXZ, out Vector3 hitPos, out Vector3 normal)
        {
            // Convert to local coords
            Vector3 local = ToLocal(worldXZ);
            float x = local.X, z = local.Z;

            // outside disk?
            if ((x * x + z * z) > (Radius * Radius))
            {
                hitPos = default;
                normal = Vector3.Up;
                return false;
            }

            // Grid coords
            float fx = (x + Radius) / _step;
            float fz = (z + Radius) / _step;

            int i = Mathf.Clamp(Mathf.FloorToInt(fx), 0, _N - 2);
            int j = Mathf.Clamp(Mathf.FloorToInt(fz), 0, _N - 2);

            float tx = fx - i;
            float tz = fz - j;

            // Bilinear height
            float h00 = _heights[i, j];
            float h10 = _heights[i + 1, j];
            float h01 = _heights[i, j + 1];
            float h11 = _heights[i + 1, j + 1];

            float h0 = Mathf.Lerp(h00, h10, tx);
            float h1 = Mathf.Lerp(h01, h11, tx);
            float h = Mathf.Lerp(h0, h1, tz);

            // Bilinear normal (re-normalize)
            Vector3 n00 = _norms[i, j];
            Vector3 n10 = _norms[i + 1, j];
            Vector3 n01 = _norms[i, j + 1];
            Vector3 n11 = _norms[i + 1, j + 1];

            Vector3 n0 = n00 * (1 - tx) + n10 * tx;
            Vector3 n1 = n01 * (1 - tx) + n11 * tx;
            Vector3 n = (n0 * (1 - tz) + n1 * tz).Normalized();

            // Back to world space
            Vector3 localHit = new Vector3(x, h, z);
            hitPos = ToGlobal(localHit);
            normal = (GlobalTransform.Basis * n).Normalized();

            // Check if concrete floor
            if (hitPos.Y < FloorY)
            {
                hitPos.Y = FloorY;
                normal = Vector3.Up;   // flat slab
            }

            return true;
        }

        // -------------------------------------------------------------------------
        // Terrain deformation: reduce height at grid points (for excavation)
        // -------------------------------------------------------------------------
        /// <summary>
        /// Reduce terrain height at specific grid indices by the given amount.
        /// Call Rebuild() after modifications to update the mesh.
        /// Clamps to not go below FloorY.
        /// </summary>
        public void ReduceHeightAt(int gridI, int gridJ, float amount)
        {
            if (_heights == null || gridI < 0 || gridI >= _N || gridJ < 0 || gridJ >= _N)
                return;

            if (!float.IsNaN(_heights[gridI, gridJ]))
            {
                float newHeight = _heights[gridI, gridJ] - amount;
                _heights[gridI, gridJ] = Mathf.Max(newHeight, FloorY);
            }
        }

        /// <summary>
        /// Reduce terrain height in a circular region around a world position.
        /// </summary>
        public void ReduceHeightCircle(Vector3 worldCenter, float radiusMeters, float depthMeters)
        {
            if (_heights == null) return;

            Vector3 local = ToLocal(worldCenter);
            float cx = local.X;
            float cz = local.Z;

            float fx = (cx + Radius) / _step;
            float fz = (cz + Radius) / _step;

            int ci = Mathf.FloorToInt(fx);
            int cj = Mathf.FloorToInt(fz);

            int radiusInCells = Mathf.CeilToInt(radiusMeters / _step) + 1;

            for (int i = Mathf.Max(0, ci - radiusInCells); i <= Mathf.Min(_N - 1, ci + radiusInCells); i++)
            {
                for (int j = Mathf.Max(0, cj - radiusInCells); j <= Mathf.Min(_N - 1, cj + radiusInCells); j++)
                {
                    if (float.IsNaN(_heights[i, j])) continue;

                    float xi = -Radius + i * _step;
                    float zj = -Radius + j * _step;
                    float dist = Mathf.Sqrt((xi - cx) * (xi - cx) + (zj - cz) * (zj - cz));

                    if (dist <= radiusMeters)
                    {
                        // Falloff: stronger at center, weaker at edges
                        float falloff = 1f - (dist / radiusMeters);
                        falloff = falloff * falloff;  // quadratic

                        ReduceHeightAt(i, j, depthMeters * falloff);
                    }
                }
            }
        }

        /// <summary>
        /// Lower terrain height in a circular area (for digging/excavation).
        /// Directly modifies _heights and rebuilds only the mesh (preserves modifications).
        /// This is efficient for real-time terrain deformation.
        /// Clamps terrain to not go below FloorY.
        /// </summary>
        public void LowerArea(Vector3 worldXZ, float radius, float deltaHeight)
        {
            if (_heights == null) return;

            // Convert to local coordinates used by _heights
            Vector3 local = ToLocal(worldXZ);
            float cx = local.X, cz = local.Z;

            // Iterate grid and subtract deltaHeight where within radius
            for (int j = 0; j < _N; j++)
            {
                float z = -Radius + j * _step;
                for (int i = 0; i < _N; i++)
                {
                    float x = -Radius + i * _step;
                    if (float.IsNaN(_heights[i, j])) continue;

                    float dx = x - cx;
                    float dz = z - cz;

                    if (dx * dx + dz * dz <= radius * radius)
                    {
                        // Lower terrain but clamp to FloorY to prevent digging through the floor
                        float newHeight = _heights[i, j] - deltaHeight;
                        _heights[i, j] = Mathf.Max(newHeight, FloorY);
                    }
                }
            }

            // Rebuild mesh ONLY (preserves modified heights)
            RebuildMeshOnly();
        }

        /// <summary>
        /// Rebuild mesh after height modifications.
        /// Recomputes normals and updates geometry without regenerating the entire terrain.
        /// Much faster than Rebuild() for real-time updates.
        /// </summary>
        public void RebuildMeshOnly()
        {
            if (_heights == null || _norms == null) return;

            // Recompute normals based on current heights
            for (int j = 0; j < _N; j++)
            {
                for (int i = 0; i < _N; i++)
                {
                    if (float.IsNaN(_heights[i, j])) { _norms[i, j] = Vector3.Up; continue; }

                    int il = Math.Max(0, i - 1), ir = Math.Min(_N - 1, i + 1);
                    int jd = Math.Max(0, j - 1), ju = Math.Min(_N - 1, j + 1);

                    float hL = _heights[il, j]; if (float.IsNaN(hL)) hL = _heights[i, j];
                    float hR = _heights[ir, j]; if (float.IsNaN(hR)) hR = _heights[i, j];
                    float hD = _heights[i, jd]; if (float.IsNaN(hD)) hD = _heights[i, j];
                    float hU = _heights[i, ju]; if (float.IsNaN(hU)) hU = _heights[i, j];

                    Vector3 dx = new Vector3(2f * _step, hR - hL, 0f);
                    Vector3 dz = new Vector3(0f, hU - hD, 2f * _step);

                    _norms[i, j] = dz.Cross(dx).Normalized();
                }
            }

            // Rebuild mesh with updated heights and normals
            var st = new SurfaceTool();
            st.Begin(Mesh.PrimitiveType.Triangles);

            for (int j = 0; j < _N - 1; j++)
            {
                float z0 = -Radius + j * _step;
                float z1 = z0 + _step;

                for (int i = 0; i < _N - 1; i++)
                {
                    float x0 = -Radius + i * _step;
                    float x1 = x0 + _step;

                    if (float.IsNaN(_heights[i, j]) || float.IsNaN(_heights[i + 1, j]) ||
                        float.IsNaN(_heights[i, j + 1]) || float.IsNaN(_heights[i + 1, j + 1]))
                        continue;

                    float y00 = _heights[i, j];
                    float y10 = _heights[i + 1, j];
                    float y01 = _heights[i, j + 1];
                    float y11 = _heights[i + 1, j + 1];

                    const float visualFloorEps = 0.0001f;
                    const float visualNudge = 0.005f;

                    if (y00 <= FloorY + visualFloorEps) y00 = FloorY - visualNudge;
                    if (y10 <= FloorY + visualFloorEps) y10 = FloorY - visualNudge;
                    if (y01 <= FloorY + visualFloorEps) y01 = FloorY - visualNudge;
                    if (y11 <= FloorY + visualFloorEps) y11 = FloorY - visualNudge;

                    // Fix normals for visual vertices
                    Vector3 n00 = _norms[i, j];
                    Vector3 n10 = _norms[i + 1, j];
                    Vector3 n01 = _norms[i, j + 1];
                    Vector3 n11 = _norms[i + 1, j + 1];

                    // Re-normalize normals after nudging so they don't produce seams 
                    n00 = n00.Normalized();
                    n10 = n10.Normalized();
                    n01 = n01.Normalized();
                    n11 = n11.Normalized();

                    Vector3 v00 = new Vector3(x0, y00, z0);
                    Vector3 v10 = new Vector3(x1, y10, z0);
                    Vector3 v01 = new Vector3(x0, y01, z1);
                    Vector3 v11 = new Vector3(x1, y11, z1);

                    Vector2 uv00 = new((float)i / (_N - 1), (float)j / (_N - 1));
                    Vector2 uv10 = new((float)(i + 1) / (_N - 1), (float)j / (_N - 1));
                    Vector2 uv01 = new((float)i / (_N - 1), (float)(j + 1) / (_N - 1));
                    Vector2 uv11 = new((float)(i + 1) / (_N - 1), (float)(j + 1) / (_N - 1));

                    bool flip = ((i + j) & 1) == 1;

                    if (!flip)
                    {
                        st.SetUV(uv00); st.SetNormal(n00); st.AddVertex(v00);
                        st.SetUV(uv10); st.SetNormal(n10); st.AddVertex(v10);
                        st.SetUV(uv01); st.SetNormal(n01); st.AddVertex(v01);

                        st.SetUV(uv10); st.SetNormal(n10); st.AddVertex(v10);
                        st.SetUV(uv11); st.SetNormal(n11); st.AddVertex(v11);
                        st.SetUV(uv01); st.SetNormal(n01); st.AddVertex(v01);
                    }
                    else
                    {
                        st.SetUV(uv00); st.SetNormal(n00); st.AddVertex(v00);
                        st.SetUV(uv11); st.SetNormal(n11); st.AddVertex(v11);
                        st.SetUV(uv01); st.SetNormal(n01); st.AddVertex(v01);

                        st.SetUV(uv00); st.SetNormal(n00); st.AddVertex(v00);
                        st.SetUV(uv10); st.SetNormal(n10); st.AddVertex(v10);
                        st.SetUV(uv11); st.SetNormal(n11); st.AddVertex(v11);
                    }
                }
            }

            st.Index();
            st.GenerateTangents();

            var mesh = st.Commit();
            _meshMI.Mesh = mesh;

            if (MaterialOverride != null)
                _meshMI.SetSurfaceOverrideMaterial(0, MaterialOverride);
        }

        // -------------------------------------------------------------------------
        // Internals
        // -------------------------------------------------------------------------
        private void EnsureChildren()
        {
            _meshMI = GetNodeOrNull<MeshInstance3D>("Mesh");
            if (_meshMI == null)
            {
                _meshMI = new MeshInstance3D { Name = "Mesh" };
                AddChild(_meshMI);
            }

            _staticBody = GetNodeOrNull<StaticBody3D>("Collider");
            if (_staticBody == null)
            {
                _staticBody = new StaticBody3D { Name = "Collider" };
                AddChild(_staticBody);
            }

            _colShape = _staticBody.GetNodeOrNull<CollisionShape3D>("Shape");
            if (_colShape == null)
            {
                _colShape = new CollisionShape3D { Name = "Shape" };
                _staticBody.AddChild(_colShape);
            }
        }
        private void FloorYFromNode()
        {
            if (FloorNodePath == null || FloorNodePath.IsEmpty)
                return; // fallback: use whatever FloorY is already

            var cyl = GetNodeOrNull<CsgCylinder3D>(FloorNodePath);
            if (cyl == null)
            {
                // Not a CsgCylinder3D? Use the node's origin as floor.
                var n = GetNodeOrNull<Node3D>(FloorNodePath);
                if (n != null) FloorY = n.GlobalTransform.Origin.Y;
                return;
            }

            // Top cap world Y = centerY + (height/2) * worldScaleY
            float worldScaleY = cyl.GlobalTransform.Basis.Y.Length();
            FloorY = cyl.GlobalTransform.Origin.Y + 0.5f * cyl.Height * worldScaleY;
            GD.Print($"[TerrainDisk] Floor from node '{cyl.Name}': topY={FloorY:F4}");
        }

        public float LowerAreaAndReturnRemovedVolume(Vector3 worldXZ, float radiusMeters, float maxDeltaHeight)
        {
            if (_heights == null) return 0f;
            if (maxDeltaHeight <= 0f || radiusMeters <= 0f) return 0f;

            // Convert world point to this node's local space
            Vector3 local = ToLocal(worldXZ);
            float cx = local.X;
            float cz = local.Z;

            // Quick out: if outside disk, nothing to remove
            if ((cx * cx + cz * cz) > (Radius * Radius))
                return 0f;

            // Center cell in grid space
            float fx = (cx + Radius) / _step;
            float fz = (cz + Radius) / _step;
            int ci = Mathf.Clamp(Mathf.FloorToInt(fx), 0, _N - 1);
            int cj = Mathf.Clamp(Mathf.FloorToInt(fz), 0, _N - 1);

            // Iterate only a small window around the circle
            int radiusInCells = Mathf.CeilToInt(radiusMeters / _step) + 1;
            int i0 = Mathf.Max(0, ci - radiusInCells);
            int i1 = Mathf.Min(_N - 1, ci + radiusInCells);
            int j0 = Mathf.Max(0, cj - radiusInCells);
            int j1 = Mathf.Min(_N - 1, cj + radiusInCells);

            float r2 = radiusMeters * radiusMeters;
            float cellArea = _step * _step;
            float removedVolume = 0f;

            for (int i = i0; i <= i1; i++)
            {
                float x = -Radius + i * _step; // cell center X (local)
                for (int j = j0; j <= j1; j++)
                {
                    float z = -Radius + j * _step; // cell center Z (local)
                    if (float.IsNaN(_heights[i, j])) continue;

                    float dx = x - cx;
                    float dz = z - cz;
                    if ((dx * dx + dz * dz) > r2) continue;

                    // Lower this cell, clamp at FloorY
                    float hOld = _heights[i, j];
                    float hNew = MathF.Max(FloorY, hOld - maxDeltaHeight);
                    float dh = hOld - hNew;
                    if (dh <= 0f) continue;

                    _heights[i, j] = hNew;
                    removedVolume += dh * cellArea;
                }
            }

            float clampedAngle = MathF.Max(angleOfReposeDeg, MinAngleDeg);
            float maxSlope = MathF.Tan(Mathf.DegToRad(clampedAngle));
            ApplyAngleOfRepose(worldXZ, radiusMeters * 1.5f, maxSlope, iterations: 2);

            // Update normals/mesh for current heights
            RebuildMeshOnly();

            return removedVolume; // in-situ m³ actually removed
        }

        /// <summary>
        /// OPTIMIZED VERSION: Lower terrain in circular area WITHOUT rebuilding mesh.
        /// Mesh update must be called separately (allows batching multiple dig operations).
        /// Returns the actual volume removed (in-situ, not swelled).
        /// </summary>
        public float LowerAreaWithoutMeshUpdate(Vector3 worldXZ, float radiusMeters, float maxDeltaHeight)
        {
            if (_heights == null) return 0f;
            if (maxDeltaHeight <= 0f || radiusMeters <= 0f) return 0f;

            // Convert world point to this node's local space
            Vector3 local = ToLocal(worldXZ);
            float cx = local.X;
            float cz = local.Z;

            // Quick out: if outside disk, nothing to remove
            if ((cx * cx + cz * cz) > (Radius * Radius))
                return 0f;

            // Center cell in grid space
            float fx = (cx + Radius) / _step;
            float fz = (cz + Radius) / _step;
            int ci = Mathf.Clamp(Mathf.FloorToInt(fx), 0, _N - 1);
            int cj = Mathf.Clamp(Mathf.FloorToInt(fz), 0, _N - 1);

            // Iterate only a small window around the circle
            int radiusInCells = Mathf.CeilToInt(radiusMeters / _step) + 1;
            int i0 = Mathf.Max(0, ci - radiusInCells);
            int i1 = Mathf.Min(_N - 1, ci + radiusInCells);
            int j0 = Mathf.Max(0, cj - radiusInCells);
            int j1 = Mathf.Min(_N - 1, cj + radiusInCells);

            float r2 = radiusMeters * radiusMeters;
            float cellArea = _step * _step;
            float removedVolume = 0f;

            for (int i = i0; i <= i1; i++)
            {
                float x = -Radius + i * _step; // cell center X (local)
                for (int j = j0; j <= j1; j++)
                {
                    float z = -Radius + j * _step; // cell center Z (local)
                    if (float.IsNaN(_heights[i, j])) continue;

                    float dx = x - cx;
                    float dz = z - cz;
                    if ((dx * dx + dz * dz) > r2) continue;

                    // Lower this cell, clamp at FloorY
                    float hOld = _heights[i, j];
                    float hNew = MathF.Max(FloorY, hOld - maxDeltaHeight);
                    float dh = hOld - hNew;
                    if (dh <= 0f) continue;

                    _heights[i, j] = hNew;
                    removedVolume += dh * cellArea;
                }
            }

            // Apply angle-of-repose smoothing in the same local region
            float clampedAngle = MathF.Max(angleOfReposeDeg, MinAngleDeg);
            float maxSlope = MathF.Tan(Mathf.DegToRad(clampedAngle));
            ApplyAngleOfRepose(worldXZ, radiusMeters * 1.5f, maxSlope, iterations: 2);

            // NOTE: Mesh update is NOT called here - caller must call RebuildMeshOnly() when ready
            return removedVolume; // in-situ m³ actually removed
        }

        /// <summary>
        /// Locally relax slopes in a circular region so that height differences
        /// don't exceed maxSlope * distance. This approximates an angle of repose
        /// without changing total volume.
        /// </summary>
        public void ApplyAngleOfRepose(Vector3 worldXZ, float radiusMeters, float maxSlope, int iterations = 2)
        {
            if (_heights == null) return;
            if (radiusMeters <= 0f || maxSlope <= 0f || iterations <= 0) return;

            // Convert center to local space
            Vector3 local = ToLocal(worldXZ);
            float cx = local.X;
            float cz = local.Z;

            // Quick out if outside disk
            if ((cx * cx + cz * cz) > (Radius * Radius))
                return;

            // Center in grid space
            float fx = (cx + Radius) / _step;
            float fz = (cz + Radius) / _step;
            int ci = Mathf.Clamp(Mathf.FloorToInt(fx), 0, _N - 1);
            int cj = Mathf.Clamp(Mathf.FloorToInt(fz), 0, _N - 1);

            int radiusInCells = Mathf.CeilToInt(radiusMeters / _step) + 1;
            int i0 = Mathf.Max(0, ci - radiusInCells);
            int i1 = Mathf.Min(_N - 1, ci + radiusInCells);
            int j0 = Mathf.Max(0, cj - radiusInCells);
            int j1 = Mathf.Min(_N - 1, cj + radiusInCells);

            int width = i1 - i0 + 1;
            int height = j1 - j0 + 1;

            float r2 = radiusMeters * radiusMeters;
            float[,] delta = new float[width, height];

            for (int iter = 0; iter < iterations; iter++)
            {
                // Clear deltas
                for (int jj = 0; jj < height; jj++)
                    for (int ii = 0; ii < width; ii++)
                        delta[ii, jj] = 0f;

                // Compute transfers (high -> low) so |dh| <= maxSlope * dist
                for (int j = j0; j <= j1; j++)
                {
                    float z = -Radius + j * _step;
                    int jj = j - j0;

                    for (int i = i0; i <= i1; i++)
                    {
                        float x = -Radius + i * _step;
                        int ii = i - i0;

                        if (float.IsNaN(_heights[i, j]))
                            continue;

                        float dxCenter = x - cx;
                        float dzCenter = z - cz;
                        if (dxCenter * dxCenter + dzCenter * dzCenter > r2)
                            continue;

                        // Only look "right" and "up" to avoid double-counting pairs
                        CheckNeighbor(i, j, i + 1, j, _step, maxSlope, i0, j0, delta, ii, jj);
                        CheckNeighbor(i, j, i, j + 1, _step, maxSlope, i0, j0, delta, ii, jj);
                    }
                }

                // Apply deltas, clamping to FloorY
                for (int j = j0; j <= j1; j++)
                {
                    int jj = j - j0;
                    for (int i = i0; i <= i1; i++)
                    {
                        int ii = i - i0;
                        if (float.IsNaN(_heights[i, j]))
                            continue;

                        float newH = _heights[i, j] + delta[ii, jj];
                        if (newH < FloorY) newH = FloorY;
                        _heights[i, j] = newH;
                    }
                }
            }

            // Local helper: pushes some height from higher to lower cell if slope too steep.
            void CheckNeighbor(
                int i, int j,
                int ni, int nj,
                float horizDist,
                float maxSlopeLocal,
                int baseI,
                int baseJ,
                float[,] deltaLocal,
                int ii,
                int jj)
            {
                if (ni < i0 || ni > i1 || nj < j0 || nj > j1)
                    return;

                if (float.IsNaN(_heights[ni, nj]))
                    return;

                float h = _heights[i, j];
                float hn = _heights[ni, nj];

                float maxDh = maxSlopeLocal * horizDist;
                float diff = h - hn;

                // if |diff| <= maxDh, slope is acceptable
                if (diff > maxDh)
                {
                    // current cell too high relative to neighbor
                    float excess = diff - maxDh;
                    float move = excess * 0.5f; // move part of it this iteration

                    deltaLocal[ii, jj] -= move;
                    deltaLocal[ni - baseI, nj - baseJ] += move;
                }
                else if (diff < -maxDh)
                {
                    // neighbor too high relative to current
                    float excess = -diff - maxDh;
                    float move = excess * 0.5f;

                    deltaLocal[ii, jj] += move;
                    deltaLocal[ni - baseI, nj - baseJ] -= move;
                }
            }
        }
    }
}