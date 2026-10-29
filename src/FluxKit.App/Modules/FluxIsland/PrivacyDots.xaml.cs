using FluxKit.Tools.FluxIsland.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>Orange while the microphone is in use and green while the camera is, like the Windows privacy indicator.</summary>
public sealed partial class PrivacyDots : UserControl
{
    private PrivacySnapshot _shown;

    public PrivacyDots()
    {
        InitializeComponent();
        MicrophoneDot.Fill = new SolidColorBrush(IslandPalette.Orange);
        CameraDot.Fill = new SolidColorBrush(IslandPalette.Green);
    }

    public bool IsShowing => Visibility == Visibility.Visible;

    public void Update(PrivacySnapshot privacy, bool springs)
    {
        Show(MicrophoneDot, privacy.MicrophoneInUse, !_shown.MicrophoneInUse, springs);
        Show(CameraDot, privacy.CameraInUse, !_shown.CameraInUse, springs);
        Visibility = privacy.MicrophoneInUse || privacy.CameraInUse ? Visibility.Visible : Visibility.Collapsed;
        _shown = privacy;
    }

    private static void Show(UIElement dot, bool on, bool wasOff, bool springs)
    {
        dot.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on && wasOff)
        {
            IslandMotion.Pop(dot, springs);
        }
    }
}
