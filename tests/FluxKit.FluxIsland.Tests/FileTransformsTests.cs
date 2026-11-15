using System.IO.Compression;
using FluxKit.Platform.Shell;

namespace FluxKit.FluxIsland.Tests;

public sealed class FileTransformsTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("fluxkit-transforms-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Free_names_add_a_number_instead_of_replacing_a_file()
    {
        Assert.Equal(Path.Combine(_folder, "a.zip"), FileTransforms.FreeName(_folder, "a.zip"));

        File.WriteAllText(Path.Combine(_folder, "a.zip"), "x");
        File.WriteAllText(Path.Combine(_folder, "a (2).zip"), "x");

        Assert.Equal(Path.Combine(_folder, "a (3).zip"), FileTransforms.FreeName(_folder, "a.zip"));
    }

    [Fact]
    public async Task One_file_zips_to_its_own_name_and_several_to_an_archive_with_folders_inside()
    {
        string report = Write("report.txt", "hello");
        string notes = Write("notes.md", "world");
        string folder = Directory.CreateDirectory(Path.Combine(_folder, "pics")).FullName;
        File.WriteAllText(Path.Combine(folder, "one.txt"), "1");

        string single = await FileTransforms.ZipAsync([report]);
        string many = await FileTransforms.ZipAsync([report, notes, folder]);

        Assert.Equal("report.zip", Path.GetFileName(single));
        Assert.Equal("Archive.zip", Path.GetFileName(many));
        using ZipArchive zip = ZipFile.OpenRead(many);
        Assert.Equal(["notes.md", "pics/one.txt", "report.txt"], zip.Entries.Select(e => e.FullName.Replace(Path.DirectorySeparatorChar, '/')).Order());
        Assert.Equal("hello", File.ReadAllText(report));
    }

    [Theory]
    [InlineData("photo.JPG", true)]
    [InlineData("shot.png", true)]
    [InlineData("doc.pdf", false)]
    [InlineData("folder", false)]
    public void Images_are_recognised_by_type(string name, bool image) => Assert.Equal(image, FileTransforms.IsImage(name));

    private string Write(string name, string text)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, text);
        return path;
    }
}
