namespace FluxKit.Utilities.DragToSwitch.Interaction;

/// <summary>Where the coordinator is in a drag session.</summary>
public enum SessionPhase
{
    Idle,
    Armed,
    Suspected,
    Active,
    Switching,
    Collapsed,

    /// <summary>The drag was ignored or cancelled; wait for the button to come up.</summary>
    WaitingForRelease,
}
