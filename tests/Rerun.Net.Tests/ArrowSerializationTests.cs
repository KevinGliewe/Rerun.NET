using Apache.Arrow;
using Rerun.Net;
using Rerun.Net.Components;
using Rerun.Net.Datatypes;
using Xunit;

namespace Rerun.Net.Tests;

public class ArrowSerializationTests
{
    [Fact]
    public void Vec3D_ToArrow_CorrectLength()
    {
        var data = new Vec3D[] { new([1f, 2f, 3f]), new([4f, 5f, 6f]) };
        var array = Vec3D.ToArrow(data);
        Assert.Equal(2, array.Length);
    }

    [Fact]
    public void Vec3D_ToArrow_CorrectValues()
    {
        var data = new Vec3D[] { new([1f, 2f, 3f]) };
        var array = Vec3D.ToArrow(data);
        Assert.IsType<FixedSizeListArray>(array);
        var fsl = (FixedSizeListArray)array;
        var values = (FloatArray)fsl.Values;
        Assert.Equal(1f, values.GetValue(0));
        Assert.Equal(2f, values.GetValue(1));
        Assert.Equal(3f, values.GetValue(2));
    }

    [Fact]
    public void Vec3D_ToArrow_EmptySpan()
    {
        var array = Vec3D.ToArrow(ReadOnlySpan<Vec3D>.Empty);
        Assert.Equal(0, array.Length);
    }

    [Fact]
    public void Rgba32_ToArrow_CorrectValues()
    {
        var data = new Rgba32[] { new(0xFF0000FFu), new(0x00FF00FFu) };
        var array = Rgba32.ToArrow(data);
        Assert.IsType<UInt32Array>(array);
        var u32 = (UInt32Array)array;
        Assert.Equal(0xFF0000FFu, u32.GetValue(0));
        Assert.Equal(0x00FF00FFu, u32.GetValue(1));
    }

    [Fact]
    public void Position3D_ToArrow_DelegatesToVec3D()
    {
        var data = new Position3D[] { new(new Vec3D([1f, 2f, 3f])) };
        var array = Position3D.ToArrow(data);
        // Position3D is transparent over Vec3D — same FixedSizeList output
        Assert.IsType<FixedSizeListArray>(array);
        Assert.Equal(1, array.Length);
    }

    [Fact]
    public void Radius_ToArrow_CorrectValues()
    {
        var data = new Radius[] { new(0.5f), new(1.0f) };
        var array = Radius.ToArrow(data);
        Assert.IsType<FloatArray>(array);
        var f = (FloatArray)array;
        Assert.Equal(0.5f, f.GetValue(0));
        Assert.Equal(1.0f, f.GetValue(1));
    }

    [Fact]
    public void Color_ToArrow_CorrectValues()
    {
        var data = new Color[] { new(new Rgba32(0xAABBCCDDu)) };
        var array = Color.ToArrow(data);
        Assert.IsType<UInt32Array>(array);
        Assert.Equal(0xAABBCCDDu, ((UInt32Array)array).GetValue(0));
    }

    [Fact]
    public void Points3D_AsBatches_ReturnsCorrectCount()
    {
        var points = new Rerun.Net.Archetypes.Points3D(
                new Position3D(new Vec3D([0f, 0f, 0f])))
            .WithColors(new Color(new Rgba32(0xFF0000FFu)));

        var batches = points.AsBatches();
        Assert.Equal(2, batches.Count); // positions + colors
    }

    [Fact]
    public void Points3D_AsBatches_RequiredOnly()
    {
        var points = new Rerun.Net.Archetypes.Points3D(
            new Position3D(new Vec3D([1f, 2f, 3f])));

        var batches = points.AsBatches();
        Assert.Single(batches); // positions only
    }

    [Fact]
    public void ClassId_ToArrow_CorrectValues()
    {
        var data = new Components.ClassId[] { new(new Datatypes.ClassId(1)), new(new Datatypes.ClassId(42)) };
        var array = Components.ClassId.ToArrow(data);
        Assert.IsType<UInt16Array>(array);
        var u16 = (UInt16Array)array;
        Assert.Equal((ushort)1, u16.GetValue(0));
        Assert.Equal((ushort)42, u16.GetValue(1));
    }

    [Fact]
    public void Text_ToArrow_CorrectValues()
    {
        var data = new Text[] { new(new Utf8("hello")), new(new Utf8("world")) };
        var array = Text.ToArrow(data);
        Assert.IsType<StringArray>(array);
        var sa = (StringArray)array;
        Assert.Equal("hello", sa.GetString(0));
        Assert.Equal("world", sa.GetString(1));
    }
}
