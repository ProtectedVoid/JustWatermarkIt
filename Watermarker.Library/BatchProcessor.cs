using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace Watermarker.Library
{
    public sealed record BatchRequest(string SourceFolder, string WatermarkFile, WatermarkOptions Options, string DestinationFolder, bool RenameOutputs, string? OriginalsFolder);

    public enum FileStatus { Success, Skipped, Failed, Warning }

    public sealed record FileResult(string SourceFile, FileStatus Status, string? OutputFile = null, string? Message = null);

    public sealed record BatchProgress(int Current, int Total, string FileName);

    public static class BatchProcessor
    {
        private static readonly string[] ACCEPTED_FORMATS = [".png", ".jpg", ".bmp", ".gif", ".tiff", ".jpeg", ".pbm", ".tga", ".webp"];

        private static bool IsAcceptedImage(string file) => ACCEPTED_FORMATS.Contains(Path.GetExtension(file).ToLowerInvariant());

        public static async Task<IReadOnlyList<FileResult>> RunAsync(BatchRequest request, IProgress<BatchProgress> progress, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(progress);
            ArgumentOutOfRangeException.ThrowIfLessThan(request.Options.Opacity, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Options.Opacity, 1);

            // Erreur si le watermark n'existe pas
            if (!File.Exists(request.WatermarkFile))
            {
                throw new FileNotFoundException($"Le fichier {request.WatermarkFile} n'existe pas");
            }
            if (!IsAcceptedImage(request.WatermarkFile))
            {
                throw new ArgumentException($"Le watermark choisi n'a pas une extension valide (Formats acceptés : {string.Join(',', ACCEPTED_FORMATS)}");
            }
            using var watermark = Image.Load(request.WatermarkFile);

            // Erreur si le dossier source n'existe pas
            if (!Directory.Exists(request.SourceFolder))
            {
                throw new DirectoryNotFoundException($"Le dossier {request.SourceFolder} n'existe pas");
            }

            // Erreur si deux dossiers sont identiques
            if (string.Compare(Path.TrimEndingDirectorySeparator(request.SourceFolder), Path.TrimEndingDirectorySeparator(request.DestinationFolder)) == 0)
            {
                throw new ArgumentException("Le dossier cible doit être différent du dossier source");
            }
            if (!string.IsNullOrWhiteSpace(request.OriginalsFolder) && string.Compare(Path.TrimEndingDirectorySeparator(request.SourceFolder), Path.TrimEndingDirectorySeparator(request.OriginalsFolder)) == 0)
            {
                throw new ArgumentException("Le dossier des originaux doit être différent du dossier source");
            }
            if (!string.IsNullOrWhiteSpace(request.OriginalsFolder) && string.Compare(Path.TrimEndingDirectorySeparator(request.DestinationFolder), Path.TrimEndingDirectorySeparator(request.OriginalsFolder)) == 0)
            {
                throw new ArgumentException("Le dossier des originaux doit être différent du dossier cible");
            }
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
                    fileResults.Add(new FileResult(file, FileStatus.Skipped, null, "L'opération a été annulée"));
                    continue;
                }

                var newName = request.RenameOutputs ? $"w_{Path.GetFileName(file)}" : Path.GetFileName(file);
                var targetFile = Path.Combine(request.DestinationFolder, newName);
                var targetTempFile = Path.Combine(request.DestinationFolder, $"tmp_{newName}");
                if (File.Exists(targetFile))
                {
                    fileResults.Add(new FileResult(file, FileStatus.Skipped, targetFile, "Le fichier existe déjà dans la destination"));
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
                            fileResults.Add(new FileResult(file, FileStatus.Warning, targetFile, "La source n'a pas été déplacée car un fichier de même nom existe déjà"));
                        }
                        else
                        {
                            try
                            {
                                File.Move(file, targetSource);
                                fileResults.Add(new FileResult(file, FileStatus.Success, targetFile, "Fichier traité avec succès"));
                            }
                            catch (Exception e)
                            {
                                fileResults.Add(new FileResult(file, FileStatus.Warning, targetFile, $"La source n'a pas été déplacée : {e.Message}"));
                            }
                        }
                    }
                    else
                    {
                        fileResults.Add(new FileResult(file, FileStatus.Success, targetFile, "Fichier traité avec succès"));
                    }
                }
                catch (Exception e)
                {
                    fileResults.Add(new FileResult(file, FileStatus.Failed, null, e.Message));
                    continue;
                }
            }

            return fileResults;
        }
    }
}
