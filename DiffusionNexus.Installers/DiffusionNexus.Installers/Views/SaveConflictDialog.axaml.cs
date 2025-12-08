using Avalonia.Controls;
using Avalonia.Interactivity;
using DiffusionNexus.Core.Models.Enums;

namespace DiffusionNexus.Installers.Views
{
    public partial class SaveConflictDialog : Window
    {
        public SaveConflictDialog() : this(string.Empty)
        {
        }

        public SaveConflictDialog(string configurationName)
        {
            InitializeComponent();
            DataContext = new { ConfigurationName = configurationName };
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(SaveConflictResolution.Cancel);
        }

        private void OnOverwriteClick(object? sender, RoutedEventArgs e)
        {
            Close(SaveConflictResolution.Overwrite);
        }

        private void OnSaveAsNewClick(object? sender, RoutedEventArgs e)
        {
            Close(SaveConflictResolution.SaveAsNew);
        }
    }
}
