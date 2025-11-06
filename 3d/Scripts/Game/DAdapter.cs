using Godot;
using DCore;
using System;
using System.Collections.Generic;
using System.Linq;

public static class DAdapter
{
	// Map 3D (x,0,z, yaw) to math 2D (x,y,theta).
	private static (double x, double y, double th) ToMath3D(Vector3 pos, double yawRad)
		=> (pos.X, pos.Z, yawRad);

	public static (Vector3[] points, int[] gears) ComputePath3D(
		Vector3 startPos, double startYawRad,
		Vector3 goalPos, double goalYawRad,
		double turnRadiusMeters,
		float fieldRadius,
		double sampleStepMeters = 0.25)
	{
		// 1) 3D → math
		var sM = ToMath3D(startPos, startYawRad);
		var gM = ToMath3D(goalPos, goalYawRad);

		// 2) Normalize by R for planning (x,y only)
		double R = turnRadiusMeters;
		var sN = (sM.x / R, sM.y / R, sM.th);
		var gN = (gM.x / R, gM.y / R, gM.th);

		// 3) Optimal RS in normalized space
		// List<PathElement> best = DubinsPaths.GetOptimalPath(sN, gN);
		// if (best == null || best.Count == 0)
		// 	return (Array.Empty<Vector3>(), Array.Empty<int>());
		List<PathElement> best = null;

		//get the shortest valid path
		var all = DubinsPaths.GetAllPaths(sN, gN);
		if(all.Count == 0)
		{
			return (Array.Empty<Vector3>(), Array.Empty<int>());
		}
		var orderedAll = all.OrderBy(p => p.Sum(e => e.Param));
		foreach (var path in orderedAll)
		{
			if (ValidatePath(startPos, startYawRad, path, turnRadiusMeters, sampleStepMeters, fieldRadius))
			{
				best = path;
				break;
			}
		}
		if(best == null)
		{
			return (Array.Empty<Vector3>(), Array.Empty<int>());
		}




		// 4) Sample polyline and gears in local normalized (start at 0,0,0)
		var pts2D = new List<Vector2>();
		var gears = new List<int>();
		DSampler.SamplePolylineWithGears((0.0, 0.0, 0.0), best, 1.0, sampleStepMeters / R, pts2D, gears);

		// 5) Transform back to world-math (scale, rotate by start θ, translate by start x,y)
		var list3 = new List<Vector3>(pts2D.Count);
		double c0 = Math.Cos(sM.th), s0 = Math.Sin(sM.th);
		foreach (var p in pts2D)
		{
			double sx = p.X * R, sy = p.Y * R;
			double wx = sM.x + (sx * c0 - sy * s0);
			double wy = sM.y + (sx * s0 + sy * c0);
			list3.Add(new Vector3((float)wx, 0f, (float)wy)); // (x, 0, z=y)
		}
		return (list3.ToArray(), gears.ToArray());
	}
	
	private static bool ValidatePath(
		Vector3 startPos, double startYawRad,
		List<PathElement> testPath,
		double turnRadiusMeters,
		double sampleStepMeters,
		double maxRange
	)
	{
		double x = startPos.X;
		double y = startPos.Y;
		double theta = startYawRad;
		foreach (var elem in testPath)
		{
			double length = elem.Param;
			double dir = (elem.Steering == Steering.LEFT) ? -1.0 : (elem.Steering == Steering.RIGHT) ? 1.0 : 0.0;
			double distance = 0.0;

			while (distance < length)
			{
				double delta = Math.Min(sampleStepMeters, length - distance);
				if (dir == 0)
				{
					x += length * Math.Cos(theta);
					y += length * Math.Sin(theta);
					distance += length;
				}
				else
				{
					double dtheta = delta / turnRadiusMeters * dir;
					double cx = x - turnRadiusMeters * Math.Sin(theta) * dir;
					double cy = y + turnRadiusMeters * Math.Cos(theta) * dir;

					theta += dtheta;
					x = cx + turnRadiusMeters * Math.Sin(theta) * dir;
					y = cy - turnRadiusMeters * Math.Cos(theta) * dir;

					distance += sampleStepMeters;
				}

				double range = Mathf.Sqrt(x * x + y * y);
				if (range >= maxRange)
				{
					return false;
				}
			}
		}
		return true;
	}
}
