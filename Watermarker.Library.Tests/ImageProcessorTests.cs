using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Watermarker.Library.Tests;

// Hypothèse : static Image ApplyWaterMark(this Image source, Image watermark, WatermarkOptions options)
// avec WatermarkOptions(float Opacity = 0.3f, PlacementMode Mode = PlacementMode.Wide)
[TestClass]
public class ImageProcessorTests
{
    private static readonly Rgba32 White = new(255, 255, 255, 255);
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    private static Image<Rgba32> Solid(int width, int height, Rgba32 color)
        => new(width, height, color);

    private static int Red(Image<Rgba32> image, int x, int y) => image[x, y].R;

    private static void AssertNear(double expected, int actual, double tolerance = 1.0)
        => Assert.IsLessThanOrEqualTo(tolerance, Math.Abs(expected - actual), $"attendu ≈ {expected}, obtenu {actual}");

    // ------------------------------------------------------------------
    // 1. Le mélange, calculé à la main
    //    Photo blanche 100×100, watermark noir opaque 10×10, mode Coin :
    //    x = min(100/20, 100/20) = 5 ; boîte 15×15 ; y = 100 − 5 − 15 = 80
    //    → rectangle (5, 80, 15, 15)
    //    Opacité 0,3 : 0,3 × 0 + 0,7 × 255 = 178,5
    // ------------------------------------------------------------------
    [TestMethod]
    public void Melange_calcule_a_la_main_en_mode_Coin()
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, Black);

        photo.ApplyWatermark(watermark, new WatermarkOptions(0.3f, PlacementMode.Corner));

        // Dans le rectangle (coins inclus)
        AssertNear(178.5, Red(photo, 5, 80));
        AssertNear(178.5, Red(photo, 19, 94));
        AssertNear(178.5, Red(photo, 10, 85));

        // Juste à l'extérieur : inchangé
        Assert.AreEqual(255, Red(photo, 4, 80));
        Assert.AreEqual(255, Red(photo, 5, 79));
        Assert.AreEqual(255, Red(photo, 20, 94));
        Assert.AreEqual(255, Red(photo, 5, 95));
        Assert.AreEqual(255, Red(photo, 50, 50));
    }

    [TestMethod]
    public void Opacite_1_donne_le_noir_et_opacite_0_ne_change_rien()
    {
        using var watermark = Solid(10, 10, Black);

        using var opaque = Solid(100, 100, White);
        opaque.ApplyWatermark(watermark, new WatermarkOptions(1f, PlacementMode.Corner));
        Assert.AreEqual(0, Red(opaque, 10, 85));

        using var invisible = Solid(100, 100, White);
        invisible.ApplyWatermark(watermark, new WatermarkOptions(0f, PlacementMode.Corner));
        Assert.AreEqual(255, Red(invisible, 10, 85));
    }

    // ------------------------------------------------------------------
    // 2. Combinaison avec la transparence propre du watermark
    //    Attendu : alpha du PNG × opacité du slider.
    //    alpha 128/255 ≈ 0,502 ; × 0,5 ≈ 0,251 ; 255 × (1 − 0,251) ≈ 191
    // ------------------------------------------------------------------
    [TestMethod]
    public void L_opacite_se_combine_avec_l_alpha_du_watermark()
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, new Rgba32(0, 0, 0, 128));

        photo.ApplyWatermark(watermark, new WatermarkOptions(0.5f, PlacementMode.Corner));

        AssertNear(191, Red(photo, 10, 85), tolerance: 2);
    }

    [TestMethod]
    public void Un_watermark_entierement_transparent_ne_change_rien()
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, new Rgba32(0, 0, 0, 0));

        photo.ApplyWatermark(watermark, new WatermarkOptions(1f, PlacementMode.Corner));

        Assert.AreEqual(255, Red(photo, 10, 85));
    }

    // ------------------------------------------------------------------
    // 3. Le watermark partagé n'est ni modifié ni libéré
    // ------------------------------------------------------------------
    [TestMethod]
    public void Le_watermark_n_est_pas_modifie()
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, Black);

        photo.ApplyWatermark(watermark, new WatermarkOptions(0.3f, PlacementMode.Corner));

        // Si le watermark avait été redimensionné ou libéré, ceci échouerait.
        Assert.AreEqual(10, watermark.Width);
        Assert.AreEqual(10, watermark.Height);
        Assert.AreEqual(0, Red(watermark, 5, 5));
    }

    // ------------------------------------------------------------------
    // 4. Orientation : la photo est remise à l'endroit AVANT le placement
    //    Photo stockée 100 × 60 avec la balise EXIF 6 (« afficher pivotée de 90° »).
    //    Après remise à l'endroit : 60 × 100.
    //    Mode Large, watermark carré 10×10 → rectangle attendu (0, 20, 60, 60).
    //    Si le placement est calculé sur la taille d'avant rotation (100 × 60),
    //    le rectangle serait (20, 0, 60, 60) et les pixels ci-dessous seraient inversés.
    // ------------------------------------------------------------------
    [TestMethod]
    public void La_photo_est_remise_a_l_endroit_avant_le_placement()
    {
        using var photo = Solid(100, 60, White);
        photo.Metadata.ExifProfile = new ExifProfile();
        photo.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        using var watermark = Solid(10, 10, Black);

        photo.ApplyWatermark(watermark, new WatermarkOptions(1f, PlacementMode.Wide));

        Assert.AreEqual(60, photo.Width);
        Assert.AreEqual(100, photo.Height);

        Assert.AreEqual(0, Red(photo, 5, 50));     // bande centrale, côté gauche
        Assert.AreEqual(0, Red(photo, 55, 50));    // bande centrale, côté droit
        Assert.AreEqual(255, Red(photo, 30, 5));   // bande haute : inchangée
        Assert.AreEqual(255, Red(photo, 30, 95));  // bande basse : inchangée
    }

    [TestMethod]
    public void La_balise_d_orientation_est_reinitialisee()
    {
        using var photo = Solid(100, 60, White);
        photo.Metadata.ExifProfile = new ExifProfile();
        photo.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        using var watermark = Solid(10, 10, Black);

        photo.ApplyWatermark(watermark, new WatermarkOptions(1f, PlacementMode.Wide));

        // Sinon la photo serait pivotée une seconde fois à l'affichage.
        var exif = photo.Metadata.ExifProfile;
        var orientation = exif.GetValue(ExifTag.Orientation);
        bool reset = exif is null
            || orientation is null
            || orientation!.Value == 1;
        Assert.IsTrue(reset, "La balise d'orientation doit valoir 1 (ou être absente).");
    }

    // ------------------------------------------------------------------
    // 5. Métadonnées : conservées, y compris après un aller-retour JPEG
    //    (ce test vérifie surtout le comportement d'ImageSharp)
    // ------------------------------------------------------------------
    [TestMethod]
    public void Les_metadonnees_survivent_a_l_application_et_a_un_aller_retour_JPEG()
    {
        using var photo = Solid(100, 100, White);
        photo.Metadata.ExifProfile = new ExifProfile();
        photo.Metadata.ExifProfile.SetValue(ExifTag.Make, "TestCam");
        using var watermark = Solid(10, 10, Black);

        photo.ApplyWatermark(watermark, new WatermarkOptions(0.3f, PlacementMode.Corner));

        using var stream = new MemoryStream();
        photo.SaveAsJpeg(stream);
        stream.Position = 0;
        using var reloaded = Image.Load(stream);

        Assert.IsNotNull(reloaded.Metadata.ExifProfile, "Profil EXIF perdu.");
        var make = reloaded.Metadata.ExifProfile.GetValue(ExifTag.Make);
        Assert.IsNotNull(make, "Tag Make perdu.");
        Assert.AreEqual("TestCam", make!.Value);
    }

    // ------------------------------------------------------------------
    // 6. Validation (attendu ROUGE sur NaN et sur les null avec la version actuelle)
    // ------------------------------------------------------------------
    [TestMethod]
    [DataRow(-0.1f)]
    [DataRow(1.1f)]
    [DataRow(float.NaN)]
    public void Une_opacite_invalide_est_refusee(float opacity)
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, Black);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => { photo.ApplyWatermark(watermark, new WatermarkOptions(opacity)); });
    }

    [TestMethod]
    public void Des_arguments_null_levent_ArgumentNullException()
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, Black);
        var options = new WatermarkOptions();

        Assert.Throws<ArgumentNullException>(
            () => { ((Image)null!).ApplyWatermark(watermark, options); });
        Assert.Throws<ArgumentNullException>(
            () => { photo.ApplyWatermark(null!, options); });
        Assert.Throws<ArgumentNullException>(
            () => { photo.ApplyWatermark(watermark, null!); });
    }
}