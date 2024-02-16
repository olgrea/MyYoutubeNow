using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
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

    public void GoToUrl(object sender, RoutedEventArgs e)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new SystemException("OS not supported");

        Button? button = sender as Button;
        VideoViewModel? vm = button?.DataContext as VideoViewModel;
        string? url = vm?.Url;
        ArgumentNullException.ThrowIfNullOrEmpty(url);

        //https://stackoverflow.com/a/2796367/241446
        using var proc = new Process { StartInfo = { UseShellExecute = true, FileName = url } };
        proc.Start();
    }
}
