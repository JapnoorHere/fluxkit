using Microsoft.Win32;

namespace FluxKit.Platform.Startup;

public enum StartupState
{
    Disabled,
    Enabled,

    /// <summary>The entry exists but points at a different FluxKit.exe (the app was moved).</summary>
    StalePath,

    /// <summary>The entry exists but the user turned it off in Task Manager / Settings › Apps › Startup.</summary>
    DisabledByUser,
}

/// <summary>Per-user "launch at sign-in" via HKCU\...\Run. No admin rights, fully transparent to the user.</summary>
public sealed class StartupRegistration(string valueName, string executablePath)
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    public string Command => $"\"{executablePath}\" --background";

    public StartupState GetState()
    {
        using RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKey);
        if (run?.GetValue(valueName) is not string command)
        {
            return StartupState.Disabled;
        }

        using RegistryKey? approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        if (approved?.GetValue(valueName) is byte[] { Length: > 0 } flags && (flags[0] & 0x1) == 1)
        {
            return StartupState.DisabledByUser;
        }

        return string.Equals(command, Command, StringComparison.OrdinalIgnoreCase) ? StartupState.Enabled : StartupState.StalePath;
    }

    public void Enable()
    {
        using RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKey);
        run.SetValue(valueName, Command, RegistryValueKind.String);
    }

    public void Disable()
    {
        using RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        run?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
