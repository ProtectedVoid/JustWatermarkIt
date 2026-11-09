using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Watermarker.Library.Exceptions;

namespace Watermarker.Library.Tests;

// Chaque test travaille dans un dossier temporaire neuf, supprimé à la fin.
// Les photos sont générées en mémoire puis écrites sur disque : rien à préparer à la main.
[TestClass]
public class BatchTests
{
    private static readonly Rgba32 White = new(255, 255, 255, 255);

    private string _root = null!;
    private string Src => Path.Combine(_root, "src");
    private string Dst => Path.Combine(_root, "dst");
    private string Orig => Path.Combine(_root, "orig");
    private string WatermarkFile => Path.Combine(_root, "wm.png");

    // ⚠ SEUL POINT À ADAPTER : le nom de la classe qui contient RunAsync.
    private static Task<IReadOnlyList<FileResult>> Run(
        BatchRequest request, IProgress<BatchProgress>? progress = null, CancellationToken token = default)
        => BatchProcessor.RunAsync(request, progress ?? new SyncProgress(), token);

    // IProgress synchrone : Progress<T> poste ses rappels sur un autre contexte,
    // ils pourraient arriver APRÈS la fin de RunAsync et rendre les tests aléatoires.
    // (Progress<T> reste le bon choix dans l'interface, où il ramène sur le fil de l'UI.)
    private sealed class SyncProgress : IProgress<BatchProgress>
    {
        public List<BatchProgress> Items { get; } = [];
        public void Report(BatchProgress value) => Items.Add(value);
    }

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "watermarker-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Src);

