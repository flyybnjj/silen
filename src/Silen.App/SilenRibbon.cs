using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Silen.App;

/// <summary>
/// The Bloom AI presence, preserving its original artwork on a softly flapping
/// native WPF mesh. The host owns timing, visibility and presentation effects.
/// </summary>
public sealed class SilenRibbon : Grid
{
    private readonly RibbonGeometry geometry;
    private readonly MeshGeometry3D mesh;
    private readonly AxisAngleRotation3D yaw = new(new Vector3D(0, 1, 0), 0);
    private readonly AxisAngleRotation3D pitch = new(new Vector3D(1, 0, 0), 0);
    private readonly AxisAngleRotation3D roll = new(new Vector3D(0, 0, 1), 0);
    private readonly TranslateTransform3D hover = new();
    private double lastPhase = double.NaN;

    public int VertexCount => geometry.VertexCount;
    public int TriangleCount => geometry.Triangles.Count / 3;
    public bool IsFrameFinite { get; private set; } = true;

    public SilenRibbon()
    {
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = false;
        SnapsToDevicePixels = false;
        UseLayoutRounding = false;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri("pack://application:,,,/Silen;component/Assets/Brand/silen-bloom.png", UriKind.Absolute);
        image.EndInit();
        image.Freeze();

        geometry = new RibbonGeometry((double)image.PixelWidth / image.PixelHeight);
        mesh = new MeshGeometry3D
        {
            Positions = geometry.RestPositions,
            TriangleIndices = geometry.Triangles,
            TextureCoordinates = geometry.TextureCoordinates,
            Normals = geometry.Normals
        };
        var texture = new ImageBrush(image)
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.None,
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewport = new Rect(0, 0, 1, 1)
        };
        texture.Freeze();
        // White ambient illumination preserves the texture's RGB and alpha.
        // Emission adds to the paper behind translucent pixels and washes it out.
        var material = new DiffuseMaterial(texture);
        material.Freeze();
        var transforms = new Transform3DGroup();
        transforms.Children.Add(new RotateTransform3D(yaw));
        transforms.Children.Add(new RotateTransform3D(pitch));
        transforms.Children.Add(new RotateTransform3D(roll));
        transforms.Children.Add(hover);
        var model = new GeometryModel3D(mesh, material) { BackMaterial = material, Transform = transforms };
        var ambient = new AmbientLight(Colors.White);
        ambient.Freeze();
        var scene = new Model3DGroup();
        scene.Children.Add(ambient);
        scene.Children.Add(model);
        var camera = new OrthographicCamera(new Point3D(0, 0, 8), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), RibbonGeometry.CameraWidth)
        { NearPlaneDistance = .1, FarPlaneDistance = 20 };
        camera.Freeze();
        var viewport = new Viewport3D
        {
            IsHitTestVisible = false,
            Focusable = false,
            ClipToBounds = false,
            Camera = camera
        };
        RenderOptions.SetBitmapScalingMode(viewport, BitmapScalingMode.HighQuality);
        viewport.Children.Add(new ModelVisual3D { Content = scene });
        Children.Add(viewport);
        RenderFrame(0);
    }

    /// <param name="phase">Ambient cycle in normalized turns. 0 and 1 preserve the exact resting artwork.</param>
    public void RenderFrame(double phase)
    {
        phase = double.IsFinite(phase) ? phase - Math.Floor(phase) : 0;
        if (phase == lastPhase) return;
        lastPhase = phase;
        var angle = phase * Math.Tau;
        yaw.Angle = 6 * Math.Sin(angle);
        pitch.Angle = 2 * Math.Sin(angle * 2);
        roll.Angle = 3 * Math.Sin(angle);
        hover.OffsetX = .07 * Math.Sin(angle);
        hover.OffsetY = .05 * Math.Sin(angle * 2);
        mesh.Positions = geometry.CreatePose(phase, out var finite);
        IsFrameFinite = finite;
    }
}
