using Godot;
using System;
using System.Linq;
using System.Collections.Generic;

public partial class TestReedsShepp : Node
{
    public override void _Ready()
    {
        // Example start and end poses
        var start = (0.0, 0.0, 0.0);                // (x, y, theta)
        var end = (5.0, 5.0, Math.PI / 2.0);       // 90 degrees

        // Get all possible paths
        var allPaths = ReedsSheppPaths.GetAllPaths(start, end);

        GD.Print($"Found {allPaths.Count} candidate paths.\n");

        int i = 1;
        foreach (var path in allPaths)
        {
            double total = path.Sum(e => e.Param);
            GD.Print($"Path {i}: length = {Math.Round(total, 3)}");

            foreach (var elem in path)
            {
                GD.Print("   " + elem.ToString());
            }

            i++;
        }

        GD.Print("\n--- Optimal Path ---");
        var best = ReedsSheppPaths.GetOptimalPath(start, end);
        double bestLen = best.Sum(e => e.Param);
        GD.Print($"Best path length = {Math.Round(bestLen, 3)}");
        foreach (var elem in best)
        {
            GD.Print("   " + elem.ToString());
        }
    }
}
