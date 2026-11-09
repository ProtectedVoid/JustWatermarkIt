using Microsoft.Windows.ApplicationModel.Resources;
using System;
using System.Collections.Generic;
using System.Text;
using Watermarker.ViewModels.Services;

namespace Watermarker.WinUI.Services
{
    public class FileMessageCodeTranslateService : IFileMessageCodeTranslateService
    {
        private readonly ResourceLoader _loader = new();

        string IFileMessageCodeTranslateService.Get(string key)
        {
            var result = _loader.GetString(key);
            return string.IsNullOrWhiteSpace(result) ? key : result;
        }
    }
}
