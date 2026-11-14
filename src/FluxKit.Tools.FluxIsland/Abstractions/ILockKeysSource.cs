namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>Caps Lock (or, when false, Num Lock) is now <paramref name="On"/>.</summary>
public readonly record struct LockKeyChange(bool IsCapsLock, bool On);

/// <summary>Caps Lock and Num Lock changes. Only those two keys are ever reported. Events may arrive on any thread.</summary>
public interface ILockKeysSource : IIslandSource
{
    event EventHandler<LockKeyChange>? Changed;
}
