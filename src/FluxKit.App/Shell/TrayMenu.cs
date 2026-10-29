using FluxKit.Core.Tools;
using FluxKit.Platform.Shell;
using Microsoft.Win32;

namespace FluxKit.App.Shell;

/// <summary>What the tray icon shows: its menu, its icon and its tooltip.</summary>
internal static class TrayMenu
{
    public static List<TrayMenuItem> Build(AppHost host)
    {
        var items = new List<TrayMenuItem> { new(Strings.Get("Tray_Open"), () => host.ShowMainWindow()), TrayMenuItem.Separator };
        foreach (IFluxTool tool in host.Registry.All)
        {
            string id = tool.Descriptor.Id;
            bool enabled = host.Registry.IsEnabled(id);
            items.Add(new TrayMenuItem(tool.Descriptor.DisplayName, () => _ = host.SetToolEnabledAsync(id, !enabled), Checked: enabled));
        }

        items.Add(TrayMenuItem.Separator);
        items.Add(new TrayMenuItem(Strings.Get("Tray_Settings"), () => host.ShowMainWindow("general")));
        items.Add(new TrayMenuItem(Strings.Get("Tray_OpenLogs"), () => AppHost.OpenFolder(host.LogDirectory)));
        items.Add(TrayMenuItem.Separator);
        items.Add(new TrayMenuItem(Strings.Get("Tray_Quit"), () => _ = host.ExitAsync()));
        return items;
    }

    /// <summary>The "off" icon when no tool is running, otherwise the one that matches the taskbar colour.</summary>
    public static string IconPath(ToolRegistry registry)
    {
        bool enabled = registry.All.Any(u => u.State.IsActive);
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        bool lightTaskbar = key?.GetValue("SystemUsesLightTheme") is int v && v == 1;
        string file = !enabled ? "Tray-trayoff.ico" : lightTaskbar ? "Tray-traylight.ico" : "Tray-traydark.ico";
        return Path.Combine(AppContext.BaseDirectory, "Assets", file);
    }

    /// <summary>"FluxKit" followed by one line per tool, e.g. "Drag-to-Switch: on".</summary>
    public static string Tooltip(ToolRegistry registry) => "FluxKit" + string.Concat(registry.All.Select(u => $"\n{u.Descriptor.DisplayName}: " + u.State.Status switch
    {
        ToolStatus.Running => Strings.Get("Tray_StatusOn"),
        ToolStatus.Degraded => Strings.Get("Tray_StatusLimited"),
        ToolStatus.Faulted => Strings.Get("Tray_StatusStopped"),
        ToolStatus.Starting => Strings.Get("Tray_StatusStarting"),
        _ => Strings.Get("Tray_StatusOff"),
    }));
}
