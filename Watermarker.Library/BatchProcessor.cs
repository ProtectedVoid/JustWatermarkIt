using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using Watermarker.Library.Exceptions;

namespace Watermarker.Library
{
    

    public enum FileMessageCode { Processed, TargetAlreadyExists, SourceAlreadyExists, SourceNotMoved, Cancelled, Failed }

    public sealed record BatchRequest(string SourceFolder, string WatermarkFile, WatermarkOptions Options, string DestinationFolder, bool RenameOutputs, string? OriginalsFolder);

    public enum FileStatus { Success, Skipped, Failed, Warning }

    public sealed record FileResult(string SourceFile, FileStatus Status, FileMessageCode Result, string? OutputFile = null, string? Details = null);

    public sealed record BatchProgress(int Current, int Total, string FileName);

    public static class BatchProcessor
    {
        private static readonly string[] ACCEPTED_FORMATS = [".png", ".jpg", ".bmp", ".gif", ".tiff", ".jpeg", ".pbm", ".tga", ".webp"];

        private static bool IsAcceptedImage(string file) => ACCEPTED_FORMATS.Contains(Path.GetExtension(file).ToLowerInvariant());

        public static async Task<IReadOnlyList<FileResult>> RunAsync(BatchRequest request, IProgress<BatchProgress> progress, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(progress);

            BatchValidationException.ThrowIf(request.Options.Opacity < 0 || request.Options.Opacity > 1, BatchValidationError.InvalidOpacity);

            // Erreur si le watermark n'existe pas
            BatchValidationException.ThrowIf(!File.Exists(request.WatermarkFile), BatchValidationError.WatermarkNotFound);

            BatchValidationException.ThrowIf(!IsAcceptedImage(request.WatermarkFile), BatchValidationError.InvalidWatermarkFormat);
            
            using var watermark = Image.Load(request.WatermarkFile);

            // Erreur si le dossier source n'existe pas
            BatchValidationException.ThrowIf(!Directory.Exists(request.SourceFolder), BatchValidationError.SourceFolderNotFound);

            // Erreur si deux dossiers sont identiques
            BatchValidationException.ThrowIf(string.Compare(Path.TrimEndingDirectorySeparator(request.SourceFolder), Path.TrimEndingDirectorySeparator(request.DestinationFolder)) == 0, BatchValidationError.DestinationSameAsSource);

            BatchValidationException.ThrowIf(!string.IsNullOrWhiteSpace(request.OriginalsFolder) && string.Compare(Path.TrimEndingDirectorySeparator(request.SourceFolder), Path.TrimEndingDirectorySeparator(request.OriginalsFolder)) == 0,BatchValidationError.NewOriginalsSameAsSource);

            BatchValidationException.ThrowIf(!string.IsNullOrWhiteSpace(request.OriginalsFolder) && string.Compare(Path.TrimEndingDirectorySeparator(request.DestinationFolder), Path.TrimEndingDirectorySeparator(request.OriginalsFolder)) == 0, BatchValidationError.NewOriginalsSameAsDestination);
            
            var fileList = Directory.GetFiles(request.SourceFolder)
                .Where(IsAcceptedImage)
                .ToList();

            int fileCount = 0;

            List<FileResult> fileResults = [];
            // On crée les dossiers destinations s'ils n'existent pas
            if (!Directory.Exists(request.DestinationFolder))
            {
                Directory.CreateDirectory(request.DestinationFolder);
            }
            if (!string.IsNullOrWhiteSpace(request.OriginalsFolder) && !Directory.Exists(request.OriginalsFolder))
            {
                Directory.CreateDirectory(request.OriginalsFolder);
            }

            foreach (var file in fileList)
            {
                progress.Report(new BatchProgress(++fileCount, fileList.Count, file));
                if (token.IsCancellationRequested)
                {
                    fileResults.Add(new FileResult(file, FileStatus.Skipped, FileMessageCode.Cancelled));
                    continue;
                }

                var newName = request.RenameOutputs ? $"w_{Path.GetFileName(file)}" : Path.GetFileName(file);
                var targetFile = Path.Combine(request.DestinationFolder, newName);
                var targetTempFile = Path.Combine(request.DestinationFolder, $"tmp_{newName}");
                if (File.Exists(targetFile))
                {
                    fileResults.Add(new FileResult(file, FileStatus.Skipped, FileMessageCode.TargetAlreadyExists, targetFile));
                    continue;
                }

                try
                {
                    using Image source = Image.Load(file);
                    using var result = source.ApplyWatermark(watermark, request.Options);
                    await result.SaveAsync(targetTempFile, token);
                    File.Move(targetTempFile, targetFile);

                    if (!string.IsNullOrWhiteSpace(request.OriginalsFolder))
                    {
                        var targetSource = Path.Combine(request.OriginalsFolder, Path.GetFileName(file));
                        if (File.Exists(targetSource))
                        {
                            fileResults.Add(new FileResult(file, FileStatus.Warning, FileMessageCode.SourceAlreadyExists, targetFile));
                        }
                        else
                        {
                            try
                            {
                                File.Move(file, targetSource);
                                fileResults.Add(new FileResult(file, FileStatus.Success, FileMessageCode.Processed, targetFile));
                            }
                            catch (Exception e)
                            {
                                fileResults.Add(new FileResult(file, FileStatus.Warning, FileMessageCode.SourceNotMoved, targetFile, e.Message));
                            }
                        }
                    }
                    else
                    {
                        fileResults.Add(new FileResult(file, FileStatus.Success, FileMessageCode.Processed, targetFile));
                    }
                }
                catch (Exception e)
                {
                    fileResults.Add(new FileResult(file, FileStatus.Failed, FileMessageCode.Failed, null, e.Message));
                    continue;
                }
            }

            return fileResults;
        }
    }
}
