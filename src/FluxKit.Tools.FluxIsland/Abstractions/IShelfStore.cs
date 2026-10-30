namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Keeps the shelf's paths between runs. Only paths are stored; the files themselves are never touched.</summary>
public interface IShelfStore
{
    IReadOnlyList<string> Load();

    void Save(IReadOnlyList<string> paths);

    /// <summary>Forgets the saved shelf, for when the user turns keeping it off.</summary>
    void Delete();
}
