using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Watermarker.Library.Tests;

// Contrat testé :
//   Image ApplyWatermark(this Image source, Image watermark, WatermarkOptions options)
//   - renvoie une COPIE de la source avec le watermark posé ;
//   - la source n'est JAMAIS modifiée : on lit les pixels sur l'image RENVOYÉE ;
//   - l'appelant libère l'image renvoyée (d'où les « using var result »).
[TestClass]
public class ImageProcessorTests
{
    private static readonly Rgba32 White = new(255, 255, 255, 255);
    private static readonly Rgba32 Black = new(0, 0, 0, 255);

    private static Image<Rgba32> Solid(int width, int height, Rgba32 color)
        => new(width, height, color);

    // ⚠ SEUL POINT À ADAPTER si le nom ou la signature de la méthode change.
    private static Image<Rgba32> Apply(Image<Rgba32> photo, Image watermark, float opacity, PlacementMode mode)
        => (Image<Rgba32>)photo.ApplyWatermark(watermark, new WatermarkOptions(opacity, mode));

    private static int Red(Image<Rgba32> image, int x, int y) => image[x, y].R;

    private static void AssertNear(double expected, int actual, double tolerance = 1.0)
        => Assert.IsLessThanOrEqualTo(tolerance,
Math.Abs(expected - actual), $"attendu ≈ {expected}, obtenu {actual}");

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

        using var result = Apply(photo, watermark, 0.3f, PlacementMode.Corner);

        // Dans le rectangle (coins inclus)
        AssertNear(178.5, Red(result, 5, 80));
        AssertNear(178.5, Red(result, 19, 94));
        AssertNear(178.5, Red(result, 10, 85));

