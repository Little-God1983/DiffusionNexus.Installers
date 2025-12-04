using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DiffusionNexus.Installers.Views
{
    public partial class ConfigurationNameDialog : Window
    {
        private readonly Func<string, Guid?, Task<bool>> _nameExistsCheck;
        private readonly Guid? _excludeId;

        public ConfigurationNameDialog() : this(string.Empty, null, (_, _) => Task.FromResult(false))
        {
        }

        public ConfigurationNameDialog(
            string currentName, 
            Guid? excludeId,
            Func<string, Guid?, Task<bool>> nameExistsCheck)
        {
            InitializeComponent();
            _nameExistsCheck = nameExistsCheck;
            _excludeId = excludeId;
            
            NameTextBox.Text = currentName;
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private async void OnOkClick(object? sender, RoutedEventArgs e)
        {
            var name = NameTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("Configuration name cannot be empty.");
                return;
            }

            if (await _nameExistsCheck(name, _excludeId))
            {
                ShowError($"A configuration with the name '{name}' already exists. Please choose a different name.");
                return;
            }

            Close(name);
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.IsVisible = true;
        }
    }
}
