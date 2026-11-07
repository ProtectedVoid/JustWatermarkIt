using System.IO;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Watermarker.Library;

namespace Watermarker.ViewModels.DTOs
{
    public sealed class ResultItem
    {
        public ResultItem(FileResult result)
        {
            FileName = Path.GetFileName(result.SourceFile);
            Message = result.Message ?? "";

            // Codes de la police d'icônes Segoe Fluent : coche, triangle d'alerte, flèche, croix.
            (Glyph, IconBrush) = result.Status switch
            {
                FileStatus.Success => ("\uE73E", new SolidColorBrush(Colors.ForestGreen)),
                FileStatus.Warning => ("\uE7BA", new SolidColorBrush(Colors.DarkOrange)),
                FileStatus.Skipped => ("\uE72A", new SolidColorBrush(Colors.Gray)),
                _ => ("\uE711", new SolidColorBrush(Colors.Firebrick)),
            };
        }

        public string FileName { get; }
        public string Message { get; }
        public string Glyph { get; }
        public Brush IconBrush { get; }
    }
}
