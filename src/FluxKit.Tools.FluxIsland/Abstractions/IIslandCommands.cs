using FluxKit.Tools.FluxIsland.Activities;

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

    /// <summary>Adds time to the running timer.</summary>
    void AddTimerTime(TimeSpan extra);

    void StartStopwatch();

    void PauseOrResumeStopwatch();

    void LapStopwatch();

    void ResetStopwatch();

    void AddToShelf(IEnumerable<string> paths);

    void RemoveFromShelf(string path);

    void ClearShelf();

    /// <summary>
    /// Confirms a drop zone's job: <paramref name="path"/> is the new file (if any), <paramref name="count"/> how many
    /// files were dropped. The file's name is shown, never logged.
    /// </summary>
    void ShowFileResult(FileResult result, string? path, int count);

    /// <summary>The island heard you talking while the microphone is muted (during a call). Shown at most every 30 s.</summary>
    void NotifySpeakingWhileMuted();

    /// <summary>Adds a quick note (from the command bar).</summary>
    void AddNote(string text);

    void RemoveNote(string text);

    void ClearNotes();

    void ClearRewind();

    /// <summary>Pins (or unpins) an item in the clipboard history, so clearing keeps it.</summary>
    void PinClip(string text);

    void RemoveClip(string text);

    void ClearClips();

    void StepVolume(int notches);

    /// <summary>Mutes the microphone, or unmutes it if it is muted.</summary>
    void ToggleMicMute();

    /// <summary>Waves away the alert on screen; the next one (or the lasting activity) comes back.</summary>
    void DismissAlert();

    /// <summary>Swaps the pill and the bubble when two lasting activities are running.</summary>
    void SwapActivities();
}
