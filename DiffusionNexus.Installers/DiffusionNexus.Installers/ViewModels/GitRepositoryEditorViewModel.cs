using CommunityToolkit.Mvvm.ComponentModel;

namespace DiffusionNexus.Installers.ViewModels
{
    public sealed partial class GitRepositoryEditorViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private bool _installRequirements = true;

        [ObservableProperty]
        private int _priority;

        [ObservableProperty]
        private bool _canSave;

        public static GitRepositoryEditorViewModel ForNew(int nextPriority)
        {
            return new GitRepositoryEditorViewModel
            {
                Title = "Add Git Repository",
                InstallRequirements = true,
                Priority = nextPriority,
                CanSave = false
            };
        }

        public static GitRepositoryEditorViewModel FromExisting(GitRepositoryItemViewModel repository)
        {
            return new GitRepositoryEditorViewModel
            {
                Title = "Edit Git Repository",
                Name = repository.Name,
                Url = repository.Url,
                InstallRequirements = repository.InstallRequirements,
                Priority = repository.Priority,
                CanSave = !string.IsNullOrWhiteSpace(repository.Name) && !string.IsNullOrWhiteSpace(repository.Url)
            };
        }

        public void ApplyTo(GitRepositoryItemViewModel repository)
        {
            repository.Name = Name.Trim();
            repository.Url = Url.Trim();
            repository.InstallRequirements = InstallRequirements;
        }

        partial void OnNameChanged(string value)
        {
            UpdateCanSave();
        }

        partial void OnUrlChanged(string value)
        {
            UpdateCanSave();
        }

        private void UpdateCanSave()
        {
            CanSave = !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Url);
        }
    }
}