        using var watermark = new Image<Rgba32>(10, 10, new Rgba32(0, 0, 0, 255));
        watermark.SaveAsPng(WatermarkFile);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    // ------------------------------------------------------------------
    // Outils
    // ------------------------------------------------------------------
    private void CreatePhoto(string name, int width = 100, int height = 100, Action<Image<Rgba32>>? tweak = null)
    {
        using var image = new Image<Rgba32>(width, height, White);
        tweak?.Invoke(image);
        var path = Path.Combine(Src, name);
        if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) image.SaveAsPng(path);
        else image.SaveAsJpeg(path);
    }

    private BatchRequest Request(
        bool rename = false, string? originals = null,
        string? source = null, string? destination = null, float opacity = 0.3f)
        => new(source ?? Src, WatermarkFile, new WatermarkOptions(opacity, PlacementMode.Corner),
               destination ?? Dst, rename, originals);

    private static int CountFiles(string folder)
        => Directory.Exists(folder) ? Directory.GetFiles(folder).Length : 0;

    private static string Describe(IEnumerable<FileResult> results)
        => string.Join(" | ", results.Select(r => $"{Path.GetFileName(r.SourceFile)}: {r.Status} ({r.Details})"));

    private static FileResult Of(IEnumerable<FileResult> results, string fileName)
        => results.Single(r => Path.GetFileName(r.SourceFile) == fileName);

    private static void AssertNear(double expected, int actual, double tolerance = 1.0)
        => Assert.IsLessThanOrEqualTo(tolerance, Math.Abs(expected - actual), $"attendu ≈ {expected}, obtenu {actual}");

    // ------------------------------------------------------------------
    // 1. Le cas nominal
    // ------------------------------------------------------------------
    [TestMethod]
    public async Task Traite_les_images_et_ignore_les_autres_fichiers()
    {
        CreatePhoto("a.jpg");
        CreatePhoto("PHOTO.JPG"); // extension en majuscules, comme sur la plupart des appareils
        CreatePhoto("c.png");
        File.WriteAllText(Path.Combine(Src, "notes.txt"), "pas une image");

        var results = await Run(Request(), token: TestContext.CancellationToken);

        Assert.HasCount(3, results, Describe(results));
        Assert.IsTrue(results.All(r => r.Status == FileStatus.Success), Describe(results));
        Assert.IsTrue(File.Exists(Path.Combine(Dst, "a.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(Dst, "PHOTO.JPG")));
        Assert.IsTrue(File.Exists(Path.Combine(Dst, "c.png")));
        Assert.IsFalse(File.Exists(Path.Combine(Dst, "notes.txt")));
        Assert.AreEqual(3, CountFiles(Dst), "Aucun fichier provisoire ne doit rester dans la destination.");
    }

    [TestMethod]
    public async Task La_sortie_a_la_meme_taille_et_porte_le_watermark()
    {
        // Mêmes valeurs que les tests de ApplyWatermark : photo 100×100, watermark noir 10×10,
        // mode Coin, opacité 0,3 → rectangle (5, 80, 15, 15), pixel = 0,7 × 255 = 178,5.
        // PNG : sans perte, donc comparaison exacte à 2 près.
        CreatePhoto("a.png");

        await Run(Request(), token: TestContext.CancellationToken);

        using var output = Image.Load<Rgba32>(Path.Combine(Dst, "a.png"));
        Assert.AreEqual(100, output.Width);
        Assert.AreEqual(100, output.Height);
        AssertNear(178.5, output[10, 85].R, 2);
        Assert.AreEqual(255, output[50, 50].R);
    }

    [TestMethod]
    public async Task Les_metadonnees_sont_conservees()
    {
        CreatePhoto("exif.jpg", tweak: image =>
        {
            image.Metadata.ExifProfile = new ExifProfile();
            image.Metadata.ExifProfile.SetValue(ExifTag.Make, "TestCam");
        });

        await Run(Request(), token: TestContext.CancellationToken);

        using var output = Image.Load(Path.Combine(Dst, "exif.jpg"));
        Assert.IsNotNull(output.Metadata.ExifProfile, "Profil EXIF perdu.");
        var make = output.Metadata.ExifProfile!.GetValue(ExifTag.Make);
        Assert.IsNotNull(make, "Tag make perdu");
        Assert.AreEqual("TestCam", make!.Value);
    }

    // ------------------------------------------------------------------
    // 2. Renommage et déplacement des originaux
    // ------------------------------------------------------------------
    [TestMethod]
    public async Task Renomme_les_sorties_avec_le_prefixe()
    {
        CreatePhoto("a.jpg");

        var results = await Run(Request(rename: true), token: TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(Path.Combine(Dst, "w_a.jpg")));
        Assert.IsFalse(File.Exists(Path.Combine(Dst, "a.jpg")));
        Assert.AreEqual("w_a.jpg", Path.GetFileName(results.Single().OutputFile));
    }

    [TestMethod]
    public async Task Sans_dossier_d_originaux_la_source_reste_en_place()
    {
        CreatePhoto("a.jpg");

        await Run(Request(), token: TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(Path.Combine(Src, "a.jpg")));
    }

    [TestMethod]
    public async Task Deplace_les_originaux_apres_un_traitement_reussi()
    {
        CreatePhoto("a.jpg");

        var results = await Run(Request(originals: Orig), token: TestContext.CancellationToken);

        Assert.AreEqual(FileStatus.Success, results.Single().Status, Describe(results));
        Assert.IsFalse(File.Exists(Path.Combine(Src, "a.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(Orig, "a.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(Dst, "a.jpg")));
    }

    [TestMethod]
    public async Task Original_non_deplace_si_le_nom_est_deja_pris_mais_la_sortie_est_creee()
    {
        CreatePhoto("a.jpg");
        Directory.CreateDirectory(Orig);
        File.WriteAllText(Path.Combine(Orig, "a.jpg"), "marqueur");

        var results = await Run(Request(originals: Orig), token: TestContext.CancellationToken);

        var result = results.Single();
        Assert.AreEqual(FileStatus.Warning, result.Status, "Traité, mais original non déplacé : avertissement.");
        Assert.AreEqual(FileMessageCode.SourceAlreadyExists, result.Result, "Le code de sortie doit indoquer le cas spécifique 'non déplacé car existe déjà'");
        Assert.IsTrue(File.Exists(Path.Combine(Src, "a.jpg")));
        Assert.AreEqual("marqueur", File.ReadAllText(Path.Combine(Orig, "a.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(Dst, "a.jpg")));
    }

    // ------------------------------------------------------------------
    // 3. Tes décisions : conflit de nom, photo en erreur
    // ------------------------------------------------------------------
    [TestMethod]
    public async Task Conflit_de_nom_en_destination_ignore_le_fichier_et_ne_touche_a_rien()
    {
        CreatePhoto("a.jpg");
        Directory.CreateDirectory(Dst);
        File.WriteAllText(Path.Combine(Dst, "a.jpg"), "marqueur");

        var results = await Run(Request(originals: Orig), token: TestContext.CancellationToken);

        Assert.AreEqual(FileStatus.Skipped, results.Single().Status, Describe(results));
        Assert.AreEqual("marqueur", File.ReadAllText(Path.Combine(Dst, "a.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(Src, "a.jpg")), "L'original ne doit pas bouger.");
        Assert.IsFalse(File.Exists(Path.Combine(Orig, "a.jpg")));
    }

    [TestMethod]
    public async Task Un_fichier_corrompu_est_en_erreur_sans_bloquer_les_autres()
    {
        CreatePhoto("good.jpg");
        File.WriteAllBytes(Path.Combine(Src, "bad.jpg"), [1, 2, 3, 4, 5]);

        var results = await Run(Request(originals: Orig), token: TestContext.CancellationToken);

        var bad = Of(results, "bad.jpg");
        Assert.AreEqual(FileStatus.Failed, bad.Status);
        Assert.IsFalse(string.IsNullOrWhiteSpace(bad.Details));
        Assert.AreEqual(FileStatus.Success, Of(results, "good.jpg").Status);

        Assert.IsTrue(File.Exists(Path.Combine(Dst, "good.jpg")));
        Assert.IsFalse(File.Exists(Path.Combine(Dst, "bad.jpg")), "Aucune sortie pour un fichier en erreur.");
        Assert.AreEqual(1, CountFiles(Dst), "Seule la bonne photo doit se trouver dans la destination (pas de reste provisoire).");
        Assert.IsTrue(File.Exists(Path.Combine(Src, "bad.jpg")), "L'original en erreur reste en place.");
        Assert.IsFalse(File.Exists(Path.Combine(Orig, "bad.jpg")));
    }

    [TestMethod]
    public async Task Relancer_le_meme_lot_ignore_tout_et_ne_modifie_rien()
    {
        CreatePhoto("a.jpg");
        await Run(Request(), token: TestContext.CancellationToken);
        var before = File.ReadAllBytes(Path.Combine(Dst, "a.jpg"));

        var second = await Run(Request(), token: TestContext.CancellationToken);

        Assert.IsTrue(second.All(r => r.Status == FileStatus.Skipped), Describe(second));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(Dst, "a.jpg")));
    }

    // ------------------------------------------------------------------
    // 4. Progression (attendu ROUGE tant que les fichiers ignorés ne sont pas comptés)
    // ------------------------------------------------------------------
    [TestMethod]
    public async Task La_progression_atteint_le_total_meme_quand_un_fichier_est_ignore()
    {
        CreatePhoto("a.jpg");
        CreatePhoto("b.jpg");
        CreatePhoto("c.jpg");
        Directory.CreateDirectory(Dst);
        File.WriteAllText(Path.Combine(Dst, "b.jpg"), "déjà là"); // b sera ignoré
        var progress = new SyncProgress();

        await Run(Request(), progress, TestContext.CancellationToken);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, progress.Items.Select(p => p.Current).ToArray());
        Assert.IsTrue(progress.Items.All(p => p.Total == 3), "Le total doit rester 3 et ne compter que les images.");
    }

    // ------------------------------------------------------------------
    // 5. Contrôles avant de commencer (attendus ROUGES tant qu'ils ne sont pas faits)
    // ------------------------------------------------------------------
    [TestMethod]
    public async Task Dossier_source_inexistant_leve_DirectoryNotFoundException()
    {
        await Assert.ThrowsAsync<BatchValidationException>(
            async () => await Run(Request(source: Path.Combine(_root, "absent")), token: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Watermark_inexistant_leve_FileNotFoundException()
    {
        CreatePhoto("a.jpg");
        var request = Request() with { WatermarkFile = Path.Combine(_root, "absent.png") };

        await Assert.ThrowsAsync<BatchValidationException>(async () => await Run(request, token: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Watermark_illisible_est_refuse_avant_de_creer_quoi_que_ce_soit()
    {
        CreatePhoto("a.jpg");
        File.WriteAllBytes(WatermarkFile, [1, 2, 3, 4, 5]); // écrase le PNG valide

        await Assert.ThrowsAsync<Exception>(async () => await Run(Request(originals: Orig), token: TestContext.CancellationToken));

        Assert.IsFalse(Directory.Exists(Dst), "Le dossier de sortie ne doit pas être créé.");
        Assert.IsFalse(Directory.Exists(Orig), "Le dossier d'originaux ne doit pas être créé.");
    }

    [TestMethod]
    public async Task Sortie_identique_a_la_source_sans_renommage_est_refusee()
    {
        CreatePhoto("a.jpg");
        var before = File.ReadAllBytes(Path.Combine(Src, "a.jpg"));

        await Assert.ThrowsAsync<BatchValidationException>(
            async () => await Run(Request(rename: false, destination: Src), token: TestContext.CancellationToken));

        CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(Src, "a.jpg")));
    }

    [TestMethod]
    public async Task La_comparaison_des_dossiers_tient_compte_du_separateur_final()
    {
        CreatePhoto("a.jpg");

        // « src » et « src/ » désignent le même dossier.
        await Assert.ThrowsAsync<BatchValidationException>(
            async () => await Run(Request(rename: false, destination: Src + Path.DirectorySeparatorChar), token: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Dossier_des_originaux_identique_a_la_source_est_refuse()
    {
        CreatePhoto("a.jpg");

        await Assert.ThrowsAsync<BatchValidationException>(
            async () => await Run(Request(originals: Src), token: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Dossier_des_originaux_identique_a_la_destination_est_refuse()
    {
        CreatePhoto("a.jpg");

        await Assert.ThrowsAsync<BatchValidationException>(
            async () => await Run(Request(originals: Dst), token: TestContext.CancellationToken));
    }

    // Documente ta décision : la destination doit toujours différer de la source,
    // même avec le renommage (sinon un second passage retraiterait les « w_ »).
    [TestMethod]
    public async Task Sortie_dans_la_source_est_refusee_meme_avec_renommage()
    {
        CreatePhoto("a.jpg");

        await Assert.ThrowsAsync<BatchValidationException>(
            async () => await Run(Request(rename: true, destination: Src), token: TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task Opacite_invalide_est_refusee_avant_de_toucher_aux_fichiers()
    {
        CreatePhoto("a.jpg");

        await Assert.ThrowsAsync<BatchValidationException>(
            async () => await Run(Request(opacity: 2f), token: TestContext.CancellationToken));

        Assert.AreEqual(0, CountFiles(Dst));
        Assert.IsTrue(File.Exists(Path.Combine(Src, "a.jpg")));
    }

    // ------------------------------------------------------------------
    // 6. Annulation
    // ------------------------------------------------------------------
    // Vrai quelle que soit ta décision (exception OU rapport partiel).
    [TestMethod]
    public async Task Annulation_avant_le_depart_ne_touche_a_aucun_fichier()
    {
        CreatePhoto("a.jpg");
        CreatePhoto("b.jpg");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            await Run(Request(originals: Orig), token: cts.Token);
        }
        catch (OperationCanceledException)
        {
            // comportement accepté
        }

        Assert.AreEqual(0, CountFiles(Dst));
        Assert.AreEqual(0, CountFiles(Orig));
        Assert.AreEqual(2, CountFiles(Src));
    }

    // Rapport partiel (décision adoptée) : les fichiers non traités sont rendus
    // « Skipped » avec un message d'annulation.
    [TestMethod]
    public async Task Annulation_renvoie_un_rapport_partiel()
    {
        CreatePhoto("a.jpg");
        CreatePhoto("b.jpg");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var results = await Run(Request(), token: cts.Token);

        Assert.HasCount(2, results, Describe(results));
        Assert.IsTrue(results.All(r => r.Status == FileStatus.Skipped), Describe(results));
    }

    public TestContext TestContext { get; set; }
}