// Examples of logging graph data to Rerun and performing force-based layouts.
// C# port of https://github.com/rerun-io/rerun/blob/docs-latest/examples/python/graphs/graphs.py
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

var colorScheme = new uint[]
{
    0xE41A1CFFu, // Red
    0x377EB8FFu, // Blue
    0x4DAF4AFFu, // Green
    0x984EA3FFu, // Purple
    0xFF7F00FFu, // Orange
    0xFFFF33FFu, // Yellow
    0xA65628FFu, // Brown
    0xF781BFFFu, // Pink
    0x999999FFu, // Gray
};

using var rec = new RecordingStream("rerun_example_graphs");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "graphs.rrd");

var description = @"# Graphs
This example shows various graph visualizations that you can create using Rerun.
In this example, the node positions—and therefore the graph layout—are computed by Rerun internally.";

rec.Log("description", new TextDocument(new Text(new Utf8(description)))
    .WithMediaType(new MediaType(new Utf8("text/markdown"))));

LogTrees(rec, colorScheme);
LogLattice(rec, 10);
LogMarkovChain(rec);

rec.FlushBlocking(5.0f);
Console.WriteLine("Logged graph examples (trees, lattice, Markov chain). Use --spawn to open viewer.");

// --- Tree / Bubble chart ---
static void LogTrees(RecordingStream rec, uint[] colorScheme)
{
    var rng = new Random(42);
    var nodes = new List<string> { "root" };
    var radii = new List<float> { 42f };
    var colors = new List<uint> { 0x515151FFu };
    var edges = new List<(string, string)>();

    for (var i = 0; i < 50; i++)
    {
        var existing = nodes[rng.Next(nodes.Count)];
        var newNode = i.ToString();
        nodes.Add(newNode);
        radii.Add(rng.Next(10, 51));
        colors.Add(colorScheme[rng.Next(colorScheme.Length)]);
        edges.Add((existing, newNode));

        rec.SetTimeSequence("frame", i);

        var graphNodes = new GraphNodes(nodes.Select(n => new GraphNode(new Utf8(n))).ToArray())
            .WithLabels(nodes.Select(n => new Text(new Utf8(n))).ToArray())
            .WithRadii(radii.Select(r => new Radius(r)).ToArray())
            .WithColors(colors.Select(c => new Color(new Rgba32(c))).ToArray());

        var graphEdges = new GraphEdges(
                edges.Select(e => new GraphEdge(new Utf8Pair(new Utf8(e.Item1), new Utf8(e.Item2)))).ToArray())
            .WithGraphType(new Rerun.Net.Components.GraphType(GraphTypeValue.Directed));

        rec.Log("node_link", graphNodes);
        rec.Log("node_link", graphEdges);

        // Bubble chart: same nodes, no edges
        rec.Log("bubble_chart", graphNodes);
    }
}

// --- Lattice ---
static void LogLattice(RecordingStream rec, int numNodes)
{
    var nodes = new List<GraphNode>();
    var nodeColors = new List<Color>();
    var labels = new List<Text>();

    for (var y = 0; y < numNodes; y++)
    {
        for (var x = 0; x < numNodes; x++)
        {
            var idx = y * numNodes + x;
            nodes.Add(new GraphNode(new Utf8(idx.ToString())));
            var r = (byte)(255f * x / (numNodes - 1));
            var g = (byte)(255f * y / (numNodes - 1));
            nodeColors.Add(new Color(new Rgba32((uint)(r << 24 | g << 16 | 0 << 8 | 255))));
            labels.Add(new Text(new Utf8($"({x}, {y})")));
        }
    }

    rec.Log("lattice", new GraphNodes(nodes.ToArray())
        .WithColors(nodeColors.ToArray())
        .WithLabels(labels.ToArray()), @static: true);

    var edges = new List<GraphEdge>();
    for (var y = 0; y < numNodes; y++)
    {
        for (var x = 0; x < numNodes; x++)
        {
            if (y > 0)
            {
                var source = (y - 1) * numNodes + x;
                var target = y * numNodes + x;
                edges.Add(new GraphEdge(new Utf8Pair(new Utf8(source.ToString()), new Utf8(target.ToString()))));
            }
            if (x > 0)
            {
                var source = y * numNodes + (x - 1);
                var target = y * numNodes + x;
                edges.Add(new GraphEdge(new Utf8Pair(new Utf8(source.ToString()), new Utf8(target.ToString()))));
            }
        }
    }

    rec.Log("lattice", new GraphEdges(edges.ToArray())
        .WithGraphType(new Rerun.Net.Components.GraphType(GraphTypeValue.Directed)), @static: true);
}

// --- Markov chain ---
static void LogMarkovChain(RecordingStream rec)
{
    var transitionMatrix = new double[,]
    {
        { 0.8, 0.1, 0.1 }, // sunny
        { 0.3, 0.4, 0.3 }, // rainy
        { 0.2, 0.3, 0.5 }, // cloudy
    };
    var stateNames = new[] { "sunny", "rainy", "cloudy" };
    var positions = new Vec2D[] { new([0f, 0f]), new([150f, 150f]), new([300f, 0f]) };
    var inactiveColor = 0x999999FFu;
    var activeColors = new uint[] { 0xFF7F00FFu, 0x377EB8FFu, 0x984EA3FFu };

    var edges = new List<GraphEdge>();
    for (var i = 0; i < stateNames.Length; i++)
        for (var j = 0; j < stateNames.Length; j++)
            if (transitionMatrix[i, j] > 0)
                edges.Add(new GraphEdge(new Utf8Pair(new Utf8(stateNames[i]), new Utf8(stateNames[j]))));
    edges.Add(new GraphEdge(new Utf8Pair(new Utf8("start"), new Utf8("sunny"))));

    var rng = new Random(42);
    var state = "sunny";

    for (var frame = 0; frame < 50; frame++)
    {
        var currentIdx = Array.IndexOf(stateNames, state);
        var nextIdx = WeightedChoice(rng, transitionMatrix, currentIdx);
        state = stateNames[nextIdx];

        var colors = stateNames.Select((_, i) =>
            new Color(new Rgba32(i == nextIdx ? activeColors[i] : inactiveColor))).ToArray();

        rec.SetTimeSequence("frame", frame);
        rec.Log("markov_chain", new GraphNodes(
                stateNames.Select(n => new GraphNode(new Utf8(n))).ToArray())
            .WithLabels(stateNames.Select(n => new Text(new Utf8(n))).ToArray())
            .WithColors(colors)
            .WithPositions(positions.Select(p => new Position2D(p)).ToArray()));
        rec.Log("markov_chain", new GraphEdges(edges.ToArray())
            .WithGraphType(new Rerun.Net.Components.GraphType(GraphTypeValue.Directed)));
    }
}

static int WeightedChoice(Random rng, double[,] matrix, int row)
{
    var r = rng.NextDouble();
    var cumulative = 0.0;
    for (var i = 0; i < matrix.GetLength(1); i++)
    {
        cumulative += matrix[row, i];
        if (r <= cumulative) return i;
    }
    return matrix.GetLength(1) - 1;
}
