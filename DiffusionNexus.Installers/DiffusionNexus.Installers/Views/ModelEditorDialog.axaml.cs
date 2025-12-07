using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using DiffusionNexus.Core.Models;

namespace DiffusionNexus.Installers.Views
{
    /// <summary>
    /// Represents the predefined model type destinations.
    /// </summary>
    public enum ModelType
    {
        None,
        DiffusionModels,
        Checkpoint,
        TextEncoder,
        Lora,
        Vae
    }

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

        // Model type toggle buttons
        private readonly ToggleButton _diffusionModelsToggle;
        private readonly ToggleButton _checkpointToggle;
        private readonly ToggleButton _textEncoderToggle;
        private readonly ToggleButton _loraToggle;
        private readonly ToggleButton _vaeToggle;

        private bool _suppressDestinationTextChanged;
        private ModelType _selectedModelType = ModelType.None;

        /// <summary>
        /// Maps model types to their default destination paths.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<ModelType, string> ModelTypeDestinations = new()
        {
            { ModelType.DiffusionModels, "models/diffusion_models" },
            { ModelType.Checkpoint, "models/checkpoints" },
            { ModelType.TextEncoder, "models/text_encoders" },
            { ModelType.Lora, "models/loras" },
            { ModelType.Vae, "models/vae" }
        };

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

            // Find model type toggle buttons
            _diffusionModelsToggle = this.FindControl<ToggleButton>("DiffusionModelsToggle")
                ?? throw new InvalidOperationException("DiffusionModelsToggle not found");
            _checkpointToggle = this.FindControl<ToggleButton>("CheckpointToggle")
                ?? throw new InvalidOperationException("CheckpointToggle not found");
            _textEncoderToggle = this.FindControl<ToggleButton>("TextEncoderToggle")
                ?? throw new InvalidOperationException("TextEncoderToggle not found");
            _loraToggle = this.FindControl<ToggleButton>("LoraToggle")
                ?? throw new InvalidOperationException("LoraToggle not found");
            _vaeToggle = this.FindControl<ToggleButton>("VaeToggle")
                ?? throw new InvalidOperationException("VaeToggle not found");

            Title = isNew ? "Add Model" : "Edit Model";

            _nameTextBox.Text = draft.Name;
            _enabledCheckBox.IsChecked = draft.Enabled;

            // Initialize destination - check if it matches a known model type
            InitializeDestination(draft.Destination);

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

        /// <summary>
        /// Initializes the destination UI based on the existing destination value.
        /// </summary>
        private void InitializeDestination(string destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
            {
                return;
            }

            // Check if destination matches a known model type
            var matchedType = ModelTypeDestinations
                .FirstOrDefault(kvp => kvp.Value.Equals(destination, StringComparison.OrdinalIgnoreCase));

            if (matchedType.Key != ModelType.None)
            {
                // Select the matching model type toggle
                SelectModelType(matchedType.Key);
            }
            else
            {
                // Custom destination - show in text box
                _destinationTextBox.Text = destination;
            }
        }

        /// <summary>
        /// Selects a model type and updates the toggle buttons accordingly.
        /// </summary>
        private void SelectModelType(ModelType modelType)
        {
            _selectedModelType = modelType;

            // Update all toggle buttons
            _diffusionModelsToggle.IsChecked = modelType == ModelType.DiffusionModels;
            _checkpointToggle.IsChecked = modelType == ModelType.Checkpoint;
            _textEncoderToggle.IsChecked = modelType == ModelType.TextEncoder;
            _loraToggle.IsChecked = modelType == ModelType.Lora;
            _vaeToggle.IsChecked = modelType == ModelType.Vae;

            // Clear destination text when a model type is selected
            if (modelType != ModelType.None)
            {
                _suppressDestinationTextChanged = true;
                _destinationTextBox.Text = string.Empty;
                _suppressDestinationTextChanged = false;
            }
        }

