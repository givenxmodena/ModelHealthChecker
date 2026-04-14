using Autodesk.Revit.DB;
using Modena.RevitAddin.RevitApi;
using Modena.Shared.DTOs;

namespace Modena.RevitAddin.Services;

/// <summary>
/// Extracts model health data directly from the live Revit Document using the Revit API.
/// Computes element counts, warnings, family sizes, and health checks in-process.
/// </summary>
public class RevitHealthExtractor : IModelHealthExtractor
{
    private readonly Document _doc;

    public RevitHealthExtractor(Document document)
    {
        _doc = document ?? throw new ArgumentNullException(nameof(document));
    }

    public Task<DashboardResponse> ExtractAsync(IRevitDocumentContext context, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        LogService.Info("RevitHealthExtractor: Starting extraction from live document.");

        var categories = ExtractCategoryDistribution();
        var warnings = ExtractWarnings();
        var families = ExtractFamilySizes();
        var healthChecks = RunHealthChecks();

        var totalChecks = healthChecks.Count;
        var failedChecks = healthChecks.Where(c => c.Count > 0).ToList();
        var passedChecks = healthChecks.Where(c => c.Count == 0).Select(c => c.Name).ToList();
        var failedCount = failedChecks.Count;
        var passedCount = totalChecks - failedCount;
        var passRate = totalChecks > 0 ? (int)Math.Round(100.0 * passedCount / totalChecks) : 100;
        var totalElements = categories.Sum(c => c.Value);

        var response = new DashboardResponse
        {
            ModelKey = BuildModelKey(context),
            ModelName = context.DocumentTitle ?? "Unknown",
            ProjectName = ExtractProjectName(context),
            LastUpdatedUtc = DateTime.UtcNow,
            Summary = new SummaryDto
            {
                PassRate = passRate,
                TotalChecks = totalChecks,
                PassedChecks = passedCount,
                FailedChecks = failedCount,
                ReportOnlyChecks = 0,
                Warnings = warnings,
                FileSizeMb = 0,
                ModelElements = totalElements
            },
            FailedChecks = failedChecks,
            Metrics = new List<MetricDto>
            {
                new() { Name = "Total Elements", Count = totalElements },
                new() { Name = "Warnings", Count = warnings },
                new() { Name = "Families", Count = families.Count }
            },
            Categories = categories,
            Families = families,
            PassedChecks = passedChecks
        };

        LogService.Info($"RevitHealthExtractor: Extraction complete. PassRate={passRate}%, Elements={totalElements}, Warnings={warnings}");
        return Task.FromResult(response);
    }

    private List<CategoryDto> ExtractCategoryDistribution()
    {
        try
        {
            var collector = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType();

            var grouped = collector
                .Where(e => e.Category is not null && !string.IsNullOrEmpty(e.Category.Name))
                .GroupBy(e => e.Category!.Name)
                .Select(g => new CategoryDto { Name = g.Key, Value = g.Count() })
                .OrderByDescending(c => c.Value)
                .ToList();

            return grouped;
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
            var warningList = _doc.GetWarnings();
            return warningList?.Count ?? 0;
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to extract warnings.", ex);
            return 0;
        }
    }

