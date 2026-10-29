namespace FluxKit.Platform.Windows;

/// <summary>Information about the window under a point, used to filter drag sources.</summary>
public sealed record WindowInfo(nint RootWindow, string ProcessName, string ClassName);
