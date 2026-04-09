// Animated analog clock with second, minute, and hour hands using Arrows3D.
// C# port of the Python "clock" example.
using Rerun.Net;
using Rerun.Net.Archetypes;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;

using var rec = new RecordingStream("rerun_example_clock");
if (args.Contains("--spawn")) rec.Spawn();
else rec.Save(args.FirstOrDefault(a => !a.StartsWith("-")) ?? "clock.rrd");

// Log the clock face as a static circle of 12 hour markers
var markerPositions = new Position3D[12];
for (var i = 0; i < 12; i++)
{
    var angle = Math.PI / 2.0 - i * Math.PI * 2.0 / 12.0;
    markerPositions[i] = new Position3D(new Vec3D([
        (float)(4.5 * Math.Cos(angle)),
        (float)(4.5 * Math.Sin(angle)),
        0f
    ]));
}

rec.Log("clock/face", new Points3D(markerPositions)
    .WithRadii(new Radius(0.15f))
    .WithColors(new Color(new Rgba32(0xAAAAAAFFu))), @static: true);

// Animate the clock: 200 steps covering ~3.3 minutes of clock time
// Each step = 1 second of clock time
const int numSteps = 200;

for (var step = 0; step < numSteps; step++)
{
    rec.SetTimeSequence("step", step);

    var totalSeconds = step;
    var seconds = totalSeconds % 60;
    var minutes = (totalSeconds / 60.0) % 60;
    var hours = (totalSeconds / 3600.0) % 12;

    // Angles: 12 o'clock is +Y, clockwise
    var secAngle = Math.PI / 2.0 - seconds * Math.PI * 2.0 / 60.0;
    var minAngle = Math.PI / 2.0 - minutes * Math.PI * 2.0 / 60.0;
    var hrAngle = Math.PI / 2.0 - hours * Math.PI * 2.0 / 12.0;

    // Second hand: long and thin, red
    var secLength = 4.0f;
    rec.Log("clock/second_hand", new Arrows3D(
            new Vector3D(new Vec3D([
                (float)(secLength * Math.Cos(secAngle)),
                (float)(secLength * Math.Sin(secAngle)),
                0f])))
        .WithOrigins(new Position3D(new Vec3D([0f, 0f, 0f])))
        .WithColors(new Color(new Rgba32(0xCC0000FFu)))
        .WithRadii(new Radius(0.02f)));

    // Minute hand: medium length, white
    var minLength = 3.5f;
    rec.Log("clock/minute_hand", new Arrows3D(
            new Vector3D(new Vec3D([
                (float)(minLength * Math.Cos(minAngle)),
                (float)(minLength * Math.Sin(minAngle)),
                0f])))
        .WithOrigins(new Position3D(new Vec3D([0f, 0f, 0f])))
        .WithColors(new Color(new Rgba32(0xCCCCCCFFu)))
        .WithRadii(new Radius(0.04f)));

    // Hour hand: short and thick, white
    var hrLength = 2.5f;
    rec.Log("clock/hour_hand", new Arrows3D(
            new Vector3D(new Vec3D([
                (float)(hrLength * Math.Cos(hrAngle)),
                (float)(hrLength * Math.Sin(hrAngle)),
                0f])))
        .WithOrigins(new Position3D(new Vec3D([0f, 0f, 0f])))
        .WithColors(new Color(new Rgba32(0xFFFFFFFFu)))
        .WithRadii(new Radius(0.06f)));
}

Console.WriteLine($"Logged {numSteps} frames of an analog clock. Use --spawn to open viewer.");
