using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AIKnowledge2Go.Installers.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnCopyAllLog(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        await clipboard.SetTextAsync(LogTextBox.Text ?? string.Empty);
    }
}