        /// <summary>
        /// Clears all model type selections.
        /// </summary>
        private void ClearModelTypeSelection()
        {
            _selectedModelType = ModelType.None;
            _diffusionModelsToggle.IsChecked = false;
            _checkpointToggle.IsChecked = false;
            _textEncoderToggle.IsChecked = false;
            _loraToggle.IsChecked = false;
            _vaeToggle.IsChecked = false;
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            _nameTextBox.Focus();
            _nameTextBox.SelectAll();
        }

        /// <summary>
        /// Handles model type toggle button clicks for mutual exclusivity.
        /// </summary>
        private void OnModelTypeClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton clickedToggle)
            {
                return;
            }

            // Determine which model type was clicked
            ModelType clickedType = clickedToggle.Name switch
            {
                "DiffusionModelsToggle" => ModelType.DiffusionModels,
                "CheckpointToggle" => ModelType.Checkpoint,
                "TextEncoderToggle" => ModelType.TextEncoder,
                "LoraToggle" => ModelType.Lora,
                "VaeToggle" => ModelType.Vae,
                _ => ModelType.None
            };

            if (clickedToggle.IsChecked == true)
            {
                // Select this type and uncheck others
                SelectModelType(clickedType);
            }
            else
            {
                // Toggle was unchecked - clear selection
                _selectedModelType = ModelType.None;
            }
        }

        /// <summary>
        /// Handles destination text changes to clear model type selection.
        /// </summary>
        private void OnDestinationTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_suppressDestinationTextChanged)
            {
                return;
            }

            // When user types in destination, clear model type selection
            if (!string.IsNullOrEmpty(_destinationTextBox.Text))
            {
                ClearModelTypeSelection();
            }
        }

        /// <summary>
        /// Gets the effective destination based on model type selection or custom input.
        /// </summary>
        private string GetEffectiveDestination()
        {
            // Custom destination takes precedence
            var customDestination = _destinationTextBox.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(customDestination))
            {
                return customDestination;
            }

            // Use model type destination if selected
            if (_selectedModelType != ModelType.None && 
                ModelTypeDestinations.TryGetValue(_selectedModelType, out var typeDestination))
            {
                return typeDestination;
            }

            return string.Empty;
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
            var destination = GetEffectiveDestination();

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

            // Initialize the selected VRAM profile from the model
            if (model.VramProfile.HasValue)
            {
                // Convert enum like VRAM_8GB to display format like "8GB"
                var enumName = model.VramProfile.Value.ToString(); // e.g., "VRAM_8GB"
                var displayValue = enumName.Replace("VRAM_", ""); // e.g., "8GB"
                
                // Check if this profile exists in the available profiles
                if (AvailableVramProfiles.Contains(displayValue))
                {
                    _selectedVramProfile = displayValue;
                }
                else
                {
                    // Try to find a matching profile (e.g., "8GB" might be stored as "8+GB")
                    var numericPart = displayValue.Replace("GB", "");
                    var matchingProfile = AvailableVramProfiles.FirstOrDefault(p => 
                        p.Replace("+GB", "").Replace("GB", "").Replace("+", "") == numericPart);
                    _selectedVramProfile = matchingProfile ?? "None";
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
                // Remove "GB" and "+" suffixes and parse the profile value
                var profileValue = SelectedVramProfile
                    .Replace("+GB", "")
                    .Replace("GB", "")
                    .Replace("+", "")
                    .Trim();
                    
                _model.VramProfile = profileValue switch
                {
                    "4" => VramProfile.VRAM_4GB,
                    "6" => VramProfile.VRAM_6GB,
                    "8" => VramProfile.VRAM_8GB,
                    "12" => VramProfile.VRAM_12GB,
                    "16" => VramProfile.VRAM_16GB,
                    "24" => VramProfile.VRAM_24GB,
                    "32" => VramProfile.VRAM_32GB,
                    "48" => VramProfile.VRAM_48GB,
                    "64" => VramProfile.VRAM_64GB,
                    _ => null
                };
            }

            return _model;
        }
    }
}
