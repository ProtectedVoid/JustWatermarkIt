using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using JustWatermarkIt.Library;

namespace JustWatermarkIt.WinUI.Helpers
{
    public static class DisplayHelper
    {
        private static readonly Brush Green = new SolidColorBrush(Colors.ForestGreen);
        private static readonly Brush Orange = new SolidColorBrush(Colors.DarkOrange);
        private static readonly Brush Gray = new SolidColorBrush(Colors.Gray);
        private static readonly Brush Red = new SolidColorBrush(Colors.Firebrick);

        public static Brush GetBrush(FileStatus status) => status switch
        {
            FileStatus.Success => Green,
            FileStatus.Warning => Orange,
            FileStatus.Skipped => Gray,
            _ => Red
        };

        public static string GetGlyph(FileStatus status) => status switch
        {
            FileStatus.Success => "\uE73E",
            FileStatus.Warning => "\uE7BA",
            FileStatus.Skipped => "\uE72A",
            _ => "\uE711",
        };
    }
}
