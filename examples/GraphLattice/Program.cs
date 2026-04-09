// 10x10 lattice graph visualization using GraphNodes and GraphEdges.
// C# port of the Python graph lattice example.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_graph_lattice");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "graph_lattice.rrd");

const int numNodes = 10;

// Build nodes with gradient colors
var nodes = new GraphNode[numNodes * numNodes];
var nodeColors = new Color[numNodes * numNodes];
var labels = new Text[numNodes * numNodes];

for (var y = 0; y < numNodes; y++)
{
    for (var x = 0; x < numNodes; x++)
    {
        var idx = y * numNodes + x;
        nodes[idx] = new GraphNode(new Utf8(idx.ToString()));
        var r = (byte)(255f * x / (numNodes - 1));
        var g = (byte)(255f * y / (numNodes - 1));
        nodeColors[idx] = new Color(new Rgba32((uint)(r << 24 | g << 16 | 0 << 8 | 255)));
        labels[idx] = new Text(new Utf8($"({x}, {y})"));
    }
}

rec.Log("lattice", new GraphNodes(nodes)
    .WithColors(nodeColors)
    .WithLabels(labels), @static: true);

// Build edges: horizontal and vertical connections
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

Console.WriteLine($"Logged a {numNodes}x{numNodes} lattice graph with {nodes.Length} nodes and {edges.Count} edges. Use --spawn to open viewer.");
