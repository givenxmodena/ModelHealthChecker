using System.IO;
using Autodesk.Revit.DB;
using Modena.RevitAddin.RevitApi;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Extracts model health data directly from the live Revit Document using the Revit API.
/// All Revit API calls run on the calling thread (the Revit dispatcher); no background
/// threads are used. Extraction is split so the UI can render fast data first.
/// </summary>
public class RevitHealthExtractor : IModelHealthExtractor
{
    private readonly Document _doc;
    private readonly PluginConfig _config;

    public RevitHealthExtractor(Document document, PluginConfig? config = null)
    {
        _doc    = document ?? throw new ArgumentNullException(nameof(document));
        _config = config   ?? new PluginConfig();
    }

    /// <summary>Full extraction — fast phase + family sizes combined. Kept for backward compatibility.</summary>
    public Task<DashboardResponse> ExtractAsync(IRevitDocumentContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LogService.Info("RevitHealthExtractor: Starting full extraction.");

        var categories = ExtractCategoryDistribution();
        var warnings   = ExtractWarnings();
        var families   = ExtractFamilySizesCore(ct);
        var checks     = RunHealthChecks();

        var response = BuildResponse(context, categories, warnings, families, checks);
        LogService.Info($"RevitHealthExtractor: Full extraction complete. PassRate={response.Summary?.PassRate}%");
        return Task.FromResult(response);
    }

    /// <summary>
    /// Fast phase: categories, warnings, and health checks only.
    /// Families list is empty — call ExtractFamilySizesAsync separately.
    /// </summary>
    public Task<DashboardResponse> ExtractFastAsync(IRevitDocumentContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LogService.Info("RevitHealthExtractor: Starting fast extraction (no families).");

        var categories = ExtractCategoryDistribution();
        var warnings   = ExtractWarnings();
        var checks     = RunHealthChecks();

        var response = BuildResponse(context, categories, warnings, families: new List<FamilyDto>(), checks);
        LogService.Info($"RevitHealthExtractor: Fast extraction complete. PassRate={response.Summary?.PassRate}%");
        return Task.FromResult(response);
    }

    /// <summary>
    /// Slow phase: opens each family document to measure its size on disk.
    /// Respects cancellation so closing the window mid-load is clean.
    /// </summary>
    public Task<List<FamilyDto>> ExtractFamilySizesAsync(IRevitDocumentContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LogService.Info("RevitHealthExtractor: Starting family size extraction.");
        var families = ExtractFamilySizesCore(ct);
        LogService.Info($"RevitHealthExtractor: Family extraction complete. Count={families.Count}");
        return Task.FromResult(families);
    }

    /// <summary>
    /// On-demand extraction of family file sizes in KB. Opens each editable family via EditFamily(),
    /// saves to a temp .rfa to measure byte length, then closes and deletes the temp file.
    /// Reports progress after each family so the UI can show a running count.
    /// </summary>
    public async Task<List<FamilyDto>> ExtractFamilySizesKbAsync(
        IRevitDocumentContext context,
        Action<int, int>? onProgress,
        CancellationToken ct)
    {
        LogService.Info("RevitHealthExtractor: Starting on-demand family KB extraction.");

        var families = new FilteredElementCollector(_doc)
            .OfClass(typeof(Family))
            .Cast<Family>()
            .Where(f => f.IsEditable)
            .ToList();

        var results = new List<FamilyDto>();
        var total   = families.Count;

        for (int i = 0; i < families.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var family  = families[i];
            int sizeKb  = 0;

            try
            {
                var familyDoc = _doc.EditFamily(family);
                if (familyDoc != null)
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), $"mhc_{Guid.NewGuid():N}.rfa");
                    try
                    {
                        familyDoc.SaveAs(tempPath, new SaveAsOptions { OverwriteExistingFile = true });
                        sizeKb = (int)(new FileInfo(tempPath).Length / 1024);
                    }
                    finally
                    {
                        try { familyDoc.Close(false); } catch { }
                        try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                LogService.Warn($"Family KB extraction skipped for '{family.Name}': {ex.Message}");
            }

            if (sizeKb > 0)
                results.Add(new FamilyDto { Name = family.Name, Kb = sizeKb });

            onProgress?.Invoke(i + 1, total);
            await Task.Yield();
        }

