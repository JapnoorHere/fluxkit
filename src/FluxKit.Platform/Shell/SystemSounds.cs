using System.Runtime.InteropServices;

namespace FluxKit.Platform.Shell;

/// <summary>Which Windows sound to play.</summary>
public enum SoundCue
{
    /// <summary>Windows' reminder sound, for a timer that has ended.</summary>
    Reminder,

    /// <summary>Windows' default notification sound, for things like low battery.</summary>
    Notification,
}

/// <summary>
/// Plays Windows' own event sounds (the ones set in Sound settings), so FluxKit ships no audio files and follows the
/// user's sound scheme, including "No sounds". Returns at once; the sound plays in the background.
/// </summary>
public static partial class SystemSounds
{
    private const uint SndAsync = 0x0001;
    private const uint SndNoDefault = 0x0002;
    private const uint SndAlias = 0x00010000;
    private const uint SndSystem = 0x00200000;

    public static void Play(SoundCue cue) =>
        _ = PlaySound(cue == SoundCue.Reminder ? "Notification.Reminder" : "Notification.Default", 0, SndAlias | SndAsync | SndNoDefault | SndSystem);

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(string sound, nint module, uint flags);
}
