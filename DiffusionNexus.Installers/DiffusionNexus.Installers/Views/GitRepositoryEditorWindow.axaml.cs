using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DiffusionNexus.Core.Models.Entities;

namespace DiffusionNexus.Installers.Views
{
    public partial class GitRepositoryEditorWindow : Window
    {
        private readonly GitRepository _draft;
        private readonly TextBox _nameTextBox;
        private readonly TextBox _urlTextBox;
        private readonly CheckBox _installRequirementsCheckBox;
        private readonly TextBlock _validationMessage;

        public GitRepositoryEditorWindow()
            : this(new GitRepository(), true)
        {
        }

        public GitRepositoryEditorWindow(GitRepository draft, bool isNew)
        {
            InitializeComponent();

            _draft = draft;
            _nameTextBox = this.FindControl<TextBox>("NameTextBox")
                ?? throw new InvalidOperationException("NameTextBox was not found in the dialog template.");
            _urlTextBox = this.FindControl<TextBox>("UrlTextBox")
                ?? throw new InvalidOperationException("UrlTextBox was not found in the dialog template.");
            _installRequirementsCheckBox = this.FindControl<CheckBox>("InstallRequirementsCheckBox")
                ?? throw new InvalidOperationException("InstallRequirementsCheckBox was not found in the dialog template.");
            _validationMessage = this.FindControl<TextBlock>("ValidationMessage")
                ?? throw new InvalidOperationException("ValidationMessage was not found in the dialog template.");

            Title = isNew ? "Add Git Repository" : "Edit Git Repository";
            _nameTextBox.Text = draft.Name;
            _urlTextBox.Text = draft.Url;
            _installRequirementsCheckBox.IsChecked = draft.InstallRequirements;

            Opened += OnOpened;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            _urlTextBox.Focus();
            _urlTextBox.CaretIndex = _urlTextBox.Text?.Length ?? 0;
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            var name = _nameTextBox.Text?.Trim() ?? string.Empty;
            var url = _urlTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(url))
            {
                _validationMessage.Text = "Repository URL is required.";
                return;
            }

            _validationMessage.Text = string.Empty;

            _draft.Name = name;
            _draft.Url = url;
            _draft.InstallRequirements = _installRequirementsCheckBox.IsChecked ?? false;

            Close(_draft);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
