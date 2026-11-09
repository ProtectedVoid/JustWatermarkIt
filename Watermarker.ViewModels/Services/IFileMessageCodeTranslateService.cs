using System;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Text;

namespace Watermarker.ViewModels.Services
{
    public interface IFileMessageCodeTranslateService
    {
        string Get(string key);

        string Format(string key, params object[] args) => string.Format(Get(key), args);
    }
}
