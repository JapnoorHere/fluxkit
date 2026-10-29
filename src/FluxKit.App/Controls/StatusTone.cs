using FluxKit.App.Theming;
using FluxKit.Core.Tools;
using Microsoft.UI.Xaml.Media;

namespace FluxKit.App.Controls;

/// <summary>The colour of a status dot.</summary>
public enum StatusTone
{
    None,
    Neutral,
    Good,
    Caution,
    Critical,
}

/// <summary>Maps tool status to a dot colour and brush.</summary>
internal static class StatusTones
{
    public static StatusTone For(ToolStatus status) => status switch
    {
        ToolStatus.Running => StatusTone.Good,
        ToolStatus.Degraded => StatusTone.Caution,
        ToolStatus.Faulted => StatusTone.Critical,
        ToolStatus.Starting => StatusTone.Neutral,
        _ => StatusTone.None,
    };

    public static Brush? Brush(StatusTone tone) => tone switch
    {
        StatusTone.Good => ThemeResources.Success,
        StatusTone.Caution => ThemeResources.Caution,
        StatusTone.Critical => ThemeResources.Critical,
        StatusTone.Neutral => ThemeResources.Neutral,
        _ => null,
    };
}
