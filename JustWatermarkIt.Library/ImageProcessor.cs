using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Text;

namespace JustWatermarkIt.Library
{
    public sealed record WatermarkOptions(float Opacity = 0.3f, PlacementMode Mode = PlacementMode.Wide);

    public static class ImageProcessor
    {
        /// <summary>
        /// Renvoie un clone de l'image source apposée du watermark
        /// </summary>
        /// <param name="source">L'image source</param>
        /// <param name="watermark">Le watermark à poser</param>
        /// <param name="options">Les options d'application du watermark</param>
        /// <returns>Une image, copie de la source, avec le watermark dessus</returns>
        /// <exception cref="ArgumentOutOfRangeException">L'opacité n'est pas entre 0 et 1</exception>
        public static Image ApplyWatermark(this Image source, Image watermark, WatermarkOptions options)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(watermark);
            ArgumentNullException.ThrowIfNull(options);

            if (options.Opacity < 0 || options.Opacity > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            var result = source.Clone(a => a.AutoOrient());
            result.Metadata.ExifProfile = source.Metadata.ExifProfile?.DeepClone();
            if(result.Metadata.ExifProfile is ExifProfile profile
                && profile.GetValue(ExifTag.Orientation) != null)
            {
                profile.RemoveValue(ExifTag.Orientation);
            }

            try
            {
                var watermarkBounds = PlacementCalculator.Compute(result.Size(), watermark.Size(), options.Mode);
                using var resizedWatermark = watermark.Clone(w => w.Resize(watermarkBounds.Width, watermarkBounds.Height));

                result.Mutate(a =>
                {
                    a.DrawImage(resizedWatermark, new Point(watermarkBounds.X, watermarkBounds.Y), options.Opacity);
                });

                return result;
            }
            catch (Exception)
            {
                result.Dispose();
                throw;
            }
        }
    }
}
