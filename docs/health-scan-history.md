# Health scan history

Status: implemented on `codex/health-scan-history`; live Revit smoke verification
remains outstanding. This document describes the add-in's local History feature.
Change this reference alongside the DTO/service/view models when behavior changes.
The original `health-scan-history-spec.md` is superseded design context.

## Using History

Dashboard is the default main tab and retains its existing contents. History is
the only added main tab. The existing family usage/size sub-tabs remain on Dashboard.
Both share the current Pulse header, Refresh action, palette and card styles.

Every fully successful scan, including automatic refreshes, creates a record.
Restoring the dashboard cache, failing/cancelling a scan, or extracting family
sizes on demand does not create a scan. Identical results are retained. History
starts when this feature is installed; previous cache files are not backfilled.

History shows scan count, latest score and change from previous, best score, and
change since best. Changes in pass rate use percentage points (pp), rather than
relative percentages. Best means highest score, breaking ties in favor of the
latest scan. A lower score than best is not labeled a declining trend.

Summary tiles follow the compact reference layout with outline icons and dated
captions. Latest pass rate has a green upward badge for improvement over the
previous chronological scan, a red downward badge for a drop, or neutral
"No change" text. The caption identifies that previous scan. The first scan has
no comparison badge. Change since best is a separate signed pp value with an
at-best/below-best date caption; a model recovering toward its peak can show
green latest movement and a red below-best value simultaneously. Selecting a
different comparison pair does not change these summary metrics.

The trend chart uses actual elapsed time and a fixed 0–100% scale. Hover for scan
details. Amber B and blue C markers match baseline/current selection buttons in
the scan table; selecting the same scan for both shows one B/C marker. The table
is newest-first and displays local dates and times,
passed/total checks, warnings, element counts and change from the previous scan.

Scan History displays 10 scans per page with Previous/Next controls, page count
and a visible range such as "11–20 of 296 scans". The first page follows new
scans on refresh. When browsing older pages, refresh keeps the previously
first-visible scan on a page where possible, so new scans do not jump you back
to the latest page. Changing models resets paging. Empty/first/last pages disable
unavailable navigation. Page changes preserve comparison selections and all
chart/summary data; comparison selectors always include the full history.
Previous-scan deltas remain chronological across page boundaries.

Comparisons default to previous → latest. Presets select previous/latest or
best/latest; B/C buttons and selectors permit any pair, including the same scan
or reversed chronology. Manual choices survive refreshes. Presets follow new
scans until a manual choice is made. At least two records are needed to compare.
Full Baseline/Current labels, dated selectors with full-value tooltips and a dated
selection caption identify the pair used for the displayed score change.

The comparison lists affected-item counts, not element identities. A check that
was passing and is now failing is **Newly failing**; a failing check now passing
is **Resolved**. Other count reductions/increases are **Improved/Regressed**.
Newly failing checks appear first, followed by regressed, resolved, improved,
not-comparable and unchanged checks. Choose **Changed** (default), **Worsened**,
**Improved**, or **All**. Worsened includes newly failing/regressed checks; Improved
includes resolved checks and reduced issue counts. Changed hides only unchanged
checks; All includes them. Filters preserve scan selections and do not alter the
summary sentence or coverage totals. Empty filters explain why no rows appear.
The summary sentence describes check changes without attributing a score change
to reduced counts within an already failing check. Checks
evaluated in only one scan are **Not comparable**, not assumed passing. The
coverage summary accounts for all recorded evaluated checks.

At normal/narrow widths, comparison appears immediately below the chart and before
the shorter scan table. Wider windows keep the panels side by side with matching
card heights and aligned bottom edges. Stacked cards size independently. The chart is
also shorter in the stacked layout, and Reload history lives in its heading.
Empty status/warning text collapses. Scan tables scroll and virtualize rows;
chart geometry retains pixel-bucket extrema
while hover still searches all scans.

## Storage and recovery

`HealthScanRecord` in Modena.Shared is the schema contract (version 1). Records
contain UTC completion timestamp, unique ID, model/project identity, summary
metrics, categories, trimmed failed-check summaries and evaluated passed names.
They omit element IDs, descriptions, recommendations and family data. Existing
check names identify checks; a rename is treated as a changed check set.

`HealthHistoryService` stores JSON Lines under `%AppData%\Modena\history`, one
file per model. Filenames contain a bounded sanitized key prefix and full SHA256
key suffix. Cloud identity uses project/model GUIDs. Local/workshared identity
uses the extractor's model/central path and title. Local renames/moves can start
separate history. The existing dashboard cache is unchanged.

History belongs to the Windows user profile; it is not team/cloud synchronized.
There is no pruning, deduplication, export or deletion UI. Storage and memory
usage grow with scan count. History I/O runs on workers, with a named per-file
mutex serializing access across local Revit sessions (five-second lock timeout).

Malformed, unsupported-version and wrong-model records are skipped and logged.
An interrupted final line is separated before the next append, preserving earlier
valid data. History save failures show a warning in History and do not fail the
dashboard scan. Read failures show a retry message; Reload history retries reads
and also discovers scans written by another session. Failed saves are not retried
as new scans. Check folder access/free space and the app log when a warning appears.
Back up the history directory before manually inspecting or repairing files.

## Validation and remaining limits

Implementation is shared across Revit 2024/.NET Framework 4.8, Revit 2025–26/.NET 8
and Revit 2027/.NET 10. No production dependency was added. Automated coverage lives
in `HealthHistoryTests.cs` and `HealthHistoryViewTests.cs`; detailed build/test and
review evidence is in `specs/000-health-scan-history/verification.md`.

Before release, verify in Revit: initial and cached opens, manual/automatic refresh,
closing during extraction, two sessions writing the same model, trial banner,
chart hover, keyboard selectors, DPI scaling, and existing Dashboard Show actions
and family controls. Offscreen WPF renders and API-targeted builds do not prove
live Revit behavior.
