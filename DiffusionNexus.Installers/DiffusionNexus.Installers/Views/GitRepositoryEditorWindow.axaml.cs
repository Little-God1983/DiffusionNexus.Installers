using Avalonia.Controls;
using Avalonia.Interactivity;

namespace DiffusionNexus.Installers.Views
{
    public partial class GitRepositoryEditorWindow : Window
    {
        public GitRepositoryEditorWindow()
        {
            InitializeComponent();
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
