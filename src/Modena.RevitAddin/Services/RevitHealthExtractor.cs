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
        var checks     = RunHealthChecks(warnings);

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
        var checks     = RunHealthChecks(warnings);

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
        List<WarningDto> warnings,
        List<FamilyDto> families,
        List<FailedCheckDto> healthChecks)
    {
        var warningCount = warnings.Sum(w => w.Count);
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
                Warnings         = warningCount,
                FileSizeMb       = 0,
                ModelElements    = totalElements
            },
            FailedChecks = failedChecks,
            Metrics = new List<MetricDto>
            {
                new() { Name = "Total Elements", Count = totalElements },
                new() { Name = "Warnings",       Count = warningCount  },
                new() { Name = "Families",       Count = families.Count }
            },
            Categories   = categories,
            Families     = families,
            Warnings     = warnings,
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

    private List<WarningDto> ExtractWarnings()
    {
        try
        {
            var warnings = _doc.GetWarnings();
            if (warnings is null || warnings.Count == 0)
                return new List<WarningDto>();

            return warnings
                .Select(w =>
                {
                    var elementIds = w.GetFailingElements()
                        .Concat(w.GetAdditionalElements())
                        .Select(GetId)
                        .Distinct()
                        .ToList();

                    return new
                    {
                        Description = string.IsNullOrWhiteSpace(w.GetDescriptionText())
                            ? "Revit warning"
                            : w.GetDescriptionText(),
                        ElementIds = elementIds
                    };
                })
                .GroupBy(w => w.Description)
                .Select(g =>
                {
                    var ids = g.SelectMany(w => w.ElementIds).Distinct().ToList();
                    return new WarningDto
                    {
                        Description = g.Key,
                        Count = g.Count(),
                        ElementCount = ids.Count,
                        ElementIds = ids
                    };
                })
                .OrderByDescending(w => w.Count)
                .ThenBy(w => w.Description)
                .ToList();
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to extract warnings.", ex);
            return new List<WarningDto>();
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

    private List<FailedCheckDto> RunHealthChecks(List<WarningDto> warnings)
    {
        var rules   = _config.HealthChecks;
        var results = new List<FailedCheckDto>();

        if (rules.RevitWarnings.Enabled)        results.Add(CheckRevitWarnings(rules.RevitWarnings, warnings));
        if (rules.MirroredElements.Enabled)     results.Add(CheckMirroredElements(rules.MirroredElements));
        if (rules.UnplacedRooms.Enabled)        results.Add(CheckUnplacedRooms(rules.UnplacedRooms));
        if (rules.DuplicateTypeNames.Enabled)   results.Add(CheckDuplicateTypeNames(rules.DuplicateTypeNames));
        if (rules.DetailLineItems.Enabled)      results.Add(CheckDetailLineItems(rules.DetailLineItems));
        if (rules.ImportedCadInstances.Enabled) results.Add(CheckImportedCadInstances(rules.ImportedCadInstances));
        if (rules.LinkedCadInstances.Enabled)   results.Add(CheckLinkedCadInstances(rules.LinkedCadInstances));
        if (rules.LinkedCadNotPinned.Enabled)   results.Add(CheckLinkedCadNotPinned(rules.LinkedCadNotPinned));
        if (rules.LinkedCadAllViews.Enabled)    results.Add(CheckLinkedCadAllViews(rules.LinkedCadAllViews));
        if (rules.LinkedRevitNotPinned.Enabled) results.Add(CheckLinkedRevitNotPinned(rules.LinkedRevitNotPinned));
        if (rules.InPlaceFamilies.Enabled)      results.Add(CheckInPlaceFamilies(rules.InPlaceFamilies));
        if (rules.RasterImages.Enabled)         results.Add(CheckRasterImages(rules.RasterImages));
        if (rules.UnusedViews.Enabled)          results.Add(CheckUnusedViews(rules.UnusedViews));
        if (rules.ViewsWithoutTemplates.Enabled) results.Add(CheckViewsWithoutTemplates(rules.ViewsWithoutTemplates));
        if (rules.DuplicateInstances.Enabled)   results.Add(CheckDuplicateInstances(rules.DuplicateInstances));

        return results;
    }

    private static FailedCheckDto BuildCheck(
        string name,
        int count,
        List<long> elementIds,
        string category,
        string defaultDiscipline,
        HealthCheckRule rule,
        string description,
        string recommendation)
    {
        return new FailedCheckDto
        {
            Name = name,
            Count = count,
            ElementIds = elementIds,
            Category = category,
            Priority = rule.ResolvePriority(count),
            Discipline = string.IsNullOrEmpty(rule.Discipline) ? defaultDiscipline : rule.Discipline!,
            Description = description,
            Recommendation = recommendation
        };
    }

    private FailedCheckDto CheckRevitWarnings(HealthCheckRule rule, List<WarningDto> warnings)
    {
        var ids = warnings.SelectMany(w => w.ElementIds).Distinct().ToList();
        var count = warnings.Sum(w => w.Count);
        return BuildCheck(
            "Revit Warnings",
            count,
            ids,
            "Model Quality",
            "General",
            rule,
            "Revit warnings indicate model conditions that can affect stability, coordination, documentation, or performance.",
            "Review warning groups by type, resolve repeated warnings first, and prioritise warnings affecting many elements.");
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

            return BuildCheck(
                "Mirrored Elements",
                ids.Count,
                ids,
                "Model Quality",
                "General",
                rule,
                "Mirrored elements can indicate modelling shortcuts that may affect hosted geometry, schedules, and coordination.",
                "Review mirrored instances and replace with correctly oriented families where project standards require it.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Mirrored Elements.", ex);
            return BuildCheck("Mirrored Elements", 0, new List<long>(), "Model Quality", "General", rule, "Mirrored elements can indicate modelling shortcuts.", "Review mirrored instances.");
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

            return BuildCheck(
                "Unplaced Rooms",
                ids.Count,
                ids,
                "Spatial",
                "Architecture",
                rule,
                "Unplaced or zero-area rooms can create incorrect area schedules and downstream coordination issues.",
                "Delete unused rooms or place/enclose rooms correctly before issuing schedules.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Unplaced Rooms.", ex);
            return BuildCheck("Unplaced Rooms", 0, new List<long>(), "Spatial", "Architecture", rule, "Unplaced rooms affect area data.", "Delete unused rooms or place/enclose them correctly.");
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

            return BuildCheck(
                "Duplicate Type Names",
                count,
                ids,
                "Naming",
                "General",
                rule,
                "Duplicate type names make schedules, QA reviews, and standards enforcement harder to trust.",
                "Rename duplicate types so each type name is unique and meaningful.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Duplicate Type Names.", ex);
            return BuildCheck("Duplicate Type Names", 0, new List<long>(), "Naming", "General", rule, "Duplicate type names affect standards review.", "Rename duplicate types.");
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

            return BuildCheck(
                "Detail Line Items",
                ids.Count,
                ids,
                "Performance",
                "General",
                rule,
                "Large numbers of detail lines can increase documentation maintenance effort and slow view performance.",
                "Review detail-line heavy views and replace repeated drafting with modelled or reusable detail components where practical.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Detail Line Items.", ex);
            return BuildCheck("Detail Line Items", 0, new List<long>(), "Performance", "General", rule, "Detail lines can indicate heavy drafting.", "Review detail-line heavy views.");
        }
    }

    private FailedCheckDto CheckImportedCadInstances(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .WhereElementIsNotElementType()
                .Cast<ImportInstance>()
                .Where(i => !i.IsLinked)
                .Select(e => GetId(e.Id))
                .ToList();

            return BuildCheck(
                "Imported CAD Files",
                ids.Count,
                ids,
                "External Files",
                "General",
                rule,
                "Imported CAD is embedded in the Revit model and can bloat files, contaminate line styles, and slow views.",
                "Remove imported CAD and link CAD files only where needed, preferably current-view only.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Imported CAD Instances.", ex);
            return BuildCheck("Imported CAD Files", 0, new List<long>(), "External Files", "General", rule, "Imported CAD can bloat models.", "Remove imported CAD and use links where necessary.");
        }
    }

    private FailedCheckDto CheckLinkedCadInstances(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .WhereElementIsNotElementType()
                .Cast<ImportInstance>()
                .Where(i => i.IsLinked)
                .Select(i => GetId(i.Id))
                .ToList();

            return BuildCheck(
                "Linked CAD Files",
                ids.Count,
                ids,
                "External Files",
                "General",
                rule,
                "Linked CAD files are preferable to imported CAD, but still need review because they can affect performance and view clarity.",
                "Keep only necessary CAD links, place them current-view only where possible, and remove stale references.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Linked CAD Files.", ex);
            return BuildCheck("Linked CAD Files", 0, new List<long>(), "External Files", "General", rule, "Linked CAD files should be reviewed.", "Remove stale CAD links.");
        }
    }

    private FailedCheckDto CheckLinkedCadNotPinned(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .WhereElementIsNotElementType()
                .Cast<ImportInstance>()
                .Where(i => i.IsLinked && !i.Pinned)
                .Select(i => GetId(i.Id))
                .ToList();

            return BuildCheck(
                "Linked CAD Not Pinned",
                ids.Count,
                ids,
                "External Files",
                "General",
                rule,
                "Unpinned CAD links can be moved accidentally, breaking coordination and documentation alignment.",
                "Pin linked CAD files after placing them in the correct location.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Linked CAD Not Pinned.", ex);
            return BuildCheck("Linked CAD Not Pinned", 0, new List<long>(), "External Files", "General", rule, "Unpinned CAD links can move accidentally.", "Pin linked CAD files.");
        }
    }

    private FailedCheckDto CheckLinkedCadAllViews(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfClass(typeof(ImportInstance))
                .WhereElementIsNotElementType()
                .Cast<ImportInstance>()
                .Where(i => i.IsLinked && !i.ViewSpecific)
                .Select(i => GetId(i.Id))
                .ToList();

            return BuildCheck(
                "Linked CAD Visible In All Views",
                ids.Count,
                ids,
                "External Files",
                "General",
                rule,
                "CAD links visible in all views can contaminate unrelated views and slow view regeneration.",
                "Reload CAD as current-view only when the reference is only needed for a specific drawing view.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Linked CAD Visible In All Views.", ex);
            return BuildCheck("Linked CAD Visible In All Views", 0, new List<long>(), "External Files", "General", rule, "All-view CAD links can slow views.", "Prefer current-view only CAD links.");
        }
    }

    private FailedCheckDto CheckLinkedRevitNotPinned(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance))
                .WhereElementIsNotElementType()
                .Cast<RevitLinkInstance>()
                .Where(i => !i.Pinned)
                .Select(i => GetId(i.Id))
                .ToList();

            return BuildCheck(
                "Linked Revit Files Not Pinned",
                ids.Count,
                ids,
                "External Files",
                "General",
                rule,
                "Unpinned Revit links can be moved accidentally, causing coordination overlays and references to drift.",
                "Pin linked Revit instances after they are coordinated.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Linked Revit Files Not Pinned.", ex);
            return BuildCheck("Linked Revit Files Not Pinned", 0, new List<long>(), "External Files", "General", rule, "Unpinned Revit links can move accidentally.", "Pin linked Revit instances.");
        }
    }

    private FailedCheckDto CheckInPlaceFamilies(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol?.Family?.IsInPlace == true)
                .Select(fi => GetId(fi.Id))
                .ToList();

            return BuildCheck(
                "In-Place Families",
                ids.Count,
                ids,
                "Performance",
                "General",
                rule,
                "In-place families are hard to reuse, can inflate model size, and often perform worse than loadable families.",
                "Replace repeated in-place families with loadable families where practical.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: In-Place Families.", ex);
            return BuildCheck("In-Place Families", 0, new List<long>(), "Performance", "General", rule, "In-place families can affect model health.", "Replace repeated in-place families.");
        }
    }

    private FailedCheckDto CheckRasterImages(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfCategory(BuiltInCategory.OST_RasterImages)
                .WhereElementIsNotElementType()
                .Select(e => GetId(e.Id))
                .ToList();

            return BuildCheck(
                "Raster Images",
                ids.Count,
                ids,
                "Performance",
                "General",
                rule,
                "Embedded raster images can increase file size and slow view performance.",
                "Remove unused images and link/reference image content only where needed.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Raster Images.", ex);
            return BuildCheck("Raster Images", 0, new List<long>(), "Performance", "General", rule, "Raster images can bloat models.", "Remove unused images.");
        }
    }

    private FailedCheckDto CheckUnusedViews(HealthCheckRule rule)
    {
        try
        {
            var placedViewIds = new HashSet<long>(
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Select(vp => GetId(vp.ViewId)));

            var ids = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(IsReviewableView)
                .Where(v => !placedViewIds.Contains(GetId(v.Id)))
                .Select(v => GetId(v.Id))
                .ToList();

            return BuildCheck(
                "Unused Views",
                ids.Count,
                ids,
                "Documentation",
                "General",
                rule,
                "Unused views accumulate over a project and can make models harder to manage.",
                "Delete stale working views or move intentionally retained views into a managed browser organisation.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Unused Views.", ex);
            return BuildCheck("Unused Views", 0, new List<long>(), "Documentation", "General", rule, "Unused views add model clutter.", "Delete stale views.");
        }
    }

    private FailedCheckDto CheckViewsWithoutTemplates(HealthCheckRule rule)
    {
        try
        {
            var ids = new FilteredElementCollector(_doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(IsReviewableView)
                .Where(v => v.ViewTemplateId == ElementId.InvalidElementId)
                .Select(v => GetId(v.Id))
                .ToList();

            return BuildCheck(
                "Views Without Templates",
                ids.Count,
                ids,
                "Documentation",
                "General",
                rule,
                "Views without templates are more likely to have inconsistent visibility, graphics, and documentation settings.",
                "Apply project view templates to production views and keep unmanaged working views clearly separated.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Views Without Templates.", ex);
            return BuildCheck("Views Without Templates", 0, new List<long>(), "Documentation", "General", rule, "Views without templates can be inconsistent.", "Apply project view templates.");
        }
    }

    private FailedCheckDto CheckDuplicateInstances(HealthCheckRule rule)
    {
        try
        {
            var duplicateGroups = new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi => fi.DesignOption is null)
                .Select(fi => new { Instance = fi, Key = GetDuplicateInstanceKey(fi) })
                .Where(x => x.Key is not null)
                .GroupBy(x => x.Key!)
                .Where(g => g.Count() > 1)
                .ToList();

            var ids = duplicateGroups
                .SelectMany(g => g.Skip(1).Select(x => GetId(x.Instance.Id)))
                .ToList();

            return BuildCheck(
                "Duplicate Instances",
                ids.Count,
                ids,
                "Model Quality",
                "General",
                rule,
                "Duplicate instances in the same location can inflate quantities and cause graphical overlap.",
                "Review duplicate elements and delete unintended stacked instances.");
        }
        catch (Exception ex)
        {
            LogService.Error("Health check failed: Duplicate Instances.", ex);
            return BuildCheck("Duplicate Instances", 0, new List<long>(), "Model Quality", "General", rule, "Duplicate instances can affect quantities.", "Delete unintended duplicates.");
        }
    }

    private static bool IsReviewableView(View view)
    {
        if (view.IsTemplate)
            return false;

        return view.ViewType is not ViewType.DrawingSheet
            and not ViewType.Schedule
            and not ViewType.Legend
            and not ViewType.ProjectBrowser
            and not ViewType.SystemBrowser
            and not ViewType.Internal;
    }

    private static string? GetDuplicateInstanceKey(FamilyInstance instance)
    {
        var typeId = GetId(instance.GetTypeId());

        return instance.Location switch
        {
            LocationPoint point => $"{typeId}|P|{FormatPoint(point.Point)}",
            LocationCurve curve => $"{typeId}|C|{FormatPoint(curve.Curve.GetEndPoint(0))}|{FormatPoint(curve.Curve.GetEndPoint(1))}",
            _ => null
        };
    }

    private static string FormatPoint(XYZ point)
    {
        const double toleranceFeet = 1.0 / 304.8; // 1 mm in Revit internal feet.
        var x = Math.Round(point.X / toleranceFeet);
        var y = Math.Round(point.Y / toleranceFeet);
        var z = Math.Round(point.Z / toleranceFeet);
        return $"{x}|{y}|{z}";
    }

    private static long GetId(ElementId id)
    {
#if NET48
        return (long)id.IntegerValue;
#else
        return id.Value;
#endif
    }

    internal static string BuildModelKey(IRevitDocumentContext context)
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
