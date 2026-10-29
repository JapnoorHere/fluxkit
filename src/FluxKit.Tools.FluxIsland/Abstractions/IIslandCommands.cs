namespace FluxKit.Tools.FluxIsland.Abstractions;

/// <summary>What the user can do from the island. Safe to call from the UI thread; nothing here blocks.</summary>
public interface IIslandCommands
{
    void PlayPause();

    void NextTrack();

    void PreviousTrack();

    void Seek(TimeSpan position);

    void StartTimer(TimeSpan duration);

    void StartFocus();

    void PauseOrResumeTimer();

    void CancelTimer();

    void AddToShelf(IEnumerable<string> paths);

    void RemoveFromShelf(string path);

    void ClearShelf();

    void StepVolume(int notches);
}
