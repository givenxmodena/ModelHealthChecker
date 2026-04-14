using FsCheck;
using Modena.Shared.DTOs;
using Modena.Shared.Enums;

namespace Modena.RevitAddin.Tests.Generators;

public static class DtoArbitraries
{
    public static Arbitrary<ModelSource> ModelSourceArbitrary() =>
        Gen.Elements(ModelSource.Local, ModelSource.RevitServer, ModelSource.ACCCloud)
           .ToArbitrary();

    public static Arbitrary<ModelIdentity> ModelIdentityArbitrary() =>
        (from docTitle in Arb.Generate<NonEmptyString>()
         from modelPath in Arb.Generate<NonEmptyString>()
         from revitVersion in Arb.Generate<NonEmptyString>()
         from isCloud in Arb.Generate<bool>()
         from projGuid in Gen.Elements(Guid.NewGuid().ToString(), null)
         from modelGuid in Gen.Elements(Guid.NewGuid().ToString(), null)
         from itemId in Gen.Elements(Guid.NewGuid().ToString(), null)
         from versionUrn in Gen.Elements("urn:adsk.wipprod:fs.file:" + Guid.NewGuid().ToString(), null)
         from source in Gen.Elements(ModelSource.Local, ModelSource.RevitServer, ModelSource.ACCCloud)
         select new ModelIdentity
         {
             DocumentTitle = docTitle.Get,
             ModelPath = modelPath.Get,
             RevitVersion = revitVersion.Get,
             IsCloudModel = isCloud,
             ProjectGuid = projGuid,
             ModelGuid = modelGuid,
             ItemId = isCloud ? itemId : null,
             VersionUrn = isCloud ? versionUrn : null,
             ModelSource = source
         }).ToArbitrary();

    public static Arbitrary<SummaryDto> SummaryDtoArbitrary() =>
        (from passRate in Gen.Choose(0, 100)
         from totalChecks in Gen.Choose(0, 10000)
         from passedChecks in Gen.Choose(0, 10000)
         from failedChecks in Gen.Choose(0, 10000)
         from reportOnly in Gen.Choose(0, 10000)
         from warnings in Gen.Choose(0, 10000)
         from fileSizeMb in Gen.Choose(0, 5000)
         from modelElements in Gen.Choose(0, 100000)
         select new SummaryDto
         {
             PassRate = passRate,
             TotalChecks = totalChecks,
             PassedChecks = passedChecks,
             FailedChecks = failedChecks,
             ReportOnlyChecks = reportOnly,
             Warnings = warnings,
             FileSizeMb = fileSizeMb,
             ModelElements = modelElements
         }).ToArbitrary();

    public static Arbitrary<FailedCheckDto> FailedCheckDtoArbitrary() =>
        (from name in Arb.Generate<NonEmptyString>()
         from count in Gen.Choose(0, 10000)
         from category in Arb.Generate<NonEmptyString>()
         from priority in Gen.Elements("HIGH", "MEDIUM", "LOW")
         from discipline in Arb.Generate<NonEmptyString>()
         select new FailedCheckDto
         {
             Name = name.Get,
             Count = count,
             Category = category.Get,
             Priority = priority,
             Discipline = discipline.Get
         }).ToArbitrary();

    public static Arbitrary<MetricDto> MetricDtoArbitrary() =>
        (from name in Arb.Generate<NonEmptyString>()
         from count in Gen.Choose(0, 100000)
         select new MetricDto { Name = name.Get, Count = count }).ToArbitrary();

    public static Arbitrary<CategoryDto> CategoryDtoArbitrary() =>
        (from name in Arb.Generate<NonEmptyString>()
         from value in Gen.Choose(0, 100000)
         select new CategoryDto { Name = name.Get, Value = value }).ToArbitrary();

    public static Arbitrary<FamilyDto> FamilyDtoArbitrary() =>
        (from name in Arb.Generate<NonEmptyString>()
         from kb in Gen.Choose(0, 50000)
         select new FamilyDto { Name = name.Get, Kb = kb }).ToArbitrary();

    public static Arbitrary<DashboardResponse> DashboardResponseArbitrary() =>
        (from modelKey in Arb.Generate<NonEmptyString>()
         from modelName in Arb.Generate<NonEmptyString>()
         from projectName in Arb.Generate<NonEmptyString>()
         from lastUpdated in Arb.Generate<DateTime>()
         from summary in SummaryDtoArbitrary().Generator
         from failedChecks in Gen.ListOf(FailedCheckDtoArbitrary().Generator)
         from metrics in Gen.ListOf(MetricDtoArbitrary().Generator)
         from categories in Gen.ListOf(CategoryDtoArbitrary().Generator)
         from families in Gen.ListOf(FamilyDtoArbitrary().Generator)
         from passedChecks in Gen.ListOf(Arb.Generate<NonEmptyString>())
         select new DashboardResponse
         {
             ModelKey = modelKey.Get,
             ModelName = modelName.Get,
             ProjectName = projectName.Get,
             LastUpdatedUtc = lastUpdated,
             Summary = summary,
             FailedChecks = failedChecks.ToList(),
             Metrics = metrics.ToList(),
             Categories = categories.ToList(),
             Families = families.ToList(),
             PassedChecks = passedChecks.Select(s => s.Get).ToList()
         }).ToArbitrary();
}
