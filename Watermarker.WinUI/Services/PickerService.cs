using Microsoft.UI.Xaml;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Watermarker.ViewModels.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Watermarker.WinUI.Services
{
    public class PickerService(Window window) : IPickerService
    {
        async Task<string?> IPickerService.PickFolderAsync()
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*"); // obligatoire, même pour un dossier
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

            StorageFolder? folder = await picker.PickSingleFolderAsync();
            return folder?.Path; // null si l'utilisateur annule
        }

        async Task<string?> IPickerService.PickImageFileAsync()
        {
            var picker = new FileOpenPicker();
            foreach (var extension in IPickerService.ImageExtensions) picker.FileTypeFilter.Add(extension);
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

            StorageFile? file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
    }
}
