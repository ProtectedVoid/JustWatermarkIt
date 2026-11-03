
using SixLabors.ImageSharp;

namespace Watermarker.Library.Tests
{
    [TestClass]
    public sealed class FitInTests
    {
        // ⚠ SEUL POINT À ADAPTER : le nom de la classe qui contient Compute
        // (et le type Size si ce n'est pas celui d'ImageSharp).
        // Hypothèse : Placement Compute(Size photo, Size watermark, PlacementMode mode)
        private static Placement Run(int photoW, int photoH, int wmW, int wmH, PlacementMode mode)
            => PlacementCalculator.Compute(new Size(photoW, photoH), new Size(wmW, wmH), mode);

        // ------------------------------------------------------------------
        // 1. Valeurs calculées à la main
        // ------------------------------------------------------------------
        //          photo W, H   wm W, H    mode                  X     Y     W     H
        // --- Large (Wide) : ajusté à la photo, centré ---
        [TestMethod]
        [DataRow(4000, 3000, 1000, 1000, PlacementMode.Wide, 500, 0, 3000, 3000)]
        [DataRow(4000, 3000, 2000, 500, PlacementMode.Wide, 0, 1000, 4000, 1000)]
        [DataRow(4000, 3000, 500, 1000, PlacementMode.Wide, 1250, 0, 1500, 3000)]
        [DataRow(4000, 3000, 4000, 3000, PlacementMode.Wide, 0, 0, 4000, 3000)] // identité
        [DataRow(3000, 4000, 1000, 1000, PlacementMode.Wide, 0, 500, 3000, 3000)] // portrait
                                                                                  // --- Coin : marge = 5 % du petit côté, grand côté du watermark = 3 × marge ---
        [DataRow(4000, 3000, 400, 200, PlacementMode.Corner, 150, 2625, 450, 225)]
        [DataRow(4000, 3000, 1000, 100, PlacementMode.Corner, 150, 2805, 450, 45)]
        [DataRow(4000, 3000, 100, 1000, PlacementMode.Corner, 150, 2400, 45, 450)]
        [DataRow(4000, 3000, 300, 300, PlacementMode.Corner, 150, 2400, 450, 450)]
        [DataRow(3000, 4000, 400, 200, PlacementMode.Corner, 150, 3625, 450, 225)] // portrait
        public void Valeurs_calculees_a_la_main(
            int photoW, int photoH, int wmW, int wmH, PlacementMode mode,
            int x, int y, int w, int h)
        {
            Assert.AreEqual(new Placement(x, y, w, h), Run(photoW, photoH, wmW, wmH, mode));
        }

        // ------------------------------------------------------------------
        // 2. Invariants : vrais pour TOUTES les tailles, y compris extrêmes
        // ------------------------------------------------------------------
        public static IEnumerable<object[]> ExtremeGrid()
        {
            int[][] photos =
            [
            [4000, 3000], [3000, 4000], [1920, 1080],
            [1080, 1920], [100, 100],   [19, 19],
        ];
            int[][] watermarks =
            [
            [400, 200],  [200, 400],  [1000, 100],
            [100, 1000], [1, 1000],   [1000, 1],
            [5000, 5000], [3, 3],
        ];

            foreach (var mode in new[] { PlacementMode.Corner, PlacementMode.Wide })
                foreach (var p in photos)
                    foreach (var w in watermarks)
                        yield return new object[] { p[0], p[1], w[0], w[1], mode };
        }

        [TestMethod]
        [DynamicData(nameof(ExtremeGrid))]
        public void Le_rectangle_est_toujours_dans_la_photo(
            int photoW, int photoH, int wmW, int wmH, PlacementMode mode)
        {
            var r = Run(photoW, photoH, wmW, wmH, mode);

            Assert.IsGreaterThanOrEqualTo(0, r.X, $"X négatif : {r}");
            Assert.IsGreaterThanOrEqualTo(0, r.Y, $"Y négatif : {r}");
            Assert.IsLessThanOrEqualTo(photoW, r.X + r.Width, $"Déborde à droite : {r}");
            Assert.IsLessThanOrEqualTo(photoH, r.Y + r.Height, $"Déborde en bas : {r}");
        }

