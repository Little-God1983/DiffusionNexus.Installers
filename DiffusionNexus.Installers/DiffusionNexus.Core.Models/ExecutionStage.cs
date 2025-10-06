namespace DiffusionNexus.Core.Models
{
    public enum ExecutionStage
    {
        PreInstall,
        PostRepository,
        PostPython,
        PostCustomNodes,
        PostModels,
        PostInstall
    }
}