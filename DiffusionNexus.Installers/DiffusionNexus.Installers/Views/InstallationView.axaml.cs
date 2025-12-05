using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DiffusionNexus.Installers.ViewModels;

namespace DiffusionNexus.Installers.Views;

public partial class InstallationView : UserControl, IFolderPickerService, IUserPromptService
{
    public InstallationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is InstallationViewModel viewModel)
        {
            viewModel.AttachFolderPickerService(this);
            viewModel.AttachUserPromptService(this);
        }
    }

    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken = default)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Installation Folder",
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    public async Task<bool> ConfirmAsync(string title, string message, string yesButtonText = "Yes", string noButtonText = "No")
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return false;

        var dialog = new Window
        {
            Title = title,
            Width = 500,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(20)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Margin = new Avalonia.Thickness(20, 0, 20, 20)
        };

        var noButton = new Button
        {
            Content = noButtonText,
            Width = 150,
            Padding = new Avalonia.Thickness(10, 8)
        };

        var yesButton = new Button
        {
            Content = yesButtonText,
            Width = 180,
            Padding = new Avalonia.Thickness(10, 8),
            Background = new SolidColorBrush(Color.FromRgb(102, 126, 234)),
            Foreground = Brushes.White
        };

        noButton.Click += (s, e) => dialog.Close(false);
        yesButton.Click += (s, e) => dialog.Close(true);

        buttonPanel.Children.Add(noButton);
        buttonPanel.Children.Add(yesButton);

        var mainPanel = new StackPanel();
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        dialog.Content = mainPanel;

        var result = await dialog.ShowDialog<bool>(parentWindow);
        return result;
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return;

        var dialog = new Window
        {
            Title = title,
            Width = 450,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(20),
            Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69))
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(20, 0, 20, 20)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 100,
            Padding = new Avalonia.Thickness(10, 8)
        };

        okButton.Click += (s, e) => dialog.Close();

        buttonPanel.Children.Add(okButton);

        var mainPanel = new StackPanel();
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        dialog.Content = mainPanel;

        await dialog.ShowDialog(parentWindow);
    }

    public async Task ShowInfoAsync(string title, string message)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parentWindow) return;

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Avalonia.Thickness(20)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Avalonia.Thickness(20, 0, 20, 20)
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 100,
            Padding = new Avalonia.Thickness(10, 8)
        };

        okButton.Click += (s, e) => dialog.Close();

        buttonPanel.Children.Add(okButton);

        var mainPanel = new StackPanel();
        mainPanel.Children.Add(messageText);
        mainPanel.Children.Add(buttonPanel);

        dialog.Content = mainPanel;

        await dialog.ShowDialog(parentWindow);
    }
}
