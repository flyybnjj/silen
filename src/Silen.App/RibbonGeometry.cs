using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Silen.App;

/// <summary>A textured membrane folding around a soft central wing hinge.</summary>
internal sealed class RibbonGeometry
{
    internal const int HorizontalSegments = 64;
    internal const int VerticalSegments = 40;
    internal const double CameraWidth = 4.2;
    internal const double ImageWidth = 3.5;
    private const int Columns = HorizontalSegments + 1;

    private readonly Point3D[] rest;
    private readonly Point3D[] working;
    private const double HingeX = ImageWidth * .06;
    private readonly double[] wingDistance;
    private readonly double[] bendWeight;
    private readonly double[] verticalFold;
    private readonly double[] flutter;

    internal Point3DCollection RestPositions { get; }
    internal PointCollection TextureCoordinates { get; }
    internal Vector3DCollection Normals { get; }
    internal Int32Collection Triangles { get; }
    internal int VertexCount => rest.Length;

    internal RibbonGeometry(double aspectRatio)
    {
        var height = ImageWidth / aspectRatio;
        var count = Columns * (VerticalSegments + 1);
        rest = new Point3D[count];
        working = new Point3D[count];
        wingDistance = new double[count];
        bendWeight = new double[count];
        verticalFold = new double[count];
        flutter = new double[count];
        var coordinates = new PointCollection(count);
        var normals = new Vector3DCollection(count);

        for (var row = 0; row <= VerticalSegments; row++)
        {
            var v = (double)row / VerticalSegments;
            var ny = 1 - 2 * v;
            for (var column = 0; column <= HorizontalSegments; column++)
            {
                var u = (double)column / HorizontalSegments;
                var nx = 2 * u - 1;
                var index = row * Columns + column;
                var p = new Point3D(nx * ImageWidth * .5, ny * height * .5, 0);
                rest[index] = p;
                coordinates.Add(new Point(u, v));
                normals.Add(new Vector3D(0, 0, 1));

                wingDistance[index] = p.X - HingeX;
                var span = p.X < HingeX ? ImageWidth * .5 + HingeX : ImageWidth * .5 - HingeX;
                var distance = Math.Clamp(Math.Abs(wingDistance[index]) / span, 0, 1);
                var softness = distance * distance * (3 - 2 * distance);
                // The membrane bends progressively toward its tips; the hinge stays
                // in place and the original texture is never replaced or crossfaded.
                bendWeight[index] = .35 + .65 * softness;
                verticalFold[index] = p.Y * (.025 + .045 * softness);
                flutter[index] = height * .018 * softness * Math.Sin(Math.PI * ny);
            }
        }

        var triangles = new Int32Collection(HorizontalSegments * VerticalSegments * 6);
        for (var row = 0; row < VerticalSegments; row++)
            for (var column = 0; column < HorizontalSegments; column++)
            {
                var a = row * Columns + column;
                triangles.Add(a); triangles.Add(a + Columns); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + Columns); triangles.Add(a + Columns + 1);
            }

        RestPositions = Frozen(new Point3DCollection(rest));
        TextureCoordinates = Frozen(coordinates);
        Normals = Frozen(normals);
        Triangles = Frozen(triangles);
    }

    internal Point3DCollection CreatePose(double phase, out bool isFinite)
    {
        isFinite = true;
        if (phase == 0) return RestPositions;
        var angle = phase * Math.Tau;
        // Two unhurried wingbeats per eight-second flight loop.
        var fold = (1 - Math.Cos(angle * 2)) * .5;
        var wave = Math.Sin(angle * 2);
        for (var i = 0; i < rest.Length; i++)
        {
            var p = rest[i];
            var bend = .92 * fold * bendWeight[i];
            p.X = HingeX + wingDistance[i] * Math.Cos(bend);
            p.Y += -verticalFold[i] * fold + flutter[i] * wave;
            p.Z = Math.Abs(wingDistance[i]) * Math.Sin(bend);
            working[i] = p;
            isFinite &= double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z);
        }
        // One frozen collection assignment avoids thousands of mesh change events.
        return Frozen(new Point3DCollection(working));
    }

    private static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }
}
