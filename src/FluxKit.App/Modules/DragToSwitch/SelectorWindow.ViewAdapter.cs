using FluxKit.Tools.DragToSwitch;
using FluxKit.Tools.DragToSwitch.Interaction;

namespace FluxKit.App.Modules.DragToSwitch;

public sealed partial class SelectorWindow
{
    /// <summary>Marshals coordinator calls (any thread) onto the UI thread without blocking.</summary>
    private sealed class ViewAdapter(SelectorWindow window) : ISelectorView
    {
        public void Show(IReadOnlyList<CardModel> cards, SelectorGeometry geometry, DragToSwitchSettings settings, DragKind kind) =>
            Post(() => window.ShowCore(cards, geometry, settings, kind));

        public void UpdateCards(IReadOnlyList<CardModel> cards, SelectorGeometry geometry) => Post(() => window.UpdateCardsCore(cards, geometry));

        public void SetCardState(int? index, CardVisualState state, TimeSpan delay, string? message = null) =>
            Post(() => window.SetCardStateCore(index, state, delay, message));

        public void SetCursorOverSelector(bool over) => Post(() => window.SetCursorOverSelectorCore(over));

        public void Collapse(CardModel destination) => Post(() => window.CollapseCore(destination));

        public void Expand() => Post(window.ExpandCore);

        public void Hide() => Post(window.HideCore);

        private void Post(Action action) => window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                window._log.Error(ex, "Selector UI update failed");
            }
        });
    }
}
