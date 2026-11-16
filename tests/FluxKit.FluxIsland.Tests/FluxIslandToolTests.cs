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
    private readonly FakeDesktops _desktops = new();
    private readonly FakeFileMoments _files = new();
    private readonly FakeBrightness _brightness = new();
    private readonly FakeShelfStore _store = new();
    private readonly FakeNotesStore _notesStore = new();
    private readonly FakeLockKeys _lockKeys = new();
    private readonly FakeDevices _devices = new();
    private readonly FakeHotkey _hotkey = new();
    private HotkeyGesture? _registered;
    private readonly FluxIslandTool _tool;

    public FluxIslandToolTests()
    {
        var sources = new IslandSources
        {
            Media = () => _media,
            Power = () => _power,
            Network = () => _network,
            Clipboard = _ => _clipboard,
            FullScreen = () => _fullScreen,
            Audio = () => _audio,
            Privacy = () => _privacy,
            Desktops = () => _desktops,
            Files = (_, _) => _files,
            Brightness = () => _brightness,
            LockKeys = () => _lockKeys,
            Devices = () => _devices,
            Hotkey = g =>
            {
                _registered = g;
                return _hotkey;
            },
            ShelfStore = _store,
            NotesStore = _notesStore,
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
    public void Landing_on_another_desktop_names_it_and_where_it_sits()
    {
        _desktops.Land(2, 4, "Work");

        IslandActivity? shown = _view.Last.Primary;
        Assert.Equal(ActivityKind.DesktopSwitched, shown?.Kind);
        Assert.Equal("Work", shown?.Text);
        Assert.Equal(2, shown?.Value);
        Assert.Equal(4, shown?.Total);

        _time.Advance(ActivityArbiter.DurationOf(ActivityKind.DesktopSwitched));

        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public void A_download_stays_while_it_arrives_then_says_it_is_done()
    {
        _files.Download(new DownloadUpdate("k", DownloadStage.Arriving, "big.iso", 50L * 1024 * 1024));

        Assert.Equal(ActivityKind.Download, _view.Last.Primary?.Kind);
        Assert.Equal("big.iso", _view.Last.Primary?.Text);
        Assert.Equal(50, _view.Last.Primary?.Value);

        _files.Download(new DownloadUpdate("k", DownloadStage.Finished, "big.iso", 0, @"C:\d\big.iso"));

        Assert.Equal(ActivityKind.DownloadDone, _view.Last.Primary?.Kind);
        Assert.Equal(@"C:\d\big.iso", _view.Last.Primary?.Path);

        _time.Advance(ActivityArbiter.DurationOf(ActivityKind.DownloadDone));

        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public void A_download_that_stalls_drops_off_by_itself()
    {
        _files.Download(new DownloadUpdate("k", DownloadStage.Arriving, string.Empty, 1));

        Assert.Null(_view.Last.Primary?.Text);

        _time.Advance(FluxIslandTool.StalledDownload);

        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public void A_screenshot_shows_its_name_and_path()
    {
        _files.Screenshot(@"C:\p\Screenshot 1.png");

        Assert.Equal(ActivityKind.Screenshot, _view.Last.Primary?.Kind);
        Assert.Equal("Screenshot 1.png", _view.Last.Primary?.Text);
        Assert.Equal(@"C:\p\Screenshot 1.png", _view.Last.Primary?.Path);
    }

    [Fact]
    public void Copied_text_is_not_looked_at_unless_smart_copy_is_on()
    {
        _clipboard.Copy(ClipboardKind.Text, "https://example.com/page");

        Assert.Equal(SmartText.None, _view.Last.Primary?.Smart);
        Assert.Null(_view.Last.Primary?.Path);
    }

    [Fact]
    public async Task Smart_copy_offers_to_open_a_copied_link()
    {
        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.SmartCopy = true;
            return s;
        });

        _clipboard.Copy(ClipboardKind.Text, "  https://www.example.com/page  ");

        IslandActivity? shown = _view.Last.Primary;
        Assert.Equal(SmartText.Link, shown?.Smart);
        Assert.Equal("example.com", shown?.Text);
        Assert.Equal("https://www.example.com/page", shown?.Path);

        _time.Advance(FluxIslandTool.SmartCopyDuration);

        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public void Brightness_changes_show_the_level()
    {
        _brightness.Set(70);

        Assert.Equal(ActivityKind.Brightness, _view.Last.Primary?.Kind);
        Assert.Equal(70, _view.Last.Primary?.Value);
    }

    [Fact]
    public void Battery_saver_turning_on_says_so_with_the_time_left()
    {
        _power.Set(new PowerSnapshot(true, 50, false, SaverOn: true, TimeLeft: TimeSpan.FromMinutes(95)));

        IslandActivity? shown = _view.Last.Primary;
        Assert.Equal(ActivityKind.BatterySaver, shown?.Kind);
        Assert.True(shown?.Flag);
        Assert.Equal(95, shown?.Total);
    }

    [Fact]
    public void Plugging_in_carries_the_time_until_full()
    {
        _power.Set(new PowerSnapshot(true, 50, false));
        _time.Advance(TimeSpan.FromMinutes(1));

        _power.Set(new PowerSnapshot(true, 50, true, TimeLeft: TimeSpan.FromMinutes(44.2)));

        Assert.Equal(ActivityKind.PluggedIn, _view.Last.Primary?.Kind);
        Assert.Equal(45, _view.Last.Primary?.Total);
    }

    [Fact]
    public void The_shelf_is_saved_as_it_changes()
    {
        _tool.AddToShelf([@"C:\a.txt", @"C:\b.txt"]);
        _tool.RemoveFromShelf(@"C:\a.txt");

        Assert.Equal([@"C:\b.txt"], _store.Saved);
    }

    [Fact]
    public async Task Turning_off_keep_shelf_forgets_the_saved_list()
    {
        _tool.AddToShelf([@"C:\a.txt"]);

        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.KeepShelf = false;
            return s;
        });
        _tool.AddToShelf([@"C:\b.txt"]);

        Assert.True(_store.Deleted);
        Assert.Null(_store.Saved);
        Assert.Equal(2, _view.Last.Shelf.Count);
    }

    [Fact]
    public async Task A_saved_shelf_comes_back_on_start()
    {
        var store = new FakeShelfStore { Saved = [@"C:\kept.txt"] };
        var view = new FakeIslandView();
        await using var tool = new FluxIslandTool(_settings, _ => view, new IslandSources { ShelfStore = store }, _time, new LoggerConfiguration().CreateLogger());

        await tool.StartAsync(CancellationToken.None);

        Assert.Equal([@"C:\kept.txt"], view.Last.Shelf);
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

    [Fact]
    public void Caps_lock_and_num_lock_show_their_new_state()
    {
        _lockKeys.Toggle(capsLock: true, on: true);

        Assert.Equal(ActivityKind.CapsLock, _view.Last.Primary?.Kind);
        Assert.True(_view.Last.Primary?.Flag);

        _lockKeys.Toggle(capsLock: false, on: false);

        Assert.Equal(ActivityKind.NumLock, _view.Last.Primary?.Kind);
        Assert.False(_view.Last.Primary?.Flag);
    }

    [Fact]
    public async Task Lock_keys_are_ignored_when_the_setting_is_off()
    {
        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.ShowLockKeys = false;
            return s;
        });

        _lockKeys.Toggle(capsLock: true, on: true);

        Assert.Null(_view.Last.Primary);
    }

    [Fact]
    public void Bluetooth_devices_show_their_name_when_they_connect_and_disconnect()
    {
        _devices.Change("Galaxy Buds", connected: true);

        Assert.Equal(ActivityKind.DeviceConnected, _view.Last.Primary?.Kind);
        Assert.Equal("Galaxy Buds", _view.Last.Primary?.Text);

        _devices.Change("Galaxy Buds", connected: false);

        Assert.Equal(ActivityKind.DeviceDisconnected, _view.Last.Primary?.Kind);
    }

    [Fact]
    public void The_shortcut_is_registered_from_settings_and_toggles_the_panel()
    {
        Assert.Equal(HotkeyGesture.Default, _registered?.ToString());

        _hotkey.Press();

        Assert.Equal(1, _view.PanelToggles);
    }

    [Fact]
    public async Task An_empty_shortcut_registers_nothing()
    {
        _registered = null;
        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.Hotkey = string.Empty;
            return s;
        });

        Assert.Null(_registered);
    }

    [Fact]
    public void Notes_from_the_command_bar_show_in_the_scene_and_are_saved()
    {
        _tool.AddNote("buy milk");
        _tool.AddNote("call Sam");

        Assert.Equal(["call Sam", "buy milk"], _view.Last.Notes.Select(n => n.Text));
        Assert.Equal(2, _notesStore.Saved.Count);

        _tool.RemoveNote("buy milk");

        Assert.Single(_view.Last.Notes);
        Assert.Single(_notesStore.Saved);
    }

    [Fact]
    public async Task Turning_keep_notes_off_deletes_the_saved_notes()
    {
        _tool.AddNote("secret");

        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.KeepNotes = false;
            return s;
        });

        Assert.True(_notesStore.Deleted);
        Assert.Single(_view.Last.Notes);
    }

    [Fact]
    public void A_call_app_using_the_microphone_shows_a_call_that_ends_with_it()
    {
        _privacy.Set(microphone: true, call: true);

        IslandActivity? call = _view.Last.Primary;
        Assert.Equal(ActivityKind.Call, call?.Kind);
        Assert.Equal(_time.GetUtcNow(), call?.Since);

        _time.Advance(TimeSpan.FromMinutes(3));
        _privacy.Set(microphone: true, call: true);
        Assert.Equal(call?.Since, _view.Last.Primary?.Since);

        _privacy.Set(microphone: false);
        Assert.NotEqual(ActivityKind.Call, _view.Last.Primary?.Kind);
    }

    [Fact]
    public void An_app_that_is_not_a_call_app_shows_no_call()
    {
        _privacy.Set(microphone: true, call: false);

        Assert.NotEqual(ActivityKind.Call, _view.Last.Primary?.Kind);
    }

    [Fact]
    public void A_muted_call_shows_the_mute_state_instead_of_the_separate_reminder()
    {
        _audio.SetMicMuted(true);
        _time.Advance(TimeSpan.FromSeconds(5));
        _privacy.Set(microphone: true, call: true);

        Assert.Equal(ActivityKind.Call, _view.Last.Primary?.Kind);
        Assert.True(_view.Last.Primary?.Flag);
        Assert.NotEqual(ActivityKind.MicMuted, _view.Last.Secondary?.Kind);
    }

    [Fact]
    public void The_you_are_muted_nudge_needs_a_muted_call_and_waits_between_nudges()
    {
        _tool.NotifySpeakingWhileMuted();
        Assert.NotEqual(ActivityKind.MicMuted, _view.Last.Primary?.Kind);

        _audio.SetMicMuted(true);
        _time.Advance(TimeSpan.FromSeconds(5));
        _privacy.Set(microphone: true, call: true);
        _tool.NotifySpeakingWhileMuted();
        Assert.Equal(1, _view.Last.Primary?.Value);

        _time.Advance(TimeSpan.FromSeconds(10));
        _tool.NotifySpeakingWhileMuted();
        Assert.Equal(ActivityKind.Call, _view.Last.Primary?.Kind);

        _time.Advance(FluxIslandTool.NudgeInterval);
        _tool.NotifySpeakingWhileMuted();
        Assert.Equal(ActivityKind.MicMuted, _view.Last.Primary?.Kind);
    }

    [Fact]
    public void Finished_downloads_and_screenshots_are_kept_for_rewind()
    {
        _files.Screenshot(@"C:shots.png");

        Assert.Equal(ActivityKind.Screenshot, Assert.Single(_view.Last.Rewind).Activity.Kind);

        _tool.ClearRewind();

        Assert.Empty(_view.Last.Rewind);
    }

    [Fact]
    public async Task Clipboard_history_keeps_copied_text_only_while_it_is_on_and_forgets_it_when_turned_off()
    {
        _clipboard.Copy(ClipboardKind.Text, "not kept");
        Assert.Empty(_view.Last.Clips);

        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.ClipboardHistory = true;
            return s;
        });
        _clipboard.Copy(ClipboardKind.Text, "kept");
        Assert.Equal("kept", Assert.Single(_view.Last.Clips).Text);

        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.ClipboardHistory = false;
            return s;
        });
        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.ClipboardHistory = true;
            return s;
        });
        Assert.Empty(_view.Last.Clips);
    }

    [Fact]
    public async Task Important_only_lets_through_what_needs_you()
    {
        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.Interruptions = IslandInterruptions.Important;
            return s;
        });

        _clipboard.Copy(ClipboardKind.Text);
        Assert.Null(_view.Last.Primary);

        _tool.StartTimer(TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(ActivityKind.TimerDone, _view.Last.Primary?.Kind);
    }

    [Fact]
    public async Task Nothing_keeps_quick_moments_away_but_lasting_ones_still_show()
    {
        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.Interruptions = IslandInterruptions.Nothing;
            return s;
        });

        _tool.StartTimer(TimeSpan.FromMinutes(1));
        Assert.Equal(ActivityKind.Timer, _view.Last.Primary?.Kind);

        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.NotEqual(ActivityKind.TimerDone, _view.Last.Primary?.Kind);
    }

    [Fact]
    public async Task Changing_only_the_look_redraws_without_restarting_the_watchers()
    {
        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.Look = IslandLook.Glass;
            return s;
        });

        Assert.False(_clipboard.Disposed);
        Assert.Equal(IslandLook.Glass, _view.Last.Settings.Look);

        await _settings.UpdateAsync<FluxIslandSettings>(FluxIslandSettings.Section, s =>
        {
            s.ShowNetwork = false;
            return s;
        });

        Assert.True(_clipboard.Disposed);
    }
}