    private List<FamilyDto> ExtractFamilySizes()
    {
        try
        {
            // Materialise the collector before opening any family documents so that
            // the filtered-element cursor is not held open while documents are opened
            // and closed inside the loop.
            var allFamilies = new FilteredElementCollector(_doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .ToList();

            var result = new List<FamilyDto>(allFamilies.Count);

            foreach (var f in allFamilies)
            {
                var sizeKb = 0;
                Document? famDoc = null;
                try
                {
                    famDoc = _doc.EditFamily(f);
                    if (famDoc is not null && !string.IsNullOrEmpty(famDoc.PathName))
                    {
                        var fi = new System.IO.FileInfo(famDoc.PathName);
                        sizeKb = (int)(fi.Length / 1024);
                    }
                }
                catch
                {
                    // Some families can't be edited; skip size
                }
                finally
                {
                    // Always close the family document before moving to the next
                    // family so Revit's document state stays clean.
                    famDoc?.Close(false);
                }

                result.Add(new FamilyDto { Name = f.Name, Kb = sizeKb });
            }

            return result
                .OrderByDescending(f => f.Kb)
                .Take(20)
                .ToList();
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to extract family sizes.", ex);
            return new List<FamilyDto>();
        }
    }

    private List<FailedCheckDto> RunHealthChecks()
    {
        var checks = new List<FailedCheckDto>();

        checks.Add(CheckMirroredElements());
        checks.Add(CheckUnplacedRooms());
        checks.Add(CheckDuplicateTypeNames());
        checks.Add(CheckDetailLineItems());
        checks.Add(CheckImportedCadInstances());

        return checks;
    }

    private FailedCheckDto CheckMirroredElements()
    {
        try
        {
            var count = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Count(fi => fi.Mirrored);

            return new FailedCheckDto
            {
                Name = "Mirrored Elements",
                Count = count,
                Category = "Model Quality",
                Priority = count > 50 ? "HIGH" : count > 10 ? "MEDIUM" : "LOW",
                Discipline = "General"
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Mirrored Elements.", ex);
            return new FailedCheckDto { Name = "Mirrored Elements", Count = 0, Category = "Model Quality", Priority = "LOW", Discipline = "General" };
        }
    }

    private FailedCheckDto CheckUnplacedRooms()
    {
        try
        {
            var count = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Architecture.Room>()
                .Count(r => r.Area <= 0);

            return new FailedCheckDto
            {
                Name = "Unplaced Rooms",
                Count = count,
                Category = "Spatial",
                Priority = count > 10 ? "HIGH" : count > 0 ? "MEDIUM" : "LOW",
                Discipline = "Architecture"
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Unplaced Rooms.", ex);
            return new FailedCheckDto { Name = "Unplaced Rooms", Count = 0, Category = "Spatial", Priority = "LOW", Discipline = "Architecture" };
        }
    }

    private FailedCheckDto CheckDuplicateTypeNames()
    {
        try
        {
            var typeNames = new FilteredElementCollector(_doc)
                .WhereElementIsElementType()
                .Select(e => e.Name)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            var duplicates = typeNames
                .GroupBy(n => n)
                .Where(g => g.Count() > 1)
                .Sum(g => g.Count() - 1);

            return new FailedCheckDto
            {
                Name = "Duplicate Type Names",
                Count = duplicates,
                Category = "Naming",
                Priority = duplicates > 20 ? "HIGH" : duplicates > 5 ? "MEDIUM" : "LOW",
                Discipline = "General"
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Duplicate Type Names.", ex);
            return new FailedCheckDto { Name = "Duplicate Type Names", Count = 0, Category = "Naming", Priority = "LOW", Discipline = "General" };
        }
    }

    private FailedCheckDto CheckDetailLineItems()
    {
        try
        {
            var count = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_Lines)
                .WhereElementIsNotElementType()
                .Count();

            return new FailedCheckDto
            {
                Name = "Detail Line Items",
                Count = count,
                Category = "Performance",
                Priority = count > 5000 ? "HIGH" : count > 1000 ? "MEDIUM" : "LOW",
                Discipline = "General"
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Detail Line Items.", ex);
            return new FailedCheckDto { Name = "Detail Line Items", Count = 0, Category = "Performance", Priority = "LOW", Discipline = "General" };
        }
    }

    private FailedCheckDto CheckImportedCadInstances()
    {
        try
        {
            var count = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .WhereElementIsNotElementType()
                .Count();

            return new FailedCheckDto
            {
                Name = "Imported CAD Instances",
                Count = count,
                Category = "Performance",
                Priority = count > 10 ? "HIGH" : count > 3 ? "MEDIUM" : "LOW",
                Discipline = "General"
            };
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Imported CAD Instances.", ex);
            return new FailedCheckDto { Name = "Imported CAD Instances", Count = 0, Category = "Performance", Priority = "LOW", Discipline = "General" };
        }
    }

    private static string BuildModelKey(IRevitDocumentContext context)
    {
        if (context.IsCloudModel && !string.IsNullOrEmpty(context.ProjectGuid) && !string.IsNullOrEmpty(context.ModelGuid))
            return Slugify($"{context.ProjectGuid}-{context.ModelGuid}");

        var path = !string.IsNullOrEmpty(context.CentralModelPath) ? context.CentralModelPath : context.ModelPath;
        return Slugify($"{path}-{context.DocumentTitle}");
    }

    private static string ExtractProjectName(IRevitDocumentContext context)
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

        var slug = input.ToLowerInvariant();
        slug = System.IO.Path.GetFileNameWithoutExtension(slug) +
               (slug.Contains(System.IO.Path.DirectorySeparatorChar) || slug.Contains('/') ? "" : "");

        // Replace non-alphanumeric with hyphens, collapse consecutive hyphens
        var sb = new System.Text.StringBuilder();
        foreach (var c in input.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else
                sb.Append('-');
        }

        var result = sb.ToString();
        while (result.Contains("--"))
            result = result.Replace("--", "-");
        return result.Trim('-');
    }
}
