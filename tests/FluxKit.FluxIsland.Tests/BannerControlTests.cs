using FluxKit.FluxIsland.Tests.Fakes;
using FluxKit.Platform.Shell;
using FluxKit.Tools.FluxIsland.Activities;
using FluxKit.Tools.FluxIsland.Sources;
using Microsoft.Win32;
using Serilog;

namespace FluxKit.FluxIsland.Tests;

public sealed class BannerControlTests
{
    private readonly FakeBanners _banners = new();

    [Fact]
    public void Hiding_records_the_old_value_and_restoring_puts_it_back_exactly()
    {
        _banners.Values["Teams!App"] = 1;
        var control = new BannerControl(_banners, _banners);

        control.Apply(["Teams!App", "Mail!App"]);

        Assert.Equal(0, _banners.Values["Teams!App"]);
        Assert.Equal(0, _banners.Values["Mail!App"]);
        Assert.Equal(2, _banners.Saved.Count);

        control.RestoreAll();

        Assert.Equal(1, _banners.Values["Teams!App"]);
        Assert.Null(_banners.Values["Mail!App"]);
        Assert.Empty(_banners.Saved);
        Assert.Empty(control.Hidden);
    }

    [Fact]
    public void Only_apps_dropped_from_the_list_are_restored_and_others_are_never_touched()
    {
        _banners.Values["Other!App"] = 1;
        var control = new BannerControl(_banners, _banners);
        control.Apply(["Teams!App", "Mail!App"]);

        control.Apply(["Mail!App"]);

        Assert.Null(_banners.Values["Teams!App"]);
        Assert.Equal(0, _banners.Values["Mail!App"]);
        Assert.Equal(1, _banners.Values["Other!App"]);
        Assert.Equal(["Mail!App"], control.Hidden);
    }

    [Fact]
    public void A_record_saved_before_a_crash_is_restored_on_the_next_start()
    {
        new BannerControl(_banners, _banners).Apply(["Teams!App"]);

        new BannerControl(_banners, _banners).RestoreAll();

        Assert.Null(_banners.Values["Teams!App"]);
    }

    [Fact]
    public void The_registry_switch_writes_reads_and_removes_the_real_value()
    {
        string root = @"Software\FluxKitTests\" + Guid.NewGuid().ToString("N");
        try
        {
            var registry = new RegistryBannerSwitch(new LoggerConfiguration().CreateLogger(), root);
            Assert.Null(registry.Read("Teams!App"));

            registry.Write("Teams!App", 0);
            Assert.Equal(0, registry.Read("Teams!App"));
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(root + @"\Teams!App"))
            {
                Assert.Equal(RegistryValueKind.DWord, key?.GetValueKind("ShowBanner"));
            }

            registry.Write("Teams!App", null);
            Assert.Null(registry.Read("Teams!App"));
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\FluxKitTests", throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public void A_path_escaping_app_id_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new NotificationBanners(@"Software\FluxKitTests").Get(@"..\..\Run"));
        Assert.Null(new RegistryBannerSwitch(new LoggerConfiguration().CreateLogger(), @"Software\FluxKitTests").Read(""));
    }

    [Fact]
    public void The_record_file_round_trips_and_is_removed_when_empty()
    {
        string file = Path.Combine(Path.GetTempPath(), "fluxkit-banners-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new JsonBannerStore(file, new LoggerConfiguration().CreateLogger());

        store.Save(new Dictionary<string, int?> { ["Teams!App"] = 1, ["Mail!App"] = null });
        IReadOnlyDictionary<string, int?> loaded = store.Load();

        Assert.Equal(1, loaded["Teams!App"]);
        Assert.Null(loaded["Mail!App"]);

        store.Save(new Dictionary<string, int?>());
        Assert.False(File.Exists(file));
    }
}
