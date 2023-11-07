using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MyYoutubeNowApp.ViewModels;

namespace MyYoutubeNowApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public async void SelectOutputDir(object sender, RoutedEventArgs e)
    {
        // Get top level from the current control. Alternatively, you can use Window reference instead.
        var topLevel = TopLevel.GetTopLevel(this);
        MainViewModel? vm = DataContext as MainViewModel;
        if (topLevel == null || vm == null)
            return;

        // Start async operation to open the dialog.
        FolderPickerOpenOptions options = new FolderPickerOpenOptions
        {
            Title = "Select Output Folder",
            AllowMultiple = false,
        };

        if(vm.OutputDir != null)
        {
            IStorageFolder? storageFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(vm.OutputDir);
            if (storageFolder != null)
                options.SuggestedStartLocation = storageFolder;
        }

        IReadOnlyList<IStorageFolder> folder = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        if(folder.Any())
        {
            var newFolder = folder.First().TryGetLocalPath();
            if (newFolder != null)
                vm.OutputDir = newFolder;
        }
    }
}
