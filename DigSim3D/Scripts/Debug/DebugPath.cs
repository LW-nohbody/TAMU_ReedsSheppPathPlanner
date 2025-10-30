using Godot;
using System;
using System.Globalization;
using System.Text;

namespace DigSim3D.Debugging
{
    public static class DebugPath
    {
        public static bool Enabled = true;
        public static int  Verbosity = 2;

        public static string Begin(string who, int vehId, int sliceId)
        {
            string id = $"{who}/veh{vehId}/slice{sliceId}/{DateTime.UtcNow.Ticks}";
            if (Enabled && Verbosity >= 1) GD.Print($"[PATH/BEGIN] {id}");
            return id;
        }

        public static void Check(string id, string tag, params (string k, object v)[] kv)
        {
            if (!Enabled || Verbosity < 2) return;
            var sb = new StringBuilder();
            foreach (var (k,v) in kv) sb.Append($" {k}={Fmt(v)}");
            // GD.Print($"[PATH/CHECK] {id} :: {tag}:{sb}");
        }

        public static void End(string id, string status="ok", params (string k, object v)[] kv)
        {
            if (!Enabled || Verbosity < 1) return;
            var sb = new StringBuilder();
            foreach (var (k,v) in kv) sb.Append($" {k}={Fmt(v)}");
            GD.Print($"[PATH/END] {id} :: {status}{sb}");
        }

        static string Fmt(object v) => v switch
        {
            float f  => f.ToString("0.000000", CultureInfo.InvariantCulture),
            double d => d.ToString("0.000000", CultureInfo.InvariantCulture),
            Vector3 p => $"({p.X:0.000},{p.Y:0.000},{p.Z:0.000})",
            _ => v?.ToString() ?? "null"
        };
    }
}