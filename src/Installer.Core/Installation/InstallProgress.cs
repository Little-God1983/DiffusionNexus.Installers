namespace Installer.Core.Installation;

public readonly record struct InstallProgress(string StepName, double Percent, bool IsIndeterminate);
