using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Watermarker.Library;
using Watermarker.ViewModels.Services;

namespace Watermarker.ViewModels.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly IPickerService _picker;
        private CancellationTokenSource? _cts;

        public MainWindowViewModel(IPickerService picker) => _picker = picker;

        // ------------------------------------------------------------------
        // Propriétés reliées au formulaire
        // [NotifyCanExecuteChangedFor] : quand la propriété change, le bouton « Lancer »
        // réévalue s'il doit être grisé ou non (voir CanStart plus bas).
        // ------------------------------------------------------------------
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        private string _sourceFolder = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        private string _watermarkFile = "";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        private string _destinationFolder = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OpacityLabel))]
        private double _opacityPercent = 30; // 30 % par défaut

        // 0 = Large, 1 = Coin (index du RadioButtons)
        [ObservableProperty]
        private int _modeIndex = 0;

        [ObservableProperty]
        private bool _renameOutputs = true;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        private bool _moveOriginals;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        private string _originalsFolder = "";

        // ------------------------------------------------------------------
        // État du traitement
        // ------------------------------------------------------------------
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(StartCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
        [NotifyPropertyChangedFor(nameof(IsNotRunning))]
        private bool _isRunning;

        [ObservableProperty]
        private double _progressValue; // 0 à 100

        [ObservableProperty]
        private string _progressText = "";

        public ObservableCollection<FileResult> Results { get; } = [];

        // Propriétés calculées, lues par la page
        public bool IsNotRunning => !IsRunning;
        public string OpacityLabel => $"{OpacityPercent:0} %";
        private PlacementMode Mode => ModeIndex == 0 ? PlacementMode.Wide : PlacementMode.Corner;

        // ------------------------------------------------------------------
        // Boutons « Choisir… » (la méthode PickSourceFolderAsync donne la commande PickSourceFolderCommand)
        // ------------------------------------------------------------------
        [RelayCommand]
        private async Task PickSourceFolderAsync()
        {
            var path = await _picker.PickFolderAsync();
            if (path is not null) SourceFolder = path;
        }

        [RelayCommand]
        private async Task PickWatermarkFileAsync()
        {
            var path = await _picker.PickImageFileAsync();
            if (path is not null) WatermarkFile = path;
        }

        [RelayCommand]
        private async Task PickDestinationFolderAsync()
        {
            var path = await _picker.PickFolderAsync();
            if (path is not null) DestinationFolder = path;
        }

        [RelayCommand]
        private async Task PickOriginalsFolderAsync()
        {
            var path = await _picker.PickFolderAsync();
            if (path is not null) OriginalsFolder = path;
        }

        // ------------------------------------------------------------------
        // Lancer / Annuler
        // ------------------------------------------------------------------
        private bool CanStart() =>
            !IsRunning
            && !string.IsNullOrWhiteSpace(SourceFolder)
            && !string.IsNullOrWhiteSpace(WatermarkFile)
            && !string.IsNullOrWhiteSpace(DestinationFolder)
            && (!MoveOriginals || !string.IsNullOrWhiteSpace(OriginalsFolder));

        [RelayCommand(CanExecute = nameof(CanStart))]
        private async Task StartAsync()
        {
            Results.Clear();
            ProgressValue = 0;
            ProgressText = "Préparation…";
            IsRunning = true;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                var request = new BatchRequest(
                    SourceFolder,
                    WatermarkFile,
                    new WatermarkOptions((float)(OpacityPercent / 100.0), Mode),
                    DestinationFolder,
                    RenameOutputs,
                    MoveOriginals ? OriginalsFolder : null);

                // Progress<T> est créé ICI, sur le fil de l'interface : ses rappels y reviennent donc
                // automatiquement, même si le traitement tourne sur un autre fil. On peut alors
                // modifier ProgressValue et ProgressText sans précaution.
                var progress = new Progress<BatchProgress>(p =>
                {
                    if (p.Total > 0) ProgressValue = 100.0 * p.Current / p.Total;
                    ProgressText = $"Fichier {p.Current} / {p.Total} : {Path.GetFileName(p.FileName)}";
                });

                // Task.Run : le traitement des images est du calcul lourd. Sans lui, il bloquerait
                // le fil de l'interface et la fenêtre se figerait.
                // ⚠ À ADAPTER : le nom de ta classe qui contient RunAsync.
                var results = await Task.Run(() => BatchProcessor.RunAsync(request, progress, token));

                foreach (var result in results) Results.Add(result);
                ProgressText = Summarize(results, token.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                // Erreurs levées AVANT de toucher aux fichiers (dossier absent, watermark illisible, dossiers identiques…)
                ProgressText = $"Impossible de lancer le traitement : {ex.Message}";
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                IsRunning = false;
            }
        }

        private bool CanCancel() => IsRunning;

        [RelayCommand(CanExecute = nameof(CanCancel))]
        private void Cancel()
        {
            ProgressText = "Annulation en cours…";
            _cts?.Cancel();
        }

        private static string Summarize(IReadOnlyList<FileResult> results, bool cancelled)
        {
            if (results.Count == 0) return "Aucune image trouvée dans le dossier source.";

            int ok = results.Count(r => r.Status == FileStatus.Success);
            int warnings = results.Count(r => r.Status == FileStatus.Warning);
            int skipped = results.Count(r => r.Status == FileStatus.Skipped);
            int failed = results.Count(r => r.Status == FileStatus.Failed);

            var text = $"{results.Count} fichier(s) : {ok} traité(s), {warnings} avertissement(s), {skipped} ignoré(s), {failed} en erreur.";
            return cancelled ? "Annulé. " + text : text;
        }
    }
}
