using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// Text whose characters roll like a counter when they change: the old one slides up and fades, the new one comes up
/// from below. Only the characters that changed move, so a countdown rolls just its last digit most seconds.
/// </summary>
public sealed partial class RollingText : StackPanel
{
    private static readonly TimeSpan RollDuration = TimeSpan.FromMilliseconds(260);

    private readonly List<Grid> _cells = [];
    private string _text = string.Empty;

    public RollingText()
    {
        Orientation = Orientation.Horizontal;

        // Rolling characters stay inside their own line instead of sliding over the text above and below.
        SizeChanged += (_, e) => Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, e.NewSize.Width, e.NewSize.Height) };
    }

    public double TextSize { get; set; } = 34;

    public Brush? TextBrush { get; set; }

    /// <summary>Hero numbers are bold; smaller rolling text is semibold.</summary>
    public bool Bold { get; set; }

    /// <summary>Changes the text; with <paramref name="roll"/> false it simply swaps (reduced motion, first show).</summary>
    public void SetText(string text, bool roll)
    {
        if (text == _text)
        {
            return;
        }

        // A different shape (say 9:59 to 10:00) rebuilds the cells; the same shape rolls only what changed.
        if (text.Length != _text.Length)
        {
            Children.Clear();
            _cells.Clear();
            foreach (char c in text)
            {
                Grid cell = new() { VerticalAlignment = VerticalAlignment.Center };
                cell.Children.Add(MakeGlyph(c));
                _cells.Add(cell);
                Children.Add(cell);
            }

            _text = text;
            return;
        }

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != _text[i])
            {
                Roll(_cells[i], text[i], roll);
            }
        }

        _text = text;
    }

    private void Roll(Grid cell, char next, bool animate)
    {
        TextBlock incoming = MakeGlyph(next);
        if (!animate)
        {
            cell.Children.Clear();
            cell.Children.Add(incoming);
            return;
        }

        UIElement? outgoing = cell.Children.Count > 0 ? cell.Children[^1] : null;
        cell.Children.Add(incoming);
        float distance = (float)(TextSize * 0.7);
        Slide(incoming, distance, 0, 0, 1);
        if (outgoing is not null)
        {
            Slide(outgoing, 0, -distance, 1, 0, () => cell.Children.Remove(outgoing));
        }
    }

    /// <summary>One character in the display face, with fixed-width digits so the number never shifts as it rolls.</summary>
    private TextBlock MakeGlyph(char c)
    {
        var glyph = new TextBlock
        {
            Text = c.ToString(),
            FontSize = TextSize,
            FontWeight = Bold ? FontWeights.Bold : FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            Foreground = TextBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(glyph, FontNumeralAlignment.Tabular);
        return glyph;
    }

    private static void Slide(UIElement element, float fromY, float toY, float fromOpacity, float toOpacity, Action? done = null)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        Visual visual = ElementCompositionPreview.GetElementVisual(element);
        Compositor compositor = visual.Compositor;
        CubicBezierEasingFunction ease = compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.8f), new Vector2(0.2f, 1));
        CompositionScopedBatch batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

        Vector3KeyFrameAnimation move = compositor.CreateVector3KeyFrameAnimation();
        move.InsertKeyFrame(0, new Vector3(0, fromY, 0));
        move.InsertKeyFrame(1, new Vector3(0, toY, 0), ease);
        move.Duration = RollDuration;
        visual.StartAnimation("Translation", move);

        ScalarKeyFrameAnimation fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0, fromOpacity);
        fade.InsertKeyFrame(1, toOpacity, ease);
        fade.Duration = RollDuration;
        visual.StartAnimation("Opacity", fade);

        batch.End();
        if (done is not null)
        {
            batch.Completed += (_, _) => done();
        }
    }
}
