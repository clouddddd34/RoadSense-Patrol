using System.Drawing.Drawing2D;

namespace RoadSensePatrol;

internal sealed class CarSkinArt : IDisposable
{
    private static readonly Rectangle[] Crops =
    [
        new(62, 58, 196, 430),
        new(450, 52, 230, 424),
        new(866, 68, 202, 414)
    ];

    // Contours follow the bodywork in the supplied top-down reference image.
    private static readonly Point[][] Contours =
    [
        [new(28, 8), new(168, 8), new(178, 42), new(184, 83), new(192, 156), new(191, 337), new(181, 402), new(165, 420), new(38, 420), new(18, 406), new(5, 344), new(4, 161), new(11, 87), new(18, 43)],
        [new(39, 6), new(192, 6), new(207, 46), new(217, 107), new(223, 171), new(219, 322), new(207, 383), new(181, 414), new(53, 414), new(22, 388), new(8, 328), new(6, 173), new(13, 108), new(23, 45)],
        [new(42, 7), new(157, 7), new(179, 42), new(191, 105), new(197, 183), new(192, 315), new(178, 386), new(158, 405), new(43, 405), new(24, 387), new(9, 318), new(7, 184), new(13, 105), new(24, 43)]
    ];

    private readonly Bitmap[] display = new Bitmap[3];
    private readonly Bitmap[] driving = new Bitmap[3];

    public CarSkinArt()
    {
        var assembly = typeof(CarSkinArt).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith("car-selection-reference.png", StringComparison.OrdinalIgnoreCase)))
            ?? throw new InvalidOperationException("Car selection art is missing.");
        using var source = new Bitmap(stream);
        for (int i = 0; i < 3; i++)
        {
            var crop = Crops[i];
            var bitmap = new Bitmap(crop.Width, crop.Height);
            using (var g = Graphics.FromImage(bitmap))
            using (var path = new GraphicsPath())
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                path.AddPolygon(Contours[i]);
                g.SetClip(path);
                g.DrawImage(source, new Rectangle(0, 0, crop.Width, crop.Height), crop, GraphicsUnit.Pixel);
            }
            display[i] = bitmap;
            driving[i] = (Bitmap)bitmap.Clone();
            // Doc's supplied art is already upright for the road view; rotating it
            // turns the roof number and body upside down in gameplay.
            if (i != 2) driving[i].RotateFlip(RotateFlipType.Rotate180FlipNone);
        }
    }

    public Image Display(int index) => display[Math.Clamp(index, 0, 2)];
    public Image Driving(int index) => driving[Math.Clamp(index, 0, 2)];

    public void Dispose()
    {
        foreach (var image in display) image?.Dispose();
        foreach (var image in driving) image?.Dispose();
    }
}
