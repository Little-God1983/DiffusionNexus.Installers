using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DiffusionNexus.Installers.ViewModels;

namespace DiffusionNexus.Installers.Views;

public partial class InstallationView : UserControl, IFolderPickerService, IUserPromptService
{
    // Theme colors matching InstallationView.axaml
    private static readonly Color BackgroundDark = Color.Parse("#1a1a2e");
    private static readonly Color BackgroundMid = Color.Parse("#16213e");
    private static readonly Color BackgroundLight = Color.Parse("#0f3460");
    private static readonly Color CardBackground = Color.Parse("#2a2a4a");
    private static readonly Color CardBorder = Color.Parse("#3a3a5a");
    private static readonly Color TextPrimary = Colors.White;
    private static readonly Color TextSecondary = Color.Parse("#aaaacc");
    private static readonly Color AccentPrimary = Color.Parse("#667eea");
    private static readonly Color AccentSecondary = Color.Parse("#764ba2");
    private static readonly Color ButtonSecondary = Color.Parse("#4a4a6a");

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

    private static LinearGradientBrush CreateBackgroundGradient() =>
        new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(BackgroundDark, 0),
                new GradientStop(BackgroundMid, 0.5),
                new GradientStop(BackgroundLight, 1)
            }
        };

    private static LinearGradientBrush CreatePrimaryButtonGradient() =>
        new()
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(AccentPrimary, 0),
                new GradientStop(AccentSecondary, 1)
            }
        };

    private static Border CreateStyledCard(Control content) =>
        new()
        {
            Background = new SolidColorBrush(CardBackground),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(24),
            BorderBrush = new SolidColorBrush(CardBorder),
            BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 4 20 0 #40000000"),
            Child = content
        };

    private static Button CreatePrimaryButton(string text) =>
        new()
        {
            Content = text,
            Background = CreatePrimaryButtonGradient(),
            Foreground = new SolidColorBrush(TextPrimary),
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            Padding = new Thickness(24, 12),
            CornerRadius = new CornerRadius(8),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

    private static Button CreateSecondaryButton(string text) =>
        new()
        {
            Content = text,
            Background = new SolidColorBrush(ButtonSecondary),
            Foreground = new SolidColorBrush(TextPrimary),
            FontSize = 14,
            Padding = new Thickness(24, 12),
            CornerRadius = new CornerRadius(8),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            HorizontalContentAlignment = HorizontalAlignment.Center
        };

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
            CanResize = false,
            Background = CreateBackgroundGradient(),
            SystemDecorations = SystemDecorations.BorderOnly
        };

        // Title
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Foreground = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(AccentPrimary, 0),
                    new GradientStop(AccentSecondary, 0.5),
                    new GradientStop(Color.Parse("#00c6ff"), 1)
                }
            },
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16)
        };

        // Message
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(TextSecondary),
            FontSize = 14,
            TextAlignment = TextAlignment.Center,
            LineHeight = 22
        };

        // Buttons
        var noButton = CreateSecondaryButton(noButtonText);
        var yesButton = CreatePrimaryButton(yesButtonText);

        noButton.Click += (s, e) => dialog.Close(false);
        yesButton.Click += (s, e) => dialog.Close(true);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 16,
            Margin = new Thickness(0, 20, 0, 0)
        };
        buttonPanel.Children.Add(noButton);
        buttonPanel.Children.Add(yesButton);

        // Card content
        var cardContent = new StackPanel { Spacing = 8 };
        cardContent.Children.Add(titleText);
        cardContent.Children.Add(messageText);
        cardContent.Children.Add(buttonPanel);

        // Main layout with card
        var mainPanel = new Border
        {
            Padding = new Thickness(24),
            Child = CreateStyledCard(cardContent)
        };

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
            Width = 480,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = CreateBackgroundGradient(),
            SystemDecorations = SystemDecorations.BorderOnly
        };

        // Error icon (using text emoji as simple icon)
        var iconText = new TextBlock
        {
            Text = "?",
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse("#ff6b6b")),
            Margin = new Thickness(0, 0, 0, 12)
        };

        // Title
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = new SolidColorBrush(Color.Parse("#ff6b6b")),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        };

        // Message
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(TextSecondary),
            FontSize = 14,
            TextAlignment = TextAlignment.Center,
            LineHeight = 22
        };

        // OK Button
        var okButton = CreateSecondaryButton("OK");
        okButton.HorizontalAlignment = HorizontalAlignment.Center;
        okButton.Margin = new Thickness(0, 20, 0, 0);
        okButton.Click += (s, e) => dialog.Close();

        // Card content
        var cardContent = new StackPanel();
        cardContent.Children.Add(iconText);
        cardContent.Children.Add(titleText);
        cardContent.Children.Add(messageText);
        cardContent.Children.Add(okButton);

        // Main layout with card
        var mainPanel = new Border
        {
            Padding = new Thickness(24),
            Child = CreateStyledCard(cardContent)
        };

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
            Width = 450,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = CreateBackgroundGradient(),
            SystemDecorations = SystemDecorations.BorderOnly
        };

        // Info icon
        var iconText = new TextBlock
        {
            Text = "?",
            FontSize = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new SolidColorBrush(AccentPrimary),
            Margin = new Thickness(0, 0, 0, 12)
        };

        // Title
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
            Foreground = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(AccentPrimary, 0),
                    new GradientStop(AccentSecondary, 1)
                }
            },
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 12)
        };

        // Message
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(TextSecondary),
            FontSize = 14,
            TextAlignment = TextAlignment.Center,
            LineHeight = 22
        };

        // OK Button
        var okButton = CreatePrimaryButton("OK");
        okButton.HorizontalAlignment = HorizontalAlignment.Center;
        okButton.Margin = new Thickness(0, 20, 0, 0);
        okButton.Click += (s, e) => dialog.Close();

        // Card content
        var cardContent = new StackPanel();
        cardContent.Children.Add(iconText);
        cardContent.Children.Add(titleText);
        cardContent.Children.Add(messageText);
        cardContent.Children.Add(okButton);

        // Main layout with card
        var mainPanel = new Border
        {
            Padding = new Thickness(24),
            Child = CreateStyledCard(cardContent)
        };

        dialog.Content = mainPanel;

        await dialog.ShowDialog(parentWindow);
    }
}
