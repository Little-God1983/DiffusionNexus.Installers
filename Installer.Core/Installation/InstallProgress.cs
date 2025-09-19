namespace AIKnowledge2Go.Installers.Core.Installation;

public sealed record InstallProgress(double Percent, string Step, bool IsIndeterminate, bool IsCompleted = false);