        var sorted = results.OrderByDescending(f => f.Kb).Take(20).ToList();
        LogService.Info($"RevitHealthExtractor: Family KB extraction complete. Count={sorted.Count}");
        return sorted;
    }

    private DashboardResponse BuildResponse(
        IRevitDocumentContext context,
        List<CategoryDto> categories,
        int warnings,
        List<FamilyDto> families,
        List<FailedCheckDto> healthChecks)
    {
        var totalChecks   = healthChecks.Count;
        var failedChecks  = healthChecks.Where(c => c.Count > 0).ToList();
        var passedChecks  = healthChecks.Where(c => c.Count == 0).Select(c => c.Name).ToList();
        var failedCount   = failedChecks.Count;
        var passedCount   = totalChecks - failedCount;
        var passRate      = totalChecks > 0 ? (int)Math.Round(100.0 * passedCount / totalChecks) : 100;
        var totalElements = categories.Sum(c => c.Value);

        return new DashboardResponse
        {
            ModelKey       = BuildModelKey(context),
            ModelName      = context.DocumentTitle ?? "Unknown",
            ProjectName    = ExtractProjectName(context),
            LastUpdatedUtc = DateTime.UtcNow,
            Summary = new SummaryDto
            {
                PassRate         = passRate,
                TotalChecks      = totalChecks,
                PassedChecks     = passedCount,
                FailedChecks     = failedCount,
                ReportOnlyChecks = 0,
                Warnings         = warnings,
                FileSizeMb       = 0,
                ModelElements    = totalElements
            },
            FailedChecks = failedChecks,
            Metrics = new List<MetricDto>
            {
                new() { Name = "Total Elements", Count = totalElements },
                new() { Name = "Warnings",       Count = warnings      },
                new() { Name = "Families",       Count = families.Count }
            },
            Categories   = categories,
            Families     = families,
            PassedChecks = passedChecks
        };
    }

    private List<CategoryDto> ExtractCategoryDistribution()
    {
        try
        {
            return new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .Where(e => e.Category is not null && !string.IsNullOrEmpty(e.Category.Name))
                .GroupBy(e => e.Category!.Name)
                .Select(g => new CategoryDto { Name = g.Key, Value = g.Count() })
                .OrderByDescending(c => c.Value)
                .ToList();
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to extract category distribution.", ex);
            return new List<CategoryDto>();
        }
    }

    private int ExtractWarnings()
    {
        try
        {
            return _doc.GetWarnings()?.Count ?? 0;
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to extract warnings.", ex);
            return 0;
        }
    }

    private List<FamilyDto> ExtractFamilySizesCore(CancellationToken ct)
    {
        // Previous approach (EditFamily + SaveAs) opened every family document, taking
        // 10–30+ minutes on models with many families. Instance count gives the same
        // actionable insight (which families are most used = biggest performance impact)
        // in under a second using two collector passes with no document opens.
        try
        {
            // Pass 1: count placed instances grouped by parent family name.
            ct.ThrowIfCancellationRequested();
            var instancesByFamily = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol?.Family is not null)
                .GroupBy(fi => fi.Symbol!.Family.Name)
                .ToDictionary(g => g.Key, g => g.Count());

            // Pass 2: enumerate all families and attach their instance counts.
            ct.ThrowIfCancellationRequested();
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Select(f => new FamilyDto
                {
                    Name          = f.Name,
                    InstanceCount = instancesByFamily.TryGetValue(f.Name, out var n) ? n : 0
                })
                .OrderByDescending(f => f.InstanceCount)
                .Take(20)
                .ToList();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            LogService.Error("Failed to extract family usage.", ex);
            return new List<FamilyDto>();
        }
    }

    private List<FailedCheckDto> RunHealthChecks()
    {
        var rules   = _config.HealthChecks;
        var results = new List<FailedCheckDto>();

        if (rules.MirroredElements.Enabled)     results.Add(CheckMirroredElements(rules.MirroredElements));
        if (rules.UnplacedRooms.Enabled)        results.Add(CheckUnplacedRooms(rules.UnplacedRooms));
        if (rules.DuplicateTypeNames.Enabled)   results.Add(CheckDuplicateTypeNames(rules.DuplicateTypeNames));
        if (rules.DetailLineItems.Enabled)      results.Add(CheckDetailLineItems(rules.DetailLineItems));
        if (rules.ImportedCadInstances.Enabled) results.Add(CheckImportedCadInstances(rules.ImportedCadInstances));

        return results;
    }

    private FailedCheckDto CheckMirroredElements(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Mirrored)
                .Select(fi => GetId(fi.Id))
                .ToList();

            return new FailedCheckDto
            {
                Name       = "Mirrored Elements",
                Count      = ids.Count,
                ElementIds = ids,
                Category   = "Model Quality",
                Priority   = rule.ResolvePriority(ids.Count),
                Discipline = string.IsNullOrEmpty(rule.Discipline) ? "General" : rule.Discipline!
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Mirrored Elements.", ex);
            return new FailedCheckDto { Name = "Mirrored Elements", Count = 0, Category = "Model Quality", Priority = "LOW", Discipline = string.IsNullOrEmpty(rule.Discipline) ? "General" : rule.Discipline! };
        }
    }

    private FailedCheckDto CheckUnplacedRooms(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Where(r => r.Area <= 0)
                .Select(r => GetId(r.Id))
                .ToList();

            return new FailedCheckDto
            {
                Name       = "Unplaced Rooms",
                Count      = ids.Count,
                ElementIds = ids,
                Category   = "Spatial",
                Priority   = rule.ResolvePriority(ids.Count),
                Discipline = string.IsNullOrEmpty(rule.Discipline) ? "Architecture" : rule.Discipline!
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Unplaced Rooms.", ex);
            return new FailedCheckDto { Name = "Unplaced Rooms", Count = 0, Category = "Spatial", Priority = "LOW", Discipline = string.IsNullOrEmpty(rule.Discipline) ? "Architecture" : rule.Discipline! };
        }
    }

    private FailedCheckDto CheckDuplicateTypeNames(HealthCheckRule rule)
    {
        try
        {
            var types = new FilteredElementCollector(_doc)
                .WhereElementIsElementType()
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .ToList();

            var duplicateNames = types
                .GroupBy(e => e.Name)
                .Where(g => g.Count() > 1)
                .ToList();

            var ids   = duplicateNames.SelectMany(g => g.Skip(1)).Select(e => GetId(e.Id)).ToList();
            var count = duplicateNames.Sum(g => g.Count() - 1);

            return new FailedCheckDto
            {
                Name       = "Duplicate Type Names",
                Count      = count,
                ElementIds = ids,
                Category   = "Naming",
                Priority   = rule.ResolvePriority(count),
                Discipline = string.IsNullOrEmpty(rule.Discipline) ? "General" : rule.Discipline!
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Duplicate Type Names.", ex);
            return new FailedCheckDto { Name = "Duplicate Type Names", Count = 0, Category = "Naming", Priority = "LOW", Discipline = string.IsNullOrEmpty(rule.Discipline) ? "General" : rule.Discipline! };
        }
    }

    private FailedCheckDto CheckDetailLineItems(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Lines)
                .WhereElementIsNotElementType()
                .Select(e => GetId(e.Id))
                .ToList();

            return new FailedCheckDto
            {
                Name       = "Detail Line Items",
                Count      = ids.Count,
                ElementIds = ids,
                Category   = "Performance",
                Priority   = rule.ResolvePriority(ids.Count),
                Discipline = string.IsNullOrEmpty(rule.Discipline) ? "General" : rule.Discipline!
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Detail Line Items.", ex);
            return new FailedCheckDto { Name = "Detail Line Items", Count = 0, Category = "Performance", Priority = "LOW", Discipline = string.IsNullOrEmpty(rule.Discipline) ? "General" : rule.Discipline! };
        }
    }

    private FailedCheckDto CheckImportedCadInstances(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .WhereElementIsNotElementType()
                .Select(e => GetId(e.Id))
                .ToList();

            return new FailedCheckDto
            {
                Name       = "Imported CAD Instances",
                Count      = ids.Count,
                ElementIds = ids,
                Category   = "Performance",
                Priority   = rule.ResolvePriority(ids.Count),
                Discipline = string.IsNullOrEmpty(rule.Discipline) ? "General" : rule.Discipline!
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Imported CAD Instances.", ex);
            return new FailedCheckDto { Name = "Imported CAD Instances", Count = 0, Category = "Performance", Priority = "LOW", Discipline = string.IsNullOrEmpty(rule.Discipline) ? "General" : rule.Discipline! };
        }
    }

    private static long GetId(ElementId id)
    {
#if NET48
        return (long)id.IntegerValue;
#else
        return id.Value;
#endif
    }

    private static string BuildModelKey(IRevitDocumentContext context)
    {
        if (context.IsCloudModel && !string.IsNullOrEmpty(context.ProjectGuid) && !string.IsNullOrEmpty(context.ModelGuid))
            return Slugify($"{context.ProjectGuid}-{context.ModelGuid}");

        var path = !string.IsNullOrEmpty(context.CentralModelPath) ? context.CentralModelPath : context.ModelPath;
        return Slugify($"{path}-{context.DocumentTitle}");
    }

    internal static string ExtractProjectName(IRevitDocumentContext context)
    {
        if (!string.IsNullOrEmpty(context.ModelPath))
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(context.ModelPath);
                if (!string.IsNullOrEmpty(dir))
                    return System.IO.Path.GetFileName(dir);
            }
            catch { }
        }
        return "Unknown Project";
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new System.Text.StringBuilder();
        foreach (var c in input.ToLowerInvariant())
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        var result = sb.ToString();
        while (result.Contains("--"))
            result = result.Replace("--", "-");
        return result.Trim('-');
    }
}
