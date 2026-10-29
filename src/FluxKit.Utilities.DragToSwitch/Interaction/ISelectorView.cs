namespace FluxKit.Utilities.DragToSwitch.Interaction;

/// <summary>
/// The selector UI as seen by the coordinator. Implementations marshal to their UI thread and must never block.
/// All geometry is in physical pixels and comes from <see cref="SelectorLayout"/>.
/// </summary>
public interface ISelectorView
{
    /// <summary>Shows the expanded selector; <paramref name="kind"/> picks the wording (file vs. window).</summary>
    void Show(IReadOnlyList<CardModel> cards, SelectorGeometry geometry, DragToSwitchSettings settings, DragKind kind);

    /// <summary>Updates the cards in place (desktops added/removed/renamed during a session).</summary>
    void UpdateCards(IReadOnlyList<CardModel> cards, SelectorGeometry geometry);

    /// <summary>Starts/stops the dwell animation for a card; <paramref name="delay"/> is the full dwell duration.</summary>
    void SetCardState(int? index, CardVisualState state, TimeSpan delay, string? message = null);

    /// <summary>Shows "Release outside the selector to drop" while the cursor is over the selector.</summary>
    void SetCursorOverSelector(bool over);

    void Collapse(CardModel destination);

    void Expand();

    void Hide();
}