        // Attendu ROUGE tant que tu n'as pas décidé d'un minimum de 1 px :
        // un rectangle de taille 0 ferait échouer le redimensionnement du watermark.
        [TestMethod]
        [DynamicData(nameof(ExtremeGrid))]
        public void La_taille_n_est_jamais_nulle(
            int photoW, int photoH, int wmW, int wmH, PlacementMode mode)
        {
            var r = Run(photoW, photoH, wmW, wmH, mode);

            Assert.IsGreaterThanOrEqualTo(1, r.Width, $"Largeur nulle : {r}");
            Assert.IsGreaterThanOrEqualTo(1, r.Height, $"Hauteur nulle : {r}");
        }

        // ------------------------------------------------------------------
        // 3. Le ratio du watermark est conservé (cas non extrêmes)
        //    Tolérance : l'arrondi entier fait perdre moins de 1 px sur la
        //    dimension dérivée, d'où la borne sur le produit en croix.
        // ------------------------------------------------------------------
        public static IEnumerable<object[]> ModerateGrid()
        {
            int[][] photos = [[4000, 3000], [3000, 4000], [1920, 1080]];
            int[][] watermarks =
            [
            [400, 200], [200, 400], [1000, 1000],
            [2000, 500], [500, 1000],
        ];

            foreach (var mode in new[] { PlacementMode.Corner, PlacementMode.Wide })
                foreach (var p in photos)
                    foreach (var w in watermarks)
                        yield return new object[] { p[0], p[1], w[0], w[1], mode };
        }

        [TestMethod]
        [DynamicData(nameof(ModerateGrid))]
        public void Le_ratio_du_watermark_est_conserve(
            int photoW, int photoH, int wmW, int wmH, PlacementMode mode)
        {
            var r = Run(photoW, photoH, wmW, wmH, mode);

            long ecart = Math.Abs((long)r.Width * wmH - (long)r.Height * wmW);
            Assert.IsLessThanOrEqualTo(Math.Max(wmW, wmH), ecart, $"Ratio déformé : {r}");
        }

        // ------------------------------------------------------------------
        // 4. Entrées invalides (attendu ROUGE jusqu'à ajout de la validation)
        // ------------------------------------------------------------------
        [TestMethod]
        [DataRow(0, 100, 10, 10)]
        [DataRow(100, 0, 10, 10)]
        [DataRow(-1, 100, 10, 10)]
        [DataRow(100, 100, 0, 10)]
        [DataRow(100, 100, 10, 0)]
        [DataRow(100, 100, 10, -5)]
        public void Les_tailles_invalides_levent_une_ArgumentException(
            int photoW, int photoH, int wmW, int wmH)
        {
            // Throws<T> accepte aussi les types dérivés (ex. ArgumentOutOfRangeException).
            Assert.Throws<ArgumentException>(
                () => { Run(photoW, photoH, wmW, wmH, PlacementMode.Corner); });
        }

        // Suppose que le paramètre de Compute s'appelle "mode".
        [TestMethod]
        public void Un_mode_inconnu_leve_une_exception_nommant_le_parametre()
        {
            var ex = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => { Run(4000, 3000, 400, 200, (PlacementMode)99); });

            Assert.AreEqual("mode", ex.ParamName);
        }

        // ------------------------------------------------------------------
        // 5. Bonus : très grandes images (débordement des int)
        //    60000 × 40000 = 2,4 milliards > int.MaxValue (≈ 2,147 milliards)
        // ------------------------------------------------------------------
        [TestMethod]
        public void Pas_de_debordement_entier_sur_tres_grandes_images()
        {
            var r = Run(60000, 40000, 60000, 1, PlacementMode.Wide);

            Assert.AreEqual(new Placement(0, 19999, 60000, 1), r);
        }
    }
}
