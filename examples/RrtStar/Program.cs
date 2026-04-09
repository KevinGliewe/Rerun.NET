// RRT* pathfinding visualization: obstacles, tree growth, and final path.
// Self-contained RRT* algorithm with seed=42 and 500 iterations.
// C# port of the Python "rrt-star" example.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_rrt_star");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "rrt_star.rrd");

// --- Configuration ---
const float mapWidth = 400f;
const float mapHeight = 400f;
const float stepSize = 15f;
const float goalRadius = 15f;
const float rewireRadius = 30f;
const int maxIterations = 500;

var start = new float[] { 20f, 20f };
var goal = new float[] { 380f, 380f };
var rng = new Random(42);

// --- Obstacles (axis-aligned rectangles defined by [x, y, w, h]) ---
var obstacles = new float[][]
{
    [100f, 0f, 30f, 200f],
    [200f, 150f, 30f, 250f],
    [300f, 0f, 30f, 300f],
    [50f, 300f, 150f, 30f],
    [250f, 100f, 30f, 100f],
};

// Log obstacles as line strips (rectangles)
var obstacleStrips = new LineStrip2D[obstacles.Length];
var obstacleColors = new Color[obstacles.Length];
for (var i = 0; i < obstacles.Length; i++)
{
    var ox = obstacles[i][0];
    var oy = obstacles[i][1];
    var ow = obstacles[i][2];
    var oh = obstacles[i][3];
    obstacleStrips[i] = new LineStrip2D([
        new Vec2D([ox, oy]),
        new Vec2D([ox + ow, oy]),
        new Vec2D([ox + ow, oy + oh]),
        new Vec2D([ox, oy + oh]),
        new Vec2D([ox, oy]),
    ]);
    obstacleColors[i] = new Color(new Rgba32(0xFF4444FFu));
}

rec.Log("rrt/obstacles", new LineStrips2D(obstacleStrips)
    .WithColors(obstacleColors)
    .WithRadii(new Radius(2f)), @static: true);

// Log start and goal
rec.Log("rrt/start", new Points2D(new Position2D(new Vec2D(start)))
    .WithColors(new Color(new Rgba32(0x00CC00FFu)))
    .WithRadii(new Radius(8f)), @static: true);
rec.Log("rrt/goal", new Points2D(new Position2D(new Vec2D(goal)))
    .WithColors(new Color(new Rgba32(0x0088FFFFu)))
    .WithRadii(new Radius(8f)), @static: true);

// --- RRT* Algorithm ---
var nodePositions = new List<float[]> { start };
var nodeParents = new List<int> { -1 };
var nodeCosts = new List<float> { 0f };
var goalReached = false;
var goalNodeIdx = -1;

static float Dist(float[] a, float[] b)
{
    var dx = a[0] - b[0];
    var dy = a[1] - b[1];
    return MathF.Sqrt(dx * dx + dy * dy);
}

bool SegmentIntersectsRect(float[] p1, float[] p2, float[] rect)
{
    // Check if line segment from p1 to p2 intersects the rectangle
    var rx = rect[0]; var ry = rect[1]; var rw = rect[2]; var rh = rect[3];

    // Use parametric line clipping (Liang-Barsky)
    var dx = p2[0] - p1[0];
    var dy = p2[1] - p1[1];
    float tMin = 0f, tMax = 1f;

    float[] p = { -dx, dx, -dy, dy };
    float[] q = { -(rx - p1[0]), rx + rw - p1[0], -(ry - p1[1]), ry + rh - p1[1] };

    for (var i = 0; i < 4; i++)
    {
        if (Math.Abs(p[i]) < 1e-8f)
        {
            if (q[i] < 0) return false;
        }
        else
        {
            var t = q[i] / p[i];
            if (p[i] < 0)
            {
                if (t > tMin) tMin = t;
            }
            else
            {
                if (t < tMax) tMax = t;
            }
            if (tMin > tMax) return false;
        }
    }
    return true;
}

bool CollisionFree(float[] from, float[] to)
{
    foreach (var obs in obstacles)
        if (SegmentIntersectsRect(from, to, obs))
            return false;
    return true;
}