        // Juste à l'extérieur : inchangé
        Assert.AreEqual(255, Red(result, 4, 80));
        Assert.AreEqual(255, Red(result, 5, 79));
        Assert.AreEqual(255, Red(result, 20, 94));
        Assert.AreEqual(255, Red(result, 5, 95));
        Assert.AreEqual(255, Red(result, 50, 50));
    }

    // Piège classique : lire les pixels de `photo` au lieu de `result`.
    // La source n'est pas modifiée, elle resterait blanche (255) et le test « 1 → noir » échouerait.
    [TestMethod]
    public void Opacite_1_donne_le_noir_et_opacite_0_ne_change_rien()
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, Black);

        // Une seule photo suffit : elle n'est jamais modifiée.
        using var opaque = Apply(photo, watermark, 1f, PlacementMode.Corner);
        using var invisible = Apply(photo, watermark, 0f, PlacementMode.Corner);

        Assert.AreEqual(0, Red(opaque, 10, 85), "Opacité 1 : le pixel devrait être noir.");
        Assert.AreEqual(255, Red(invisible, 10, 85), "Opacité 0 : le pixel devrait rester blanc.");
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

        using var result = Apply(photo, watermark, 0.5f, PlacementMode.Corner);

        AssertNear(191, Red(result, 10, 85), tolerance: 2);
    }

    [TestMethod]
    public void Un_watermark_entierement_transparent_ne_change_rien()
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, new Rgba32(0, 0, 0, 0));

        using var result = Apply(photo, watermark, 1f, PlacementMode.Corner);

        Assert.AreEqual(255, Red(result, 10, 85));
    }

    // ------------------------------------------------------------------
    // 3. Ce qui ne doit JAMAIS être modifié : le watermark partagé et la source
    // ------------------------------------------------------------------
    [TestMethod]
    public void Le_watermark_n_est_pas_modifie()
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, Black);

        using var result = Apply(photo, watermark, 0.3f, PlacementMode.Corner);

        // Si le watermark avait été redimensionné ou libéré, ceci échouerait.
        Assert.AreEqual(10, watermark.Width);
        Assert.AreEqual(10, watermark.Height);
        Assert.AreEqual(0, Red(watermark, 5, 5));
    }

    [TestMethod]
    public void La_source_n_est_pas_modifiee()
    {
        using var photo = Solid(100, 60, White);
        photo.Metadata.ExifProfile = new ExifProfile();
        photo.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        using var watermark = Solid(10, 10, Black);

        using var result = Apply(photo, watermark, 1f, PlacementMode.Wide);

        Assert.AreNotSame(photo, result);
        // Taille, pixels et balise d'orientation de la source : intacts.
        Assert.AreEqual(100, photo.Width);
        Assert.AreEqual(60, photo.Height);
        Assert.AreEqual(255, Red(photo, 50, 30));
        var orientation = photo.Metadata.ExifProfile!.GetValue(ExifTag.Orientation);
        Assert.IsNotNull(orientation);
        Assert.AreEqual((ushort)6, orientation!.Value);
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

        using var result = Apply(photo, watermark, 1f, PlacementMode.Wide);

        Assert.AreEqual(60, result.Width);
        Assert.AreEqual(100, result.Height);

        Assert.AreEqual(0, Red(result, 5, 50));     // bande centrale, côté gauche
        Assert.AreEqual(0, Red(result, 55, 50));    // bande centrale, côté droit
        Assert.AreEqual(255, Red(result, 30, 5));   // bande haute : inchangée
        Assert.AreEqual(255, Red(result, 30, 95));  // bande basse : inchangée
    }

    // ------------------------------------------------------------------
    // 5. Métadonnées
    //
    //    « Conservées » et « orientation réinitialisée » ne se contredisent pas :
    //    - toutes les métadonnées qui DÉCRIVENT la photo (appareil, date, GPS, réglages…)
    //      sont conservées telles quelles ;
    //    - la balise d'orientation est une INSTRUCTION D'AFFICHAGE (« pivote-moi »).
    //      Une fois les pixels pivotés par AutoOrient, la garder ferait pivoter la
    //      photo une seconde fois : on la remet donc à « normal » (1).
    // ------------------------------------------------------------------
    [TestMethod]
    public void Les_metadonnees_survivent_a_l_application_et_a_un_aller_retour_JPEG()
    {
        using var photo = Solid(100, 100, White);
        photo.Metadata.ExifProfile = new ExifProfile();
        photo.Metadata.ExifProfile.SetValue(ExifTag.Make, "TestCam");
        using var watermark = Solid(10, 10, Black);

        using var result = Apply(photo, watermark, 0.3f, PlacementMode.Corner);

        using var stream = new MemoryStream();
        result.SaveAsJpeg(stream);
        stream.Position = 0;
        using var reloaded = Image.Load(stream);

        Assert.IsNotNull(reloaded.Metadata.ExifProfile, "Profil EXIF perdu.");
        var make = reloaded.Metadata.ExifProfile!.GetValue(ExifTag.Make);
        Assert.IsNotNull(make, "Tag make perdu");
        Assert.AreEqual("TestCam", make!.Value);
    }

    [TestMethod]
    public void La_balise_d_orientation_est_reinitialisee()
    {
        using var photo = Solid(100, 60, White);
        photo.Metadata.ExifProfile = new ExifProfile();
        photo.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        using var watermark = Solid(10, 10, Black);

        using var result = Apply(photo, watermark, 1f, PlacementMode.Wide);

        var exif = result.Metadata.ExifProfile;
        bool reset = exif is null
            || exif.GetValue(ExifTag.Orientation) == null
            || exif.GetValue(ExifTag.Orientation).Value == 1;
        Assert.IsTrue(reset, "La balise d'orientation doit valoir 1 (ou être absente).");
    }

    // Le test qui montre les deux règles ensemble : une photo avec un tag « Make » ET une
    // orientation. Après traitement, Make est intact, seule l'orientation a changé.
    [TestMethod]
    public void Seule_l_orientation_change_parmi_les_metadonnees()
    {
        using var photo = Solid(100, 60, White);
        photo.Metadata.ExifProfile = new ExifProfile();
        photo.Metadata.ExifProfile.SetValue(ExifTag.Make, "TestCam");
        photo.Metadata.ExifProfile.SetValue(ExifTag.Orientation, (ushort)6);
        using var watermark = Solid(10, 10, Black);

        using var result = Apply(photo, watermark, 0.3f, PlacementMode.Corner);

        var exif = result.Metadata.ExifProfile;
        Assert.IsNotNull(exif, "Profil EXIF perdu.");
        var make = exif.GetValue(ExifTag.Make);
        Assert.IsNotNull(make, "Tag Make perdu.");
        Assert.AreEqual("TestCam", make!.Value);

        var orientation = exif.GetValue(ExifTag.Orientation);
        bool orientationReset = orientation == null
            || orientation!.Value == 1;
        Assert.IsTrue(orientationReset, "L'orientation doit être réinitialisée.");
    }

    // ------------------------------------------------------------------
    // 6. Validation
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
            () => { photo.ApplyWatermark(watermark, new WatermarkOptions(opacity)).Dispose(); });
    }

    [TestMethod]
    public void Des_arguments_null_levent_ArgumentNullException()
    {
        using var photo = Solid(100, 100, White);
        using var watermark = Solid(10, 10, Black);
        var options = new WatermarkOptions();

        Assert.Throws<ArgumentNullException>(
            () => { ((Image)null!).ApplyWatermark(watermark, options).Dispose(); });
        Assert.Throws<ArgumentNullException>(
            () => { photo.ApplyWatermark(null!, options).Dispose(); });
        Assert.Throws<ArgumentNullException>(
            () => { photo.ApplyWatermark(watermark, null!).Dispose(); });
    }
}