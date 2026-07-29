using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace EquipmentFailureAnalysis.Services
{
    public sealed class EquipmentDowntimeLossService
    {
        public EquipmentDowntimeLossReport BuildReport(
            IEnumerable<EquipmentInfo> masterEquipment,
            DateTime startDate,
            DateTime endDate,
            PeriodGranularity granularity,
            string? subdivisionFilter = null)
        {
            var ruCulture = new CultureInfo("ru-RU");
            var start = startDate.Date;
            var end = endDate.Date;
            if (end < start)
                end = start;

            var periodExclusiveEnd = end.AddDays(1);
            var now = DateTime.Now;

            // Generate period buckets
            var periodHeaders = GeneratePeriodBuckets(start, periodExclusiveEnd, granularity, ruCulture);

            var equipmentRows = new List<EquipmentDowntimeLossRow>();
            var allIssueDetails = new List<DowntimeIssueDetail>();

            var filteredEquipment = masterEquipment ?? Enumerable.Empty<EquipmentInfo>();
            if (!string.IsNullOrWhiteSpace(subdivisionFilter)
                && !string.Equals(subdivisionFilter, "Все группы", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(subdivisionFilter, "Без группы", StringComparison.OrdinalIgnoreCase))
                {
                    filteredEquipment = filteredEquipment.Where(e => string.IsNullOrWhiteSpace(e.Subdivision));
                }
                else
                {
                    filteredEquipment = filteredEquipment.Where(e =>
                        string.Equals(e.Subdivision?.Trim(), subdivisionFilter.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            }

            double grandTotalRepairMinutes = 0;
            double grandTotalSetupMinutes = 0;
            int grandTotalRepairCount = 0;
            int grandTotalSetupCount = 0;

            foreach (var eq in filteredEquipment)
            {
                var invNo = string.IsNullOrWhiteSpace(eq.InventoryNumber) ? "б/н" : eq.InventoryNumber.Trim();
                var eqTitle = eq.Title?.Trim() ?? string.Empty;
                var subdivision = string.IsNullOrWhiteSpace(eq.Subdivision) ? "Без группы" : eq.Subdivision.Trim();

                string equipmentIdKey = eqTitle;

                var row = new EquipmentDowntimeLossRow
                {
                    EquipmentIdKey = equipmentIdKey,
                    EquipmentTitle = eqTitle,
                    InventoryNumber = invNo,
                    Subdivision = subdivision
                };

                // Initialize buckets for this equipment row
                foreach (var header in periodHeaders)
                {
                    row.PeriodBuckets[header.PeriodKey] = new PeriodLossBucket
                    {
                        PeriodKey = header.PeriodKey,
                        PeriodLabel = header.PeriodLabel,
                        StartDate = header.StartDate,
                        EndDate = header.EndDate
                    };
                }

                if (eq.Issues != null)
                {
                    foreach (var issue in eq.Issues)
                    {
                        var issueEnd = issue.IsInProgress ? now : issue.End;
                        if (issueEnd <= start || issue.Start >= periodExclusiveEnd)
                            continue;

                        bool isRepair = issue.Type == IssueType.Ремонт;
                        bool isSetup = issue.Type == IssueType.Настройка;
                        double totalIssueMinutes = Math.Max(0, (issueEnd - issue.Start).TotalMinutes);

                        // Collect issue detail for Sheet 2
                        allIssueDetails.Add(new DowntimeIssueDetail
                        {
                            EquipmentIdKey = equipmentIdKey,
                            EquipmentTitle = eqTitle,
                            InventoryNumber = invNo,
                            Subdivision = subdivision,
                            JiraIssueKey = issue.JiraIssueKey?.Trim() ?? string.Empty,
                            IssueType = issue.Type.ToString(),
                            Start = issue.Start,
                            End = issueEnd,
                            DurationMinutes = totalIssueMinutes,
                            Responsible = issue.Responsible?.Trim() ?? string.Empty,
                            Reporter = issue.Reporter?.Trim() ?? string.Empty,
                            Description = issue.Description?.Trim() ?? string.Empty,
                            Comments = issue.Comments?.Trim() ?? string.Empty
                        });

                        foreach (var header in periodHeaders)
                        {
                            var bucketStart = header.StartDate;
                            var bucketEnd = header.EndDate;

                            if (issue.Start >= bucketEnd || issueEnd <= bucketStart)
                                continue;

                            var overlapStart = issue.Start < bucketStart ? bucketStart : issue.Start;
                            var overlapEnd = issueEnd > bucketEnd ? bucketEnd : issueEnd;
                            var overlapMinutes = Math.Max(0, (overlapEnd - overlapStart).TotalMinutes);

                            if (overlapMinutes <= 0)
                                continue;

                            var bucket = row.PeriodBuckets[header.PeriodKey];
                            if (isRepair)
                            {
                                bucket.RepairMinutes += overlapMinutes;
                                bucket.RepairCount++;
                                row.TotalRepairMinutes += overlapMinutes;
                                row.TotalRepairCount++;
                            }
                            else if (isSetup)
                            {
                                bucket.SetupMinutes += overlapMinutes;
                                bucket.SetupCount++;
                                row.TotalSetupMinutes += overlapMinutes;
                                row.TotalSetupCount++;
                            }
                            else
                            {
                                // Fallback default as repair
                                bucket.RepairMinutes += overlapMinutes;
                                bucket.RepairCount++;
                                row.TotalRepairMinutes += overlapMinutes;
                                row.TotalRepairCount++;
                            }
                        }
                    }
                }

                grandTotalRepairMinutes += row.TotalRepairMinutes;
                grandTotalSetupMinutes += row.TotalSetupMinutes;
                grandTotalRepairCount += row.TotalRepairCount;
                grandTotalSetupCount += row.TotalSetupCount;

                equipmentRows.Add(row);
            }

            equipmentRows = equipmentRows
                .OrderByDescending(r => r.TotalDowntimeMinutes)
                .ThenBy(r => r.EquipmentTitle)
                .ToList();

            allIssueDetails = allIssueDetails
                .OrderBy(d => d.EquipmentIdKey)
                .ThenByDescending(d => d.Start)
                .ToList();

            return new EquipmentDowntimeLossReport
            {
                StartDate = start,
                EndDate = end,
                Granularity = granularity,
                PeriodHeaders = periodHeaders,
                Rows = equipmentRows,
                AllIssueDetails = allIssueDetails,
                TotalRepairMinutes = grandTotalRepairMinutes,
                TotalSetupMinutes = grandTotalSetupMinutes,
                TotalRepairCount = grandTotalRepairCount,
                TotalSetupCount = grandTotalSetupCount
            };
        }

        private static List<PeriodLossBucket> GeneratePeriodBuckets(
            DateTime start, DateTime periodExclusiveEnd, PeriodGranularity granularity, CultureInfo culture)
        {
            var buckets = new List<PeriodLossBucket>();

            if (granularity == PeriodGranularity.Monthly)
            {
                var cur = new DateTime(start.Year, start.Month, 1);
                while (cur < periodExclusiveEnd)
                {
                    var monthEnd = cur.AddMonths(1);
                    var rawLabel = cur.ToString("MMMM yyyy", culture);
                    var label = char.ToUpper(rawLabel[0], culture) + rawLabel.Substring(1);

                    buckets.Add(new PeriodLossBucket
                    {
                        PeriodKey = cur.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                        PeriodLabel = label,
                        StartDate = cur,
                        EndDate = monthEnd
                    });

                    cur = monthEnd;
                }
            }
            else // Quarterly
            {
                int startQuarter = (start.Month - 1) / 3 + 1;
                var curQuarterStart = new DateTime(start.Year, (startQuarter - 1) * 3 + 1, 1);

                while (curQuarterStart < periodExclusiveEnd)
                {
                    var quarterEnd = curQuarterStart.AddMonths(3);
                    int qNum = (curQuarterStart.Month - 1) / 3 + 1;
                    string romanQ = qNum switch
                    {
                        1 => "I",
                        2 => "II",
                        3 => "III",
                        _ => "IV"
                    };
                    string label = $"{romanQ} Квартал {curQuarterStart.Year}";

                    buckets.Add(new PeriodLossBucket
                    {
                        PeriodKey = $"{curQuarterStart.Year}-Q{qNum}",
                        PeriodLabel = label,
                        StartDate = curQuarterStart,
                        EndDate = quarterEnd
                    });

                    curQuarterStart = quarterEnd;
                }
            }

            return buckets;
        }
    }
}
