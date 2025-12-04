using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using DiffusionNexus.Core.Models;

namespace DiffusionNexus.Installers.Views
{
    public partial class ModelEditorDialog : Window
    {
        private readonly ModelDownload _draft;
        private readonly bool _vramEnabled;
        private readonly string[] _availableVramProfiles;
        private readonly ObservableCollection<DownloadLinkViewModel> _downloadLinks = new();
        
        private readonly TextBox _nameTextBox;
        private readonly TextBox _destinationTextBox;
        private readonly CheckBox _enabledCheckBox;
        private readonly Border _vramInfoBorder;
        private readonly ItemsControl _downloadLinksItemsControl;
        private readonly TextBlock _validationMessage;

        public ModelEditorDialog()
            : this(new ModelDownload(), false, [], true)
        {
        }

        public ModelEditorDialog(
            ModelDownload draft,
            bool vramEnabled,
            string[] availableVramProfiles,
            bool isNew)
        {
            InitializeComponent();

            _draft = draft;
            _vramEnabled = vramEnabled;
            _availableVramProfiles = availableVramProfiles;

            _nameTextBox = this.FindControl<TextBox>("NameTextBox")
                ?? throw new InvalidOperationException("NameTextBox not found");
            _destinationTextBox = this.FindControl<TextBox>("DestinationTextBox")
                ?? throw new InvalidOperationException("DestinationTextBox not found");
            _enabledCheckBox = this.FindControl<CheckBox>("EnabledCheckBox")
                ?? throw new InvalidOperationException("EnabledCheckBox not found");
            _vramInfoBorder = this.FindControl<Border>("VramInfoBorder")
                ?? throw new InvalidOperationException("VramInfoBorder not found");
            _downloadLinksItemsControl = this.FindControl<ItemsControl>("DownloadLinksItemsControl")
                ?? throw new InvalidOperationException("DownloadLinksItemsControl not found");
            _validationMessage = this.FindControl<TextBlock>("ValidationMessage")
                ?? throw new InvalidOperationException("ValidationMessage not found");

            Title = isNew ? "Add Model" : "Edit Model";

            _nameTextBox.Text = draft.Name;
            _destinationTextBox.Text = draft.Destination;
            _enabledCheckBox.IsChecked = draft.Enabled;

            if (!_vramEnabled)
            {
                _vramInfoBorder.IsVisible = true;
            }

            foreach (var link in draft.DownloadLinks)
            {
                _downloadLinks.Add(new DownloadLinkViewModel(link, _vramEnabled, _availableVramProfiles));
            }

            _downloadLinksItemsControl.ItemsSource = _downloadLinks;

            Opened += OnOpened;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            _nameTextBox.Focus();
            _nameTextBox.SelectAll();
        }

        private void OnAddLinkClick(object? sender, RoutedEventArgs e)
        {
            var newLink = new ModelDownloadLink
            {
                Url = string.Empty,
                VramProfile = null,
                Destination = string.Empty,
                Enabled = true
            };

            _downloadLinks.Add(new DownloadLinkViewModel(newLink, _vramEnabled, _availableVramProfiles));
        }

        private void OnRemoveLinkClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DownloadLinkViewModel linkVm)
            {
                _downloadLinks.Remove(linkVm);
            }
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            var name = _nameTextBox.Text?.Trim() ?? string.Empty;
            var destination = _destinationTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                _validationMessage.Text = "Model name is required.";
                return;
            }

            if (_downloadLinks.Count == 0)
            {
                _validationMessage.Text = "At least one download link is required.";
                return;
            }

            foreach (var link in _downloadLinks)
            {
                if (string.IsNullOrWhiteSpace(link.Url))
                {
                    _validationMessage.Text = "All download links must have a URL.";
                    return;
                }
            }

            _validationMessage.Text = string.Empty;

            _draft.Name = name;
            _draft.Destination = destination;
            _draft.Enabled = _enabledCheckBox.IsChecked ?? true;
            
            _draft.DownloadLinks.Clear();
            foreach (var linkVm in _downloadLinks)
            {
                _draft.DownloadLinks.Add(linkVm.ToModel());
            }

            Close(_draft);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }

    public partial class DownloadLinkViewModel : ObservableObject
    {
        private readonly ModelDownloadLink _model;
        private readonly string[] _baseVramProfiles;

        public DownloadLinkViewModel(
            ModelDownloadLink model,
            bool vramEnabled,
            string[] availableVramProfiles)
        {
            _model = model;
            _baseVramProfiles = availableVramProfiles;
            VramEnabled = vramEnabled;

            _url = model.Url;
            _destination = model.Destination;
            _enabled = model.Enabled;

            var profiles = new ObservableCollection<string> { "None" };
            if (vramEnabled && availableVramProfiles.Length > 0)
            {
                foreach (var profile in availableVramProfiles)
                {
                    profiles.Add(profile);
                }
            }
            AvailableVramProfiles = profiles;

            if (model.VramProfile.HasValue)
            {
                _selectedVramProfile = model.VramProfile.Value.ToString().Replace("VRAM_", "") + "GB";
                if (!AvailableVramProfiles.Contains(_selectedVramProfile))
                {
                    _selectedVramProfile = "None";
                }
            }
            else
            {
                _selectedVramProfile = "None";
            }
        }

        public ObservableCollection<string> AvailableVramProfiles { get; }

        public bool VramEnabled { get; }

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private string _selectedVramProfile = "None";

        [ObservableProperty]
        private string _destination = string.Empty;

        [ObservableProperty]
        private bool _enabled = true;

        public ModelDownloadLink ToModel()
        {
            _model.Url = Url;
            _model.Destination = Destination;
            _model.Enabled = Enabled;

            if (SelectedVramProfile == "None" || !VramEnabled)
            {
                _model.VramProfile = null;
            }
            else
            {
                _model.VramProfile = SelectedVramProfile switch
                {
                    "8GB" => VramProfile.VRAM_8GB,
                    "12GB" => VramProfile.VRAM_12GB,
                    "16GB" => VramProfile.VRAM_16GB,
                    "24GB" => VramProfile.VRAM_24GB,
                    "24+GB" => VramProfile.VRAM_24GB,
                    _ => null
                };
            }

            return _model;
        }
    }
}
