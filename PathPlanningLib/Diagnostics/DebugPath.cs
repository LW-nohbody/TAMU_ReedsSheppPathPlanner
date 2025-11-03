using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace PathPlanningLib.Diagnostics
{
    public static class DebugPath
    {
        public static bool Enabled = false;  // turn on only when needed
        public static int  Verbosity = 2;

        public static string Begin(string who, int vehId, int sliceId)
        {
            string id = $"{who}/veh{vehId}/slice{sliceId}/{DateTime.UtcNow.Ticks}";
            if (Enabled && Verbosity >= 1) Debug.WriteLine($"[LIB/BEGIN] {id}");
            return id;
        }

        public static void Check(string id, string tag, params (string k, object v)[] kv)
        {
            if (!Enabled || Verbosity < 2) return;
            var sb = new StringBuilder();
            foreach (var (k,v) in kv) sb.Append($" {k}={Fmt(v)}");
            Debug.WriteLine($"[LIB/CHECK] {id} :: {tag}:{sb}");
        }

        public static void End(string id, string status="ok", params (string k, object v)[] kv)
        {
            if (!Enabled || Verbosity < 1) return;
            var sb = new StringBuilder();
            foreach (var (k,v) in kv) sb.Append($" {k}={Fmt(v)}");
            Debug.WriteLine($"[LIB/END] {id} :: {status}{sb}");
        }

        static string Fmt(object v) => v switch
        {
            float f  => f.ToString("0.000000", CultureInfo.InvariantCulture),
            double d => d.ToString("0.000000", CultureInfo.InvariantCulture),
            _ => v?.ToString() ?? "null"
        };
    }
}