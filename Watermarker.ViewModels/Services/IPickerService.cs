using System;
using System.Collections.Generic;
using System.Text;

namespace Watermarker.ViewModels.Services
{
    public interface IPickerService
    {
        protected static readonly string[] ImageExtensions = 
            [".png", ".jpg', '.jpeg", ".bmp", ".gif", ".tiff", ".webp", ".tga", ".pbm"];

        public Task<string?> PickFolderAsync();

        public Task<string?> PickImageFileAsync();
    }
}
