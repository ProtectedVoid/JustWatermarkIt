using SixLabors.ImageSharp;

namespace JustWatermarkIt.Library
{
    public record struct Placement(int X, int Y, int Width, int Height);

    public static class PlacementCalculator
    {

        public static Placement Compute(Size photo, Size watermark, PlacementMode mode)
        {
            if (!photo.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(photo));
            }
            if (!watermark.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(watermark));
            }

            int x;
            int y;
            int width;
            int height;
            Size sizeResult;

            switch (mode)
            {
                case PlacementMode.Corner:
                    x = Math.Min(photo.Width / 20, photo.Height / 20);
                    sizeResult = watermark.FitIn(new Size(3 * x, 3 * x));
                    height = sizeResult.Height;
                    width = sizeResult.Width;
                    y = photo.Height - x - height;
                    break;

                case PlacementMode.Wide:
                    sizeResult = watermark.FitIn(photo);
                    height = sizeResult.Height;
                    width = sizeResult.Width;
                    x = (photo.Width - width) / 2;
                    y = (photo.Height - height) / 2;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode));
            }
            return new Placement(x, y, width, height);
        }

        private static Size FitIn(this Size original, Size target)
        {
            long width = target.Width;
            long height = target.Width * original.Height / original.Width;
            if (height > target.Height)
            {
                height = target.Height;
                width = target.Height * original.Width / original.Height;
            }

            if (width > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }

            if (height > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(target));
            }

            return new Size(Math.Max((int)width, 1), Math.Max((int)height, 1));
        }

        private static bool IsValid(this Size toTest)
        {
            return !toTest.IsEmpty
                && toTest.Width > 0
                && toTest.Height > 0;
        }
    }
}
