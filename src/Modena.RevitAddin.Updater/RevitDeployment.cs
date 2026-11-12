using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Modena.RevitAddin.Updater;

internal static class RevitDeployment
{
    internal static readonly IReadOnlyDictionary<string, string> Artifacts =
        new Dictionary<string, string>
        {
            ["2024"] = "net48-2024",
            ["2025"] = "net8.0-windows-2025",
            ["2026"] = "net8.0-windows-2026",
            ["2027"] = "net10.0-windows-2027"
        };

    // Read metadata without loading Autodesk assemblies or requiring Revit to run.
    internal static string ValidatePayload(string packageRoot, string year)
    {
        if (!Artifacts.TryGetValue(year, out var folder))
            throw new ArgumentOutOfRangeException(nameof(year), year, "Unsupported Revit year.");
        var source = Path.Combine(packageRoot, folder);
        foreach (var file in new[] { "Modena.RevitAddin.dll", "Modena.Shared.dll", "Newtonsoft.Json.dll",
                     "Microsoft.Web.WebView2.Core.dll", "Microsoft.Web.WebView2.Wpf.dll", "WebView2Loader.dll",
                     "modena-config.json", "revit-build.json", "Registry/ModelHealthChecker.json" })
            if (!File.Exists(Path.Combine(source, file)))
                throw new FileNotFoundException($"Incomplete Revit {year} payload: {file}", Path.Combine(source, file));

        using var stream = File.OpenRead(Path.Combine(source, "Modena.RevitAddin.dll"));
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        foreach (var name in new[] { "RevitAPI", "RevitAPIUI" })
        {
            var references = metadata.AssemblyReferences.Select(metadata.GetAssemblyReference)
                .Where(reference => metadata.GetString(reference.Name) == name).ToArray();
            if (references.Length != 1 || references[0].Version.Major != int.Parse(year) - 2000)
                throw new InvalidDataException($"Revit {year} payload references the wrong {name}.");
        }
        string expectedFramework = year == "2024" ? ".NETFramework,Version=v4.8" :
            year == "2027" ? ".NETCoreApp,Version=v10.0" : ".NETCoreApp,Version=v8.0";
        bool frameworkMatches = false;
        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference) continue;
            var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference) continue;
            var type = metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if (metadata.GetString(type.Name) != "TargetFrameworkAttribute") continue;
            var value = metadata.GetBlobReader(attribute.Value);
            value.ReadUInt16();
            frameworkMatches = value.ReadSerializedString() == expectedFramework;
        }
        if (!frameworkMatches) throw new InvalidDataException($"Wrong target runtime for Revit {year}.");
        return source;
    }
}
