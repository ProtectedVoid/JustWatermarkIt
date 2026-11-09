using System;
using System.Collections.Generic;
using System.Text;
using Watermarker.Library;

namespace Watermarker.ViewModels.DTO
{
    public sealed class ResultItem(FileResult result)
    {
        public string FileName { get; } = Path.GetFileName(result.SourceFile);
        public string Message { get; } = result.Message ?? "";
        public FileStatus Status { get; } = result.Status;
    }
}
