using Godot;
using System;

[Tool]
public partial class TerrainDisk : Node3D
{
    [Export] public float Radius = 15f;
    [Export(PropertyHint.Range, "32,1024,1")] public int Resolution = 256;

    // your gentler terrain settings
    [Export] public float Amplitude  = 0.35f;
    [Export] public float Frequency  = 0.04f;
    [Export] public int   Octaves    = 2;
    [Export] public float Lacunarity = 2.0f;
    [Export] public float Gain       = 0.4f;
    [Export] public int   Seed       = 1337;
    [Export(PropertyHint.Range, "0,1,0.01")] public float Smooth = 0.6f;

    [Export] public Material MaterialOverride;

    private MeshInstance3D   _meshMI;
    private StaticBody3D     _staticBody;
    private CollisionShape3D _colShape;

    public override void _Ready()
    {
        EnsureChildren();
        Rebuild();
        if (Engine.IsEditorHint()) SetProcess(false);
    }

    private void EnsureChildren()
    {
        _meshMI = GetNodeOrNull<MeshInstance3D>("Mesh");
        if (_meshMI == null) { _meshMI = new MeshInstance3D { Name = "Mesh" }; AddChild(_meshMI); }

        _staticBody = GetNodeOrNull<StaticBody3D>("Collider");
        if (_staticBody == null) { _staticBody = new StaticBody3D { Name = "Collider" }; AddChild(_staticBody); }

        _colShape = _staticBody.GetNodeOrNull<CollisionShape3D>("Shape");
        if (_colShape == null) { _colShape = new CollisionShape3D { Name = "Shape" }; _staticBody.AddChild(_colShape); }
    }

    private void Rebuild()
    {
        // --- noise ---
        var noise = new FastNoiseLite
        {
            Seed      = Seed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = Frequency
        };
        noise.FractalOctaves    = Octaves;
        noise.FractalLacunarity = Lacunarity;
        noise.FractalGain       = Gain;

        float NRaw(float x, float z) => noise.GetNoise2D(x, z);

        // blend a small world-space box blur for rolling hills
        float Smoothed(float x, float z, float r)
        {
            float c  = NRaw(x, z);
            float n1 = NRaw(x + r, z) + NRaw(x - r, z) + NRaw(x, z + r) + NRaw(x, z - r);
            float n2 = NRaw(x + r, z + r) + NRaw(x + r, z - r) + NRaw(x - r, z + r) + NRaw(x - r, z - r);
            float avg = (4f * c + 2f * n1 + 1f * n2) / (4f + 2f * 4f + 4f);
            return Mathf.Lerp(c, avg, Mathf.Clamp(Smooth, 0f, 1f));
        }

        int   N    = Mathf.Max(32, Resolution);
        float size = Radius * 2f;
        float step = size / (N - 1);
        float blurR = step * 2f;

        // Precompute heights and per-vertex normals (central differences)
        var heights = new float[N, N];
        var norms   = new Vector3[N, N];

        bool Inside(float x, float z) => (x * x + z * z) <= (Radius * Radius);

        for (int j = 0; j < N; j++)
        {
            float z = -Radius + j * step;
            for (int i = 0; i < N; i++)
            {
                float x = -Radius + i * step;
                if (Inside(x, z))
                {
                    float n = Smoothed(x, z, blurR);
                    heights[i, j] = Amplitude * n;
                }
                else
                {
                    heights[i, j] = float.NaN; // mark outside disk
                }
            }
        }

        // normals from height gradients; use step as world dx/dz
        for (int j = 0; j < N; j++)
        {
            for (int i = 0; i < N; i++)
            {
                if (float.IsNaN(heights[i, j])) { norms[i, j] = Vector3.Up; continue; }

                int il = Math.Max(0, i - 1), ir = Math.Min(N - 1, i + 1);
                int jd = Math.Max(0, j - 1), ju = Math.Min(N - 1, j + 1);

                float hL = heights[il, j]; if (float.IsNaN(hL)) hL = heights[i, j];
                float hR = heights[ir, j]; if (float.IsNaN(hR)) hR = heights[i, j];
                float hD = heights[i, jd]; if (float.IsNaN(hD)) hD = heights[i, j];
                float hU = heights[i, ju]; if (float.IsNaN(hU)) hU = heights[i, j];

                // build tangent vectors in world space
                Vector3 dx = new Vector3(2f * step, hR - hL, 0f);
                Vector3 dz = new Vector3(0f, hU - hD, 2f * step);

                // y-up normal: dz × dx (order matters)
                norms[i, j] = dz.Cross(dx).Normalized();
            }
        }

        // Emit triangles, using checkerboard diagonal flip and per-vertex normals
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

                if (float.IsNaN(heights[i, j]) || float.IsNaN(heights[i + 1, j]) ||
                    float.IsNaN(heights[i, j + 1]) || float.IsNaN(heights[i + 1, j + 1]))
                    continue;

                Vector3 v00 = new Vector3(x0, heights[i, j],     z0);
                Vector3 v10 = new Vector3(x1, heights[i + 1, j], z0);
                Vector3 v01 = new Vector3(x0, heights[i, j + 1], z1);
                Vector3 v11 = new Vector3(x1, heights[i + 1, j + 1], z1);

                Vector2 uv00 = new((float)i / (N - 1),       (float)j / (N - 1));
                Vector2 uv10 = new((float)(i + 1) / (N - 1), (float)j / (N - 1));
                Vector2 uv01 = new((float)i / (N - 1),       (float)(j + 1) / (N - 1));
                Vector2 uv11 = new((float)(i + 1) / (N - 1), (float)(j + 1) / (N - 1));

                bool flip = ((i + j) & 1) == 1; // checkerboard

                if (!flip)
                {
                    // tri: v00-v10-v01
                    st.SetUV(uv00); st.SetNormal(norms[i, j]);         st.AddVertex(v00);
                    st.SetUV(uv10); st.SetNormal(norms[i + 1, j]);     st.AddVertex(v10);
                    st.SetUV(uv01); st.SetNormal(norms[i, j + 1]);     st.AddVertex(v01);
                    // tri: v10-v11-v01
                    st.SetUV(uv10); st.SetNormal(norms[i + 1, j]);     st.AddVertex(v10);
                    st.SetUV(uv11); st.SetNormal(norms[i + 1, j + 1]); st.AddVertex(v11);
                    st.SetUV(uv01); st.SetNormal(norms[i, j + 1]);     st.AddVertex(v01);
                }
                else
                {
                    // flipped diagonal
                    // tri: v00-v11-v01
                    st.SetUV(uv00); st.SetNormal(norms[i, j]);         st.AddVertex(v00);
                    st.SetUV(uv11); st.SetNormal(norms[i + 1, j + 1]); st.AddVertex(v11);
                    st.SetUV(uv01); st.SetNormal(norms[i, j + 1]);     st.AddVertex(v01);
                    // tri: v00-v10-v11
                    st.SetUV(uv00); st.SetNormal(norms[i, j]);         st.AddVertex(v00);
                    st.SetUV(uv10); st.SetNormal(norms[i + 1, j]);     st.AddVertex(v10);
                    st.SetUV(uv11); st.SetNormal(norms[i + 1, j + 1]); st.AddVertex(v11);
                }
            }
        }

        // Index now that normals/UVs are baked, then generate tangents for normal maps later
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
                Roughness   = 1.0f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel
            };
            _meshMI.SetSurfaceOverrideMaterial(0, mat);
        }

        _colShape.Shape = new ConcavePolygonShape3D { Data = mesh.GetFaces() };
    }
}