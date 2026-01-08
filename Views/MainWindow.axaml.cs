using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DownloadHDAvalonia.ViewModels;
using CodingSeb.Localization;
using System.ComponentModel;
using System.Linq;

namespace DownloadHDAvalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainWindowViewModel();
            DataContext = viewModel;
            
            // Assinar evento para scroll automático quando o progresso atualizar
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainWindowViewModel.ProgressText))
            {
                // Scroll automático quando o progresso atualizar
                Dispatcher.UIThread.Post(() =>
                {
                    scrollViewerApp?.ScrollToEnd();
                }, DispatcherPriority.Background);
            }
        }

        private async void OpenFolderDialog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
                return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = Loc.Tr("Selecionar pasta para download")
            });

            var folder = folders.FirstOrDefault();
            if (folder != null && DataContext is MainWindowViewModel vm)
            {
                string selectedPath = folder.Path.LocalPath;
                vm.FolderPath = selectedPath;
            }
        }
    }
}

