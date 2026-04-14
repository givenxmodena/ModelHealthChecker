using FsCheck;
using FsCheck.Xunit;
using Modena.Shared.DTOs;
using Modena.RevitAddin.Tests.Generators;
using Newtonsoft.Json;

namespace Modena.RevitAddin.Tests.Properties;

/// <summary>
/// Property-based tests for DTO JSON serialization round-trips.
/// </summary>
public class DtoSerializationProperties
{
    // Feature: modena-model-health-checker, Property 13: DashboardResponse JSON round-trip
    // **Validates: Requirements 15.1, 15.2**
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DtoArbitraryProvider) })]
    public bool DashboardResponse_JsonRoundTrip_ProducesEquivalentObject(DashboardResponse original)
    {
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<DashboardResponse>(json);

        if (deserialized is null) return false;

        return original.ModelKey == deserialized.ModelKey
            && original.ModelName == deserialized.ModelName
            && original.ProjectName == deserialized.ProjectName
            && original.LastUpdatedUtc == deserialized.LastUpdatedUtc
            && SummaryEquals(original.Summary, deserialized.Summary)
            && ListEquals(original.FailedChecks, deserialized.FailedChecks, FailedCheckEquals)
            && ListEquals(original.Metrics, deserialized.Metrics, MetricEquals)
            && ListEquals(original.Categories, deserialized.Categories, CategoryEquals)
            && ListEquals(original.Families, deserialized.Families, FamilyEquals)
            && original.PassedChecks.SequenceEqual(deserialized.PassedChecks);
    }

    // Feature: modena-model-health-checker, Property 14: ModelIdentity JSON round-trip
    // **Validates: Requirements 15.3**
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(DtoArbitraryProvider) })]
    public bool ModelIdentity_JsonRoundTrip_ProducesEquivalentObject(ModelIdentity original)
    {
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<ModelIdentity>(json);

        if (deserialized is null) return false;

        return original.DocumentTitle == deserialized.DocumentTitle
            && original.ModelPath == deserialized.ModelPath
            && original.RevitVersion == deserialized.RevitVersion
            && original.IsCloudModel == deserialized.IsCloudModel
            && original.ProjectGuid == deserialized.ProjectGuid
            && original.ModelGuid == deserialized.ModelGuid
            && original.ItemId == deserialized.ItemId
            && original.VersionUrn == deserialized.VersionUrn
            && original.ModelSource == deserialized.ModelSource;
    }

    private static bool SummaryEquals(SummaryDto a, SummaryDto b) =>
        a.PassRate == b.PassRate
        && a.TotalChecks == b.TotalChecks
        && a.PassedChecks == b.PassedChecks
        && a.FailedChecks == b.FailedChecks
        && a.ReportOnlyChecks == b.ReportOnlyChecks
        && a.Warnings == b.Warnings
        && a.FileSizeMb == b.FileSizeMb
        && a.ModelElements == b.ModelElements;

    private static bool FailedCheckEquals(FailedCheckDto a, FailedCheckDto b) =>
        a.Name == b.Name && a.Count == b.Count && a.Category == b.Category
        && a.Priority == b.Priority && a.Discipline == b.Discipline;

    private static bool MetricEquals(MetricDto a, MetricDto b) =>
        a.Name == b.Name && a.Count == b.Count;

    private static bool CategoryEquals(CategoryDto a, CategoryDto b) =>
        a.Name == b.Name && a.Value == b.Value;

    private static bool FamilyEquals(FamilyDto a, FamilyDto b) =>
        a.Name == b.Name && a.Kb == b.Kb;

    private static bool ListEquals<T>(List<T> a, List<T> b, Func<T, T, bool> eq) =>
        a.Count == b.Count && a.Zip(b).All(pair => eq(pair.First, pair.Second));
}
