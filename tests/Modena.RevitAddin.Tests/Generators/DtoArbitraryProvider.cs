using FsCheck;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Tests.Generators;

/// <summary>
/// FsCheck Arbitrary provider for DTO types. Used via [Property(Arbitrary = ...)] attribute.
/// </summary>
public class DtoArbitraryProvider
{
    public static Arbitrary<ModelIdentity> ModelIdentity => DtoArbitraries.ModelIdentityArbitrary();
    public static Arbitrary<DashboardResponse> DashboardResponse => DtoArbitraries.DashboardResponseArbitrary();
    public static Arbitrary<SummaryDto> SummaryDto => DtoArbitraries.SummaryDtoArbitrary();
    public static Arbitrary<FailedCheckDto> FailedCheckDto => DtoArbitraries.FailedCheckDtoArbitrary();
    public static Arbitrary<MetricDto> MetricDto => DtoArbitraries.MetricDtoArbitrary();
    public static Arbitrary<CategoryDto> CategoryDto => DtoArbitraries.CategoryDtoArbitrary();
    public static Arbitrary<FamilyDto> FamilyDto => DtoArbitraries.FamilyDtoArbitrary();
}
