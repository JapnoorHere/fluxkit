using FluxKit.Core.Tools;
using FluxKit.FluxIsland.Tests.Fakes;
using FluxKit.Tools.FluxIsland;
using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Activities;
using Microsoft.Extensions.Time.Testing;
using Serilog;

namespace FluxKit.FluxIsland.Tests;

public sealed class FluxIslandToolTests : IAsyncLifetime
{
    private readonly FakeTimeProvider _time = new();
    private readonly FakeSettings _settings = new();
    private readonly FakeIslandView _view = new();
    private readonly FakeMedia _media = new();
    private readonly FakePower _power = new(new PowerSnapshot(true, 50, false));
    private readonly FakeNetwork _network = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeFullScreen _fullScreen = new();
    private readonly FakeAudio _audio = new();
    private readonly FakePrivacy _privacy = new();
    private readonly FluxIslandTool _tool;

    public FluxIslandToolTests()
    {
        var sources = new IslandSources
        {
            Media = () => _media,
            Power = () => _power,
            Network = () => _network,
            Clipboard = () => _clipboard,
            FullScreen = () => _fullScreen,
            Audio = () => _audio,
            Privacy = () => _privacy,
        };
        _tool = new FluxIslandTool(_settings, _ => _view, sources, _time, new LoggerConfiguration().CreateLogger());
    }

    public Task InitializeAsync() => _tool.StartAsync(CancellationToken.None);

    public async Task DisposeAsync() => await _tool.DisposeAsync();

    [Fact]
    public void Starts_running_and_renders_an_empty_island()
    {
        Assert.Equal(ToolStatus.Running, _tool.State.Status);
        Assert.Null(_view.Last.Primary);
        Assert.False(_view.Last.Hidden);
    }

    [Fact]
    public void Volume_change_from_anywhere_shows_the_level_and_mute_shows_zero()
    {
        _audio.ChangeVolume(64, muted: false);

        Assert.Equal(ActivityKind.Volume, _view.Last.Primary?.Kind);
        Assert.Equal(64, _view.Last.Primary?.Value);

        _audio.ChangeVolume(64, muted: true);

        Assert.Equal(0, _view.Last.Primary?.Value);
    }

    [Fact]
    public async Task Volume_changes_are_ignored_when_the_setting_is_off()
    {
        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.ShowVolumeChanges = false;
            return s;
        });

        _audio.ChangeVolume(30, muted: false);

        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public void Switching_the_output_names_the_new_device()
    {
        _audio.SwitchOutput("Headphones");

        Assert.Equal(ActivityKind.AudioDevice, _view.Last.Primary?.Kind);
        Assert.Equal("Headphones", _view.Last.Primary?.Text);
    }

    [Fact]
    public void Toggling_the_mic_flashes_its_state_then_goes_away_when_nothing_listens()
    {
        _tool.ToggleMicMute();

        Assert.True(_audio.MicMuted);
        Assert.True(_view.Last.MicMuted);
        Assert.Equal(ActivityKind.MicMuted, _view.Last.Primary?.Kind);
        Assert.True(_view.Last.Primary?.Flag);

        _time.Advance(FluxIslandTool.MicToggleDuration);

        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public void A_muted_mic_stays_on_the_island_while_an_app_is_listening()
    {
        _tool.ToggleMicMute();
        _privacy.Set(microphone: true);
        _time.Advance(FluxIslandTool.MicToggleDuration);

        Assert.Equal(ActivityKind.MicMuted, _view.Last.Primary?.Kind);

        _privacy.Set(microphone: false);

        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public void Copy_shows_a_short_copied_alert_that_runs_out()
    {
        _clipboard.Copy(ClipboardKind.Image);

        Assert.Equal(ActivityKind.Copied, _view.Last.Primary?.Kind);
        Assert.Equal(ClipboardKind.Image, _view.Last.Primary?.Clipboard);

        _time.Advance(ActivityArbiter.DurationOf(ActivityKind.Copied));

        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public void Playing_music_stays_and_paused_music_steps_aside_after_a_while()
    {
        var song = new MediaInfo("Song", "Artist", true, TimeSpan.Zero, TimeSpan.FromMinutes(3), _time.GetUtcNow(), true, true, true);

        _media.Set(song);
        Assert.Equal(ActivityKind.Media, _view.Last.Primary?.Kind);

        _media.Set(song with { IsPlaying = false });
        _time.Advance(FluxIslandTool.PausedMediaLinger - TimeSpan.FromSeconds(1));
        Assert.Equal(ActivityKind.Media, _view.Last.Primary?.Kind);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(_view.Last.Primary);
        Assert.NotNull(_view.Last.Media);
    }

    [Fact]
    public void Plugging_in_the_charger_raises_an_alert_with_the_percentage()
    {
        _power.Set(new PowerSnapshot(true, 64, true));

        Assert.Equal(ActivityKind.PluggedIn, _view.Last.Primary?.Kind);
        Assert.Equal(64, _view.Last.Primary?.Value);
    }

    [Fact]
    public void Going_offline_and_back_raises_two_alerts()
    {
        _network.Set(false);
        Assert.Equal(ActivityKind.Offline, _view.Last.Primary?.Kind);

        _network.Set(true);
        Assert.Equal(ActivityKind.Online, _view.Last.Primary?.Kind);
    }

    [Fact]
    public void Full_screen_hides_the_island()
    {
        _fullScreen.Set(true);
        Assert.True(_view.Last.Hidden);

        _fullScreen.Set(false);
        Assert.False(_view.Last.Hidden);
    }

    [Fact]
    public void Finished_timer_raises_timer_done()
    {
        _tool.StartTimer(TimeSpan.FromMinutes(1));
        Assert.Equal(ActivityKind.Timer, _view.Last.Primary?.Kind);

        _time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(ActivityKind.TimerDone, _view.Last.Primary?.Kind);
        Assert.False(_view.Last.Timer?.IsActive);
    }

    [Fact]
    public void Files_on_the_shelf_show_as_a_lasting_activity()
    {
        _tool.AddToShelf([@"C:\a.txt", @"C:\b.txt"]);

        Assert.Equal(ActivityKind.Shelf, _view.Last.Primary?.Kind);
        Assert.Equal(2, _view.Last.Primary?.Value);

        _tool.ClearShelf();
        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public async Task Turning_a_feature_off_stops_its_source()
    {
        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.ShowClipboard = false;
            return s;
        });

        Assert.True(_clipboard.Disposed);
    }

    [Fact]
    public async Task Stopping_closes_the_view()
    {
        await _tool.StopAsync(CancellationToken.None);

        Assert.True(_view.Closed);
        Assert.Equal(ToolStatus.Disabled, _tool.State.Status);
    }
}
