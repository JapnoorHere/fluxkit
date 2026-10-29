using FluxKit.Tools.DragToSwitch;
using FluxKit.Tools.DragToSwitch.Interaction;

namespace FluxKit.DragToSwitch.Tests.Fakes;

internal sealed class FakeView : ISelectorView
{
    public int ShowCount { get; private set; }

    public int HideCount { get; private set; }

    public bool Collapsed { get; private set; }

    public bool OverSelector { get; private set; }

    public IReadOnlyList<CardModel> Cards { get; private set; } = [];

    public Dictionary<int, (CardVisualState State, string? Message)> CardStates { get; } = [];

    public DragKind? Kind { get; private set; }

    public void Show(IReadOnlyList<CardModel> cards, SelectorGeometry geometry, DragToSwitchSettings settings, DragKind kind)
    {
        ShowCount++;
        Kind = kind;
        Cards = cards;
        Collapsed = false;
    }

    public void UpdateCards(IReadOnlyList<CardModel> cards, SelectorGeometry geometry) => Cards = cards;

    public void SetCardState(int? index, CardVisualState state, TimeSpan delay, string? message = null)
    {
        if (index is int i)
        {
            CardStates[i] = (state, message);
        }
    }

    public void SetCursorOverSelector(bool over) => OverSelector = over;

    public void Collapse(CardModel destination) => Collapsed = true;

    public void Expand() => Collapsed = false;

    public void Hide() => HideCount++;
}
