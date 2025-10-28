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
        [Export] public float Amplitude = 0.35f;
        [Export] public float Frequency = 0.04f;
        [Export] public int Octaves = 2;
        [Export] public float Lacunarity = 2.0f;
        [Export] public float Gain = 0.4f;
        [Export] public int Seed = 1337;
        [Export(PropertyHint.Range, "0,1,0.01")] public float Smooth = 0.6f;

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

        public override void _Ready()
        {
            EnsureChildren();
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
                        _heights[i, j] = Amplitude * n;
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

                    Vector3 v00 = new Vector3(x0, _heights[i, j], z0);
                    Vector3 v10 = new Vector3(x1, _heights[i + 1, j], z0);
                    Vector3 v01 = new Vector3(x0, _heights[i, j + 1], z1);
                    Vector3 v11 = new Vector3(x1, _heights[i + 1, j + 1], z1);

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
            return true;
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
    }
}