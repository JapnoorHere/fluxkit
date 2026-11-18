using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace FluxKit.App.Modules.FluxIsland;

/// <summary>
/// The command bar. It parses what is typed on every key with <see cref="CommandParser"/>, previews the result, and
/// raises <see cref="Submitted"/> on Enter. It never runs anything itself, so the same bar works in previews.
/// </summary>
public sealed partial class CommandBar : UserControl
{
    private IslandCommand? _command;
    private CommandPreview? _shown;

    public CommandBar()
    {
        InitializeComponent();
        var secondary = new SolidColorBrush(IslandPalette.TextSecondary);
        PromptGlyph.Foreground = secondary;
        EscHintText.Foreground = secondary;
        ResultDetail.Foreground = secondary;
        Input.TextChanged += (_, _) => Preview();
        Input.PreviewKeyDown += OnKeyDown;
        Preview();
    }

    /// <summary>False when effects are reduced: results swap without motion.</summary>
    public bool Animate { get; set; } = true;

    /// <summary>Enter was pressed on something the bar understood.</summary>
    public event EventHandler<IslandCommand>? Submitted;

    /// <summary>Esc was pressed: close the bar.</summary>
    public event EventHandler? Dismissed;

    /// <summary>Down was pressed on an empty bar: show the full panel instead.</summary>
    public event EventHandler? PanelRequested;

    /// <summary>Empties the field and puts the caret in it, ready to type.</summary>
    public void Reset()
    {
        Input.Text = string.Empty;
        Preview();
    }

    public void FocusInput() => Input.Focus(FocusState.Keyboard);

    /// <summary>Types <paramref name="text"/> into the field, for previews and screenshots.</summary>
    public void ShowText(string text) => Input.Text = text;

    /// <summary>Briefly shows a confirmation in the result line, e.g. "Copied 99" after a sum.</summary>
    public void Confirm(string text)
    {
        Swatch.Visibility = Visibility.Collapsed;
        ResultBadge.DrawCheck(IslandPalette.Green, Animate);
        _shown = null;
        ResultTitle.Text = text;
        ResultDetail.Text = Strings.Get("Island_Cmd_KeepTyping");
        EnterHint.Visibility = Visibility.Collapsed;
        Swatch.Visibility = Visibility.Collapsed;
        Input.SelectAll();
    }

    private void Preview()
    {
        _command = CommandParser.Parse(Input.Text);
        if (_command is null)
        {
            bool empty = string.IsNullOrWhiteSpace(Input.Text);
            ShowPreview(new CommandPreview(
                empty ? "\xE82F" : "\xE9CE",
                Strings.Get(empty ? "Island_Cmd_Examples" : "Island_Cmd_NoMatch"),
                Strings.Get(empty ? "Island_Cmd_ExamplesDetail" : "Island_Cmd_NoMatchDetail"),
                IslandPalette.TextSecondary));
            EnterHint.Visibility = Visibility.Collapsed;
            return;
        }

        ShowPreview(CommandPresenter.Describe(_command));
        EnterHintText.Text = Strings.Get(_command.Kind is CommandKind.Calculate or CommandKind.ShowColor ? "Island_Cmd_EnterCopy" : "Island_Cmd_EnterRun");
        EnterHint.Visibility = Visibility.Visible;
    }

    private void ShowPreview(CommandPreview preview)
    {
        if (preview.Glyph != _shown?.Glyph || preview.Accent != _shown?.Accent)
        {
            // A new kind of result: its badge pops in, in its own colour (plan I13).
            ResultBadge.Show(preview.Glyph, preview.Accent);
            ResultBadge.Pop(Animate);
        }

        _shown = preview;
        ResultTitle.Text = preview.Title;
        ResultDetail.Text = preview.Detail ?? string.Empty;
        ResultDetail.Visibility = preview.Detail is null ? Visibility.Collapsed : Visibility.Visible;
        Swatch.Visibility = preview.Swatch is null ? Visibility.Collapsed : Visibility.Visible;
        if (preview.Swatch is { } swatch)
        {
            Swatch.Fill = new SolidColorBrush(swatch);
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter when _command is not null:
                e.Handled = true;
                Submitted?.Invoke(this, _command);
                break;
            case VirtualKey.Escape:
                e.Handled = true;
                Dismissed?.Invoke(this, EventArgs.Empty);
                break;
            case VirtualKey.Down when Input.Text.Length == 0:
                e.Handled = true;
                PanelRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }
}
