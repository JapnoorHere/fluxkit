namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>
/// A system event the island listens to. <see cref="Start"/> subscribes and <see cref="IDisposable.Dispose"/>
/// unsubscribes. Events may arrive on any thread; sources never poll.
/// </summary>
public interface IIslandSource : IDisposable
{
    void Start();
}
