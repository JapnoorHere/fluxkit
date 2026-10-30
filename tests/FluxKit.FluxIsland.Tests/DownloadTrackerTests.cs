using FluxKit.Tools.FluxIsland.Abstractions;
using FluxKit.Tools.FluxIsland.Sources;

namespace FluxKit.FluxIsland.Tests;

public sealed class DownloadTrackerTests
{
    private const string Folder = @"C:\Users\me\Downloads\";
    private readonly DownloadTracker _tracker = new();
    private readonly DateTimeOffset _now = DateTimeOffset.UnixEpoch;

    [Theory]
    [InlineData("report.pdf.crdownload", "report.pdf")]
    [InlineData("movie.mkv.part", "movie.mkv")]
    [InlineData("Unconfirmed 81234.crdownload", "")]
    public void Final_name_drops_the_browser_extension(string partial, string expected)
    {
        Assert.Equal(expected, DownloadTracker.FinalName(Folder + partial));
    }

    [Fact]
    public void A_partial_file_growing_is_reported_at_most_once_a_second()
    {
        DownloadUpdate? first = _tracker.Grew(Folder + "a.zip.crdownload", 1000, _now);
        DownloadUpdate? soon = _tracker.Grew(Folder + "a.zip.crdownload", 2000, _now.AddMilliseconds(400));
        DownloadUpdate? later = _tracker.Grew(Folder + "a.zip.crdownload", 3000, _now + DownloadTracker.ReportEvery);

        Assert.Equal(DownloadStage.Arriving, first?.Stage);
        Assert.Equal("a.zip", first?.Name);
        Assert.Null(soon);
        Assert.Equal(3000, later?.Bytes);
    }

    [Fact]
    public void Plain_files_that_appear_are_not_called_downloads()
    {
        Assert.Null(_tracker.Grew(Folder + "notes.txt", 10, _now));
        Assert.Null(_tracker.Renamed(Folder + "notes.txt", Folder + "notes2.txt", 10, _now));
    }

    [Fact]
    public void Renaming_the_partial_file_to_its_real_name_means_finished()
    {
        _tracker.Grew(Folder + "a.zip.crdownload", 1000, _now);

        DownloadUpdate? done = _tracker.Renamed(Folder + "a.zip.crdownload", Folder + "a.zip", 5000, _now);

        Assert.Equal(DownloadStage.Finished, done?.Stage);
        Assert.Equal("a.zip", done?.Name);
        Assert.Equal(Folder + "a.zip", done?.Path);
        Assert.Equal(0, _tracker.ArrivingCount);
    }

    [Fact]
    public void Picking_the_real_name_while_downloading_moves_the_download_to_its_new_key()
    {
        _tracker.Grew(Folder + "Unconfirmed 1.crdownload", 1000, _now);

        DownloadUpdate? named = _tracker.Renamed(Folder + "Unconfirmed 1.crdownload", Folder + "b.iso.crdownload", 1000, _now);

        Assert.Equal(DownloadStage.Arriving, named?.Stage);
        Assert.Equal("b.iso", named?.Name);
        Assert.Equal(Folder + "Unconfirmed 1.crdownload", named?.Previous);
        Assert.Equal(1, _tracker.ArrivingCount);
    }

    [Fact]
    public void Deleting_a_partial_file_means_cancelled()
    {
        _tracker.Grew(Folder + "a.zip.part", 1000, _now);

        Assert.Equal(DownloadStage.Cancelled, _tracker.Deleted(Folder + "a.zip.part")?.Stage);
        Assert.Null(_tracker.Deleted(Folder + "other.zip"));
    }
}
