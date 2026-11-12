using System.IO.Compression;
using System.Xml.Linq;
using Modena.RevitAddin.Updater;

namespace Modena.RevitAddin.Tests.Properties;

public class RevitArtifactsTests
{
    private static string RepositoryRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ModenaModelHealthChecker.sln")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }
    }

    [Theory]
    [InlineData("2024", "net48-2024")]
    [InlineData("2025", "net8.0-windows-2025")]
    [InlineData("2026", "net8.0-windows-2026")]
    [InlineData("2027", "net10.0-windows-2027")]
    public void EachYearHasAnExactArtifact(string year, string folder)
    {
        Assert.Equal(folder, RevitDeployment.Artifacts[year]);
        Assert.Equal(4, RevitDeployment.Artifacts.Values.Distinct().Count());
    }

    [Fact]
    public void UnsupportedYearsAreRejected() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RevitDeployment.ValidatePayload("unused", "2028"));

    [Fact]
    public void MissingPayloadNeverFallsBackToRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        try
        {
            File.Copy(typeof(RevitArtifactsTests).Assembly.Location, Path.Combine(root, "Modena.RevitAddin.dll"));
            Assert.Throws<FileNotFoundException>(() => RevitDeployment.ValidatePayload(root, "2025"));
        }
        finally { Directory.Delete(root, true); }
    }

    // Integration contract: run Build-Release.ps1 -Version 1.0.5 -SkipVelopack before the suite.
    [Theory]
    [InlineData("2024")]
    [InlineData("2025")]
    [InlineData("2026")]
    [InlineData("2027")]
    public void StagingInstallerAndZipContainMatchingApiBuilds(string year)
    {
        string repo = RepositoryRoot;
        string folder = RevitDeployment.Artifacts[year];
        string package = RevitDeployment.ValidatePayload(Path.Combine(repo, "build-output", "package"), year);
        string staged = RevitDeployment.ValidatePayload(Path.Combine(repo, "publish", "Installer"), year);
        Assert.Equal(File.ReadAllBytes(Path.Combine(staged, "Modena.RevitAddin.dll")),
            File.ReadAllBytes(Path.Combine(package, "Modena.RevitAddin.dll")));
        var installer = File.ReadAllLines(Path.Combine(repo, "installer", "ModenaModelHealthChecker-Setup.iss"));
        var route = Assert.Single(installer.Where(line => line.StartsWith("Source:") && line.Contains($"GetRevitAddInsPath|{year}")));
        Assert.Contains($"Installer\\{folder}\\*", route);
        Assert.Contains($"IsRevitInstalled('{year}')", route);
        string installerText = string.Join("\n", installer);
        Assert.Contains($"CreateAddinManifest('{year}')", installerText);
        Assert.Contains($"Type: filesandordirs; Name: \"{{userappdata}}\\Autodesk\\Revit\\Addins\\{year}\\ModelHealthChecker\"", installerText);

        var archives = Directory.GetFiles(Path.Combine(repo, "releases"), $"ModelHealthChecker-*-Revit{year}.zip");
        var archive = archives.OrderByDescending(File.GetLastWriteTimeUtc).First();
        using var zip = ZipFile.OpenRead(archive);
        var dll = Assert.Single(zip.Entries.Where(entry => entry.FullName.Replace('\\', '/') == "ModelHealthChecker/Modena.RevitAddin.dll"));
        using var stream = dll.Open();
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        Assert.Equal(File.ReadAllBytes(Path.Combine(package, "Modena.RevitAddin.dll")), bytes.ToArray());
        var manifest = XDocument.Load(zip.GetEntry("Modena.RevitAddin.addin")!.Open());
        Assert.Equal("ModelHealthChecker\\Modena.RevitAddin.dll", manifest.Descendants("Assembly").Single().Value);
    }

    [Fact]
    public void RenamingAnotherYearsPayloadIsRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var original = Path.Combine(RepositoryRoot, "build-output", "package", "net8.0-windows-2026");
        var wrong = Path.Combine(root, "net8.0-windows-2025");
        Directory.CreateDirectory(wrong);
        try
        {
            foreach (var source in Directory.GetFiles(original, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(wrong, Path.GetRelativePath(original, source));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination);
            }
            Assert.Throws<InvalidDataException>(() => RevitDeployment.ValidatePayload(root, "2025"));
        }
        finally { Directory.Delete(root, true); }
    }
}
