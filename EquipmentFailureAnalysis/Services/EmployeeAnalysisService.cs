using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace EquipmentFailureAnalysis.Services
{
    public sealed class EmployeeAnalysisResult
    {
        public List<EmployeeAnalysisRow> Rows { get; init; } = new List<EmployeeAnalysisRow>();
        public int TotalIssues { get; init; }
        public int UnassignedIssues { get; init; }
        public int RepairsTotal { get; init; }
        public int SetupsTotal { get; init; }
        public double SlaCompliancePercent { get; init; }
        public int SlaBreaches { get; init; }
        public double CoveragePercent { get; init; }
        public double AvgEquipmentPerEmployee { get; init; }
        public TimeSpan AvgDuration { get; init; }
        public string TopByIssues { get; init; } = "-";
        public string TopByIssuesValue { get; init; } = "0";
        public string TopByDuration { get; init; } = "-";
        public string TopByDurationValue { get; init; } = "00:00";
    }

    public sealed class EmployeeAnalysisService
    {
        private sealed class EmployeeIssueProjection
        {
            public required EquipmentInfo Equipment { get; init; }
            public required Issue Issue { get; init; }
        }

        public EmployeeAnalysisResult Analyze(
            List<EquipmentInfo> masterEquipment,
            string selectedMonth,
            string selectedSubdivision,
            double slaTargetMinutes)
        {
            var now = DateTime.Now;
            var monthStart = DateTime.MinValue;
            var monthEnd = DateTime.MaxValue;
            var ruCulture = new CultureInfo("ru-RU");
            if (!string.IsNullOrWhiteSpace(selectedMonth) && selectedMonth != "Все месяцы" && DateTime.TryParseExact(selectedMonth, "MMMM yyyy", ruCulture, DateTimeStyles.None, out var monthDate))
            {
                monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
                monthEnd = monthStart.AddMonths(1);
            }

            var issuesWithEquipmentAll = masterEquipment
                .SelectMany(eq => eq.Issues.Select(issue => new EmployeeIssueProjection { Equipment = eq, Issue = issue }))
                .ToList();

            var issuesWithEquipment = FilterEmployeeIssuesBySelectedSubdivision(
                FilterEmployeeIssuesBySelectedMonth(issuesWithEquipmentAll, selectedMonth),
                selectedSubdivision).ToList();

            var totalIssues = issuesWithEquipment.Count;

            var assignedIssues = issuesWithEquipment
                .Where(x => !IsUnassignedResponsible(x.Issue.Responsible))
                .ToList();

            var unassignedIssues = totalIssues - assignedIssues.Count;
            var repairsTotal = assignedIssues.Count(x => x.Issue.Type == IssueType.Ремонт);
            var setupsTotal = assignedIssues.Count(x => x.Issue.Type == IssueType.Настройка);

            var slaMetTotal = assignedIssues.Count(x => Math.Max(0, ((x.Issue.IsInProgress ? now : x.Issue.End) - x.Issue.Start).TotalMinutes) <= slaTargetMinutes);
            var slaBreaches = Math.Max(0, assignedIssues.Count - slaMetTotal);
            var slaCompliancePercent = assignedIssues.Count == 0
                ? 0.0
                : slaMetTotal * 100.0 / assignedIssues.Count;

            var coveragePercent = totalIssues == 0
                ? 0.0
                : assignedIssues.Count * 100.0 / totalIssues;

            var rows = assignedIssues
                .GroupBy(x => x.Issue.Responsible!.Trim(), StringComparer.CurrentCultureIgnoreCase)
                .Select(g =>
                {
                    var issues = g.Select(x => x.Issue).ToList();
                    var repairsCount = issues.Count(i => i.Type == IssueType.Ремонт);
                    var setupsCount = issues.Count(i => i.Type == IssueType.Настройка);
                    var slaMetCount = issues.Count(i => Math.Max(0, ((i.IsInProgress ? now : i.End) - i.Start).TotalMinutes) <= slaTargetMinutes);
                    var totalDuration = TimeSpan.FromMinutes(issues.Sum(i =>
                    {
                        var issueEnd = i.IsInProgress ? now : i.End;
                        if (monthStart != DateTime.MinValue)
                        {
                            var overlapStart = i.Start < monthStart ? monthStart : i.Start;
                            var overlapEnd = issueEnd > monthEnd ? monthEnd : issueEnd;
                            return Math.Max(0, (overlapEnd - overlapStart).TotalMinutes);
                        }
                        return Math.Max(0, (issueEnd - i.Start).TotalMinutes);
                    }));
                    var avgMinutes = issues.Count == 0 ? 0.0 : totalDuration.TotalMinutes / issues.Count;
                    var lastIssueDate = issues.Count == 0 ? DateTime.MinValue : issues.Max(i => i.IsInProgress ? now : i.End);

                    return new EmployeeAnalysisRow
                    {
                        Name = g.Key,
                        Subdivision = string.Join(", ", g
                            .Select(x => x.Equipment.Subdivision)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(s => s!.Trim())
                            .Distinct(StringComparer.CurrentCultureIgnoreCase)),
                        IssuesCount = issues.Count,
                        EventSharePercent = assignedIssues.Count == 0 ? 0.0 : issues.Count * 100.0 / assignedIssues.Count,
                        RepairsCount = repairsCount,
                        SetupsCount = setupsCount,
                        RepairsSharePercent = issues.Count == 0 ? 0.0 : repairsCount * 100.0 / issues.Count,
                        SlaMetCount = slaMetCount,
                        SlaCompliancePercent = issues.Count == 0
                            ? 0.0
                            : slaMetCount * 100.0 / issues.Count,
                        EquipmentCount = g.Select(x => x.Equipment.Title).Distinct(StringComparer.CurrentCultureIgnoreCase).Count(),
                        TotalDuration = totalDuration,
                        TotalDurationText = FormatDuration(totalDuration),
                        AvgDurationMinutes = avgMinutes,
                        AvgDurationText = FormatDuration(TimeSpan.FromMinutes(avgMinutes)),
                        LastIssueDate = lastIssueDate,
                        LastIssueDateText = lastIssueDate == DateTime.MinValue ? "-" : lastIssueDate.ToString("dd.MM.yyyy")
                    };
                })
                .Select(r =>
                {
                    if (string.IsNullOrWhiteSpace(r.Subdivision))
                        r.Subdivision = "-";
                    return r;
                })
                .ToList();

            if (rows.Count > 0)
            {
                const double repairComplexityWeight = 1.35;
                const double setupComplexityWeight = 1.0;

                var maxIssues = Math.Max(1, rows.Max(r => r.IssuesCount));
                var maxEquipment = Math.Max(1, rows.Max(r => r.EquipmentCount));
                var minAvgMinutes = rows.Min(r => r.AvgDurationMinutes);
                var maxAvgMinutes = rows.Max(r => r.AvgDurationMinutes);
                var avgSpan = Math.Max(1.0, maxAvgMinutes - minAvgMinutes);
                var maxComplexityLoad = Math.Max(
                    1.0,
                    rows.Max(r => r.RepairsCount * repairComplexityWeight + r.SetupsCount * setupComplexityWeight));

                foreach (var row in rows)
                {
                    var shareScore = Math.Clamp(row.EventSharePercent, 0, 100);
                    var slaScore = Math.Clamp(row.SlaCompliancePercent, 0, 100);
                    var speedScore = Math.Clamp(100.0 - ((row.AvgDurationMinutes - minAvgMinutes) / avgSpan) * 100.0, 0, 100);
                    var loadScore = row.IssuesCount * 100.0 / maxIssues;
                    var complexityLoadScore = (row.RepairsCount * repairComplexityWeight + row.SetupsCount * setupComplexityWeight)
                        * 100.0 / maxComplexityLoad;
                    var coverageScore = row.EquipmentCount * 100.0 / maxEquipment;

                    var score = slaScore * 0.40
                                + shareScore * 0.15
                                + speedScore * 0.15
                                + loadScore * 0.10
                                + complexityLoadScore * 0.10
                                + coverageScore * 0.10;

                    row.PerformanceScore = Math.Round(score, 1);
                    var grade = row.PerformanceScore >= 85 ? "A"
                        : row.PerformanceScore >= 70 ? "B"
                        : row.PerformanceScore >= 55 ? "C"
                        : "D";
                    row.PerformanceSummary = $"{row.PerformanceScore:0.0} ({grade})";
                }
            }

            rows = rows
                .OrderByDescending(r => r.PerformanceScore)
                .ThenByDescending(r => r.SlaCompliancePercent)
                .ThenByDescending(r => r.IssuesCount)
                .ThenBy(r => r.Name)
                .ToList();

            var avgEquipmentPerEmployee = rows.Count == 0 ? 0.0 : rows.Average(r => r.EquipmentCount);

            var avgDurationMinutes = assignedIssues.Count == 0
                ? 0.0
                : assignedIssues.Sum(x =>
                {
                    var issueEnd = x.Issue.IsInProgress ? now : x.Issue.End;
                    if (monthStart != DateTime.MinValue)
                    {
                        var overlapStart = x.Issue.Start < monthStart ? monthStart : x.Issue.Start;
                        var overlapEnd = issueEnd > monthEnd ? monthEnd : issueEnd;
                        return Math.Max(0, (overlapEnd - overlapStart).TotalMinutes);
                    }
                    return Math.Max(0, (issueEnd - x.Issue.Start).TotalMinutes);
                }) / assignedIssues.Count;

            var topByIssues = rows.OrderByDescending(r => r.IssuesCount).ThenBy(r => r.Name).FirstOrDefault();
            var topByIssuesName = topByIssues?.Name ?? "-";
            var topByIssuesVal = topByIssues == null ? "0" : $"{topByIssues.IssuesCount} событий";

            var topByDuration = rows.OrderByDescending(r => r.TotalDuration).ThenBy(r => r.Name).FirstOrDefault();
            var topByDurationName = topByDuration?.Name ?? "-";
            var topByDurationVal = topByDuration == null ? "00:00" : FormatDuration(topByDuration.TotalDuration);

            return new EmployeeAnalysisResult
            {
                Rows = rows,
                TotalIssues = totalIssues,
                UnassignedIssues = unassignedIssues,
                RepairsTotal = repairsTotal,
                SetupsTotal = setupsTotal,
                SlaCompliancePercent = slaCompliancePercent,
                SlaBreaches = slaBreaches,
                CoveragePercent = coveragePercent,
                AvgEquipmentPerEmployee = avgEquipmentPerEmployee,
                AvgDuration = TimeSpan.FromMinutes(avgDurationMinutes),
                TopByIssues = topByIssuesName,
                TopByIssuesValue = topByIssuesVal,
                TopByDuration = topByDurationName,
                TopByDurationValue = topByDurationVal
            };
        }

        private static bool IsUnassignedResponsible(string? responsible)
        {
            if (string.IsNullOrWhiteSpace(responsible))
                return true;

            var value = responsible.Trim();
            return value == "-"
                   || value == "-1"
                   || value.Equals("Не назначен", StringComparison.CurrentCultureIgnoreCase)
                   || value.Equals("unassigned", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("null", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
                duration = TimeSpan.Zero;

            var hours = (int)duration.TotalHours;
            return $"{hours:00}:{duration.Minutes:00}";
        }

        private IEnumerable<EmployeeIssueProjection> FilterEmployeeIssuesBySelectedMonth(
            IEnumerable<EmployeeIssueProjection> source, string selectedMonth)
        {
            if (string.IsNullOrWhiteSpace(selectedMonth) || selectedMonth == "Все месяцы")
                return source;

            var ru = new CultureInfo("ru-RU");
            if (!DateTime.TryParseExact(selectedMonth, "MMMM yyyy", ru, DateTimeStyles.None, out var monthDate))
                return source;

            var monthStart = new DateTime(monthDate.Year, monthDate.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var now = DateTime.Now;
            return source.Where(x => x.Issue.Start < monthEnd && (x.Issue.IsInProgress ? now : x.Issue.End) >= monthStart);
        }

        private IEnumerable<EmployeeIssueProjection> FilterEmployeeIssuesBySelectedSubdivision(
            IEnumerable<EmployeeIssueProjection> source, string selectedSubdivision)
        {
            if (string.IsNullOrWhiteSpace(selectedSubdivision)
                || string.Equals(selectedSubdivision, "Все группы", StringComparison.CurrentCultureIgnoreCase))
                return source;

            if (string.Equals(selectedSubdivision, "Без группы", StringComparison.CurrentCultureIgnoreCase))
                return source.Where(x => string.IsNullOrWhiteSpace(x.Equipment.Subdivision));

            return source.Where(x => string.Equals(x.Equipment.Subdivision?.Trim(), selectedSubdivision, StringComparison.CurrentCultureIgnoreCase));
        }
    }
}
