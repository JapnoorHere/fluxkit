using System.Runtime.InteropServices;
using System.Text;
using FluxKit.Core.Tools;
using FluxKit.Tools.DragToSwitch.Interaction;
using Microsoft.Win32;

namespace FluxKit.App.Shell;

/// <summary>System details for the About page and the copyable diagnostics report.</summary>
internal static class Diagnostics
{
    /// <summary>For example "Windows 11 24H2, build 26100.2314".</summary>
    public static string OsDescription
    {
        get
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string display = key?.GetValue("DisplayVersion") as string ?? string.Empty;
            object? ubr = key?.GetValue("UBR");
            Version v = Environment.OSVersion.Version;
            string name = v.Build >= 22000 ? "Windows 11" : "Windows 10";
            return $"{name} {display}, build {v.Build}.{ubr}".Trim();
        }
    }

    /// <summary>Redacted, copyable diagnostics: no file names, paths of user files or window titles.</summary>
    public static string Build(AppHost host)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"FluxKit {host.Version} ({RuntimeInformation.ProcessArchitecture})");
        sb.AppendLine($"OS: {OsDescription}");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"Virtual desktops: {host.Desktops.Desktops.Count}, backend: {host.Desktops.BackendDescription}, pin: {host.Desktops.CanPinWindows}");
        foreach (IFluxTool tool in host.Registry.All)
        {
            sb.AppendLine($"Tool {tool.Descriptor.Id}: {tool.State.Status} {tool.State.Detail}");
        }

        SessionSummary? last = host.DragToSwitch.LastSession;
        sb.AppendLine(last is null ? "Last drag session: none" : $"Last drag session: {last.StartedAt:u} source={last.SourceProcess} {last.FromDesktop}->{last.ToDesktop} {last.SwitchMs} ms end={last.End}");
        sb.AppendLine($"Settings: recovered={host.Settings.RecoveredFromCorruption} readOnly={host.Settings.IsReadOnly}");
        sb.AppendLine($"Startup entry: {host.Startup.GetState()}");
        return sb.ToString();
    }
}
