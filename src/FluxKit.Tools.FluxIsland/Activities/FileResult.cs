namespace FluxKit.Tools.FluxIsland.Activities;

/// <summary>What a drop zone did with the dropped files, for the moment that confirms it.</summary>
public enum FileResult
{
    PathCopied,
    Zipped,
    Converted,
    Resized,
    Failed,
}
