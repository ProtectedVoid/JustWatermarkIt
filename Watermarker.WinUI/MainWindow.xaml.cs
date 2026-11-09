using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Watermarker.ViewModels.ViewModels;
using Watermarker.WinUI.Services;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Watermarker.WinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        // Le XAML s'y réfère par {x:Bind ViewModel.Xxx}.
        public MainWindowViewModel ViewModel { get; }

        public MainWindow()
        {
            // ⚠ Le ViewModel doit exister AVANT InitializeComponent() :
            // c'est à cet appel que les liaisons {x:Bind} lisent leurs valeurs initiales.
            ViewModel = new MainWindowViewModel(new PickerService(this));

            InitializeComponent();

            SystemBackdrop = new MicaBackdrop();                         // fond « Windows 11 »
            AppWindow.Resize(new Windows.Graphics.SizeInt32(1080, 900));  // taille initiale
        }
    }
}