for (var iter = 0; iter < maxIterations; iter++)
{
    rec.SetTimeSequence("iteration", iter);

    // Sample random point (with goal bias)
    float[] sample;
    if (rng.NextDouble() < 0.1)
        sample = goal;
    else
        sample = [(float)(rng.NextDouble() * mapWidth), (float)(rng.NextDouble() * mapHeight)];

    // Find nearest node
    var nearestIdx = 0;
    var nearestDist = float.MaxValue;
    for (var i = 0; i < nodePositions.Count; i++)
    {
        var d = Dist(nodePositions[i], sample);
        if (d < nearestDist)
        {
            nearestDist = d;
            nearestIdx = i;
        }
    }

    // Steer towards sample
    var nearest = nodePositions[nearestIdx];
    var dist = Dist(nearest, sample);
    float[] newNode;
    if (dist <= stepSize)
        newNode = sample;
    else
    {
        var ratio = stepSize / dist;
        newNode = [nearest[0] + (sample[0] - nearest[0]) * ratio,
                   nearest[1] + (sample[1] - nearest[1]) * ratio];
    }

    // Check collision
    if (!CollisionFree(nearest, newNode))
        continue;

    // Find nearby nodes for rewiring
    var newCost = nodeCosts[nearestIdx] + Dist(nearest, newNode);
    var bestParent = nearestIdx;
    var bestCost = newCost;

    var nearbyIndices = new List<int>();
    for (var i = 0; i < nodePositions.Count; i++)
    {
        if (Dist(nodePositions[i], newNode) < rewireRadius)
        {
            nearbyIndices.Add(i);
            var candidateCost = nodeCosts[i] + Dist(nodePositions[i], newNode);
            if (candidateCost < bestCost && CollisionFree(nodePositions[i], newNode))
            {
                bestParent = i;
                bestCost = candidateCost;
            }
        }
    }

    // Add the new node
    var newIdx = nodePositions.Count;
    nodePositions.Add(newNode);
    nodeParents.Add(bestParent);
    nodeCosts.Add(bestCost);

    // Rewire nearby nodes through new node if cheaper
    foreach (var ni in nearbyIndices)
    {
        var newPathCost = bestCost + Dist(newNode, nodePositions[ni]);
        if (newPathCost < nodeCosts[ni] && CollisionFree(newNode, nodePositions[ni]))
        {
            nodeParents[ni] = newIdx;
            nodeCosts[ni] = newPathCost;
        }
    }

    // Check if goal is reached
    if (Dist(newNode, goal) < goalRadius)
    {
        if (!goalReached || bestCost + Dist(newNode, goal) < nodeCosts[goalNodeIdx])
        {
            goalReached = true;
            goalNodeIdx = newIdx;
        }
    }

    // Log tree nodes every 10 iterations
    if (iter % 10 == 0 || iter == maxIterations - 1)
    {
        var treePoints = nodePositions.Select(p =>
            new Position2D(new Vec2D(p))).ToArray();
        rec.Log("rrt/tree/nodes", new Points2D(treePoints)
            .WithColors(new Color(new Rgba32(0xAAAAAAFFu)))
            .WithRadii(new Radius(2f)));

        // Log tree edges
        var edgeStrips = new List<LineStrip2D>();
        for (var i = 1; i < nodePositions.Count; i++)
        {
            var parent = nodeParents[i];
            if (parent >= 0)
            {
                edgeStrips.Add(new LineStrip2D([
                    new Vec2D(nodePositions[parent]),
                    new Vec2D(nodePositions[i])
                ]));
            }
        }
        if (edgeStrips.Count > 0)
        {
            rec.Log("rrt/tree/edges", new LineStrips2D(edgeStrips.ToArray())
                .WithColors(new Color(new Rgba32(0x666666FFu)))
                .WithRadii(new Radius(0.5f)));
        }
    }
}

// Log final path if goal was reached
if (goalReached)
{
    var pathNodes = new List<float[]>();
    var current = goalNodeIdx;
    while (current >= 0)
    {
        pathNodes.Add(nodePositions[current]);
        current = nodeParents[current];
    }
    pathNodes.Reverse();

    // Add the goal itself
    pathNodes.Add(goal);

    var pathStrip = new LineStrip2D(pathNodes.Select(p => new Vec2D(p)).ToArray());
    rec.Log("rrt/path", new LineStrips2D(pathStrip)
        .WithColors(new Color(new Rgba32(0x00FF88FFu)))
        .WithRadii(new Radius(3f)), @static: true);

    Console.WriteLine($"RRT* found a path with {pathNodes.Count} waypoints after {maxIterations} iterations. Use --spawn to open viewer.");
}
else
{
    Console.WriteLine($"RRT* did not find a path after {maxIterations} iterations. Use --spawn to open viewer.");
}
