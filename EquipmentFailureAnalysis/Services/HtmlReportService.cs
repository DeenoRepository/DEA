using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace EquipmentFailureAnalysis.Services
{
    public sealed class HtmlReportOptions
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string GroupBy { get; set; } = "day";
        public bool IncludeDashboard { get; set; } = true;
        public bool IncludeDowntime { get; set; } = true;
        public bool IncludeEmployee { get; set; } = true;
        public bool OnlyInProgress { get; set; }
        public bool FilterByDuration { get; set; }
        public double MinDurationMinutes { get; set; } = 60;
        public bool ShowStart { get; set; } = true;
        public bool ShowEnd { get; set; } = true;
        public bool ShowEquipment { get; set; } = true;
        public bool ShowSubdivision { get; set; } = true;
        public bool ShowType { get; set; } = true;
        public bool ShowResponsible { get; set; } = true;
        public bool ShowDescription { get; set; } = true;
    }

    public sealed class HtmlReportResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ReportPath { get; set; } = string.Empty;
    }

    public sealed class HtmlReportService
    {
        private sealed class ReportIssueRow
        {
            public DateTime Start { get; init; }
            public DateTime End { get; init; }
            public string EquipmentTitle { get; init; } = string.Empty;
            public string InventoryNumber { get; init; } = string.Empty;
            public string Subdivision { get; init; } = string.Empty;
            public string Responsible { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public IssueType Type { get; init; }
            public bool IsInProgress { get; init; }
            public double DurationMinutes { get; init; }
        }

        public HtmlReportResult GenerateHtmlReport(MainWindowViewModel vm, HtmlReportOptions options)
        {
            if (options.EndDate < options.StartDate)
            {
                return new HtmlReportResult
                {
                    Success = false,
                    ErrorMessage = "Дата окончания периода не может быть меньше даты начала."
                };
            }

            var now = DateTime.Now;
            var periodEndExclusive = options.EndDate.AddDays(1);
            var reportRows = vm.GetEquipmentForReports()
                .SelectMany(eq => (eq.Issues ?? new ObservableCollection<Issue>())
                    .Where(issue => (issue.IsInProgress ? now : issue.End) > options.StartDate && issue.Start < periodEndExclusive)
                    .Select(issue =>
                    {
                        var issueEnd = issue.IsInProgress ? now : issue.End;
                        var overlapStart = issue.Start < options.StartDate ? options.StartDate : issue.Start;
                        var overlapEnd = issueEnd > periodEndExclusive ? periodEndExclusive : issueEnd;
                        var duration = Math.Max(0, (overlapEnd - overlapStart).TotalMinutes);
                        return new ReportIssueRow
                        {
                            Start = issue.Start,
                            End = issue.End,
                            EquipmentTitle = eq.Title ?? string.Empty,
                            InventoryNumber = eq.InventoryNumber ?? string.Empty,
                            Subdivision = eq.Subdivision ?? string.Empty,
                            Responsible = string.IsNullOrWhiteSpace(issue.Responsible) ? "Без ответственного" : issue.Responsible!.Trim(),
                            Description = issue.Description ?? string.Empty,
                            Type = issue.Type,
                            IsInProgress = issue.IsInProgress,
                            DurationMinutes = duration
                        };
                    }))
                .ToList();

            if (options.OnlyInProgress)
                reportRows = reportRows.Where(r => r.IsInProgress).ToList();

            if (options.FilterByDuration)
                reportRows = reportRows.Where(r => r.DurationMinutes >= options.MinDurationMinutes).ToList();

            var ru = new CultureInfo("ru-RU");
            var groupByKey = NormalizeGroupByKey(options.GroupBy);
            var grouped = reportRows
                .GroupBy(row => groupByKey switch
                {
                    "month" => new DateTime(row.Start.Year, row.Start.Month, 1).ToString("MMMM yyyy", ru),
                    "employee" => row.Responsible,
                    "equipment" => string.IsNullOrWhiteSpace(row.InventoryNumber) ? row.EquipmentTitle : $"{row.EquipmentTitle} ({row.InventoryNumber})",
                    _ => row.Start.Date.ToString("dd.MM.yyyy")
                })
                .Select(g => new
                {
                    GroupName = g.Key,
                    Total = g.Count(),
                    Repairs = g.Count(x => x.Type == IssueType.Ремонт),
                    Setups = g.Count(x => x.Type == IssueType.Настройка),
                    AvgMinutes = g.Any() ? g.Average(x => x.DurationMinutes) : 0.0,
                    TotalMinutes = g.Sum(x => x.DurationMinutes)
                })
                .OrderByDescending(x => x.Total)
                .ThenBy(x => x.GroupName)
                .ToList();

            var downtimeTotalIssues = reportRows.Count;
            var downtimeRepairs = reportRows.Count(x => x.Type == IssueType.Ремонт);
            var downtimeSetups = reportRows.Count(x => x.Type == IssueType.Настройка);
            var downtimeTotalMinutes = reportRows.Sum(x => x.DurationMinutes);
            var downtimeAffectedEquipment = reportRows
                .Select(x => x.InventoryNumber)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            string H(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

            var detailsColumns = new List<(string Header, Func<ReportIssueRow, string> Value)>
            {
                ("Начало", x => x.Start.ToString("dd.MM.yyyy HH:mm")),
                ("Окончание / длительность", x => x.IsInProgress
                    ? FormatTimeSpan(TimeSpan.FromMinutes(Math.Max(0, (DateTime.Now - x.Start).TotalMinutes)))
                    : x.End.ToString("dd.MM.yyyy HH:mm")),
                ("Оборудование", x => string.IsNullOrWhiteSpace(x.InventoryNumber) ? x.EquipmentTitle : $"{x.EquipmentTitle} ({x.InventoryNumber})"),
                ("Группа", x => string.IsNullOrWhiteSpace(x.Subdivision) ? "-" : x.Subdivision),
                ("Тип", x => x.Type.ToString()),
                ("Ответственный", x => x.Responsible),
                ("Описание", x => x.Description)
            };

            detailsColumns = detailsColumns
                .Where((x, index) => index switch
                {
                    0 => options.ShowStart,
                    1 => options.ShowEnd,
                    2 => options.ShowEquipment,
                    3 => options.ShowSubdivision,
                    4 => options.ShowType,
                    5 => options.ShowResponsible,
                    6 => options.ShowDescription,
                    _ => true
                })
                .ToList();

            if (detailsColumns.Count == 0)
            {
                return new HtmlReportResult
                {
                    Success = false,
                    ErrorMessage = "Выберите хотя бы одно поле в блоке «Поля детализации отчета»."
                };
            }

            var html = new StringBuilder();
            html.AppendLine("<!doctype html>");
            html.AppendLine("<html lang=\"ru\"><head><meta charset=\"utf-8\" />");
            html.AppendLine("<title>Отчет DEA</title>");
            html.AppendLine("<style>@page{size:A4;margin:10mm}body{font-family:'Segoe UI',Arial,sans-serif;background:#f4f7fb;color:#1f2937;margin:12px;font-size:12px;line-height:1.2}h1{font-size:18px;margin:0 0 8px}h2{font-size:14px;margin:0 0 8px}section{background:#fff;border:1px solid #e5eaf0;padding:10px;margin:0 0 10px;break-inside:avoid-page}table{width:100%;border-collapse:collapse;font-size:11px}th,td{border-bottom:1px solid #edf1f5;padding:4px 6px;text-align:left;vertical-align:top}th{background:#f8fafc}.muted{color:#6b7280;font-size:11px}@media print{body{margin:0;zoom:0.86;background:#fff}section{border-color:#d9dee5}}</style>");
            html.AppendLine("</head><body>");
            html.AppendLine($"<h1>Отчет по метрикам оборудования</h1><div class=\"muted\">Период: {options.StartDate:dd.MM.yyyy} - {options.EndDate:dd.MM.yyyy}. Сформирован: {DateTime.Now:dd.MM.yyyy HH:mm}</div>");

            if (options.OnlyInProgress)
                html.AppendLine("<div class=\"muted\">Режим отчета: только задачи в процессе на момент формирования.</div>");
            if (options.FilterByDuration)
                html.AppendLine($"<div class=\"muted\">Режим отчета: длительность задач от {options.MinDurationMinutes:0} мин.</div>");

            if (options.IncludeDashboard)
            {
                html.AppendLine("<section><h2>Панель управления</h2><table><tbody>");
                html.AppendLine($"<tr><th>События (30 дней)</th><td>{vm.Dashboard.DashboardCurrentPeriodIssues}</td></tr>");
                html.AppendLine($"<tr><th>SLA</th><td>{vm.Dashboard.DashboardCurrentPeriodSlaCompliancePercent:0.#}%</td></tr>");
                html.AppendLine($"<tr><th>Средняя длительность</th><td>{H(vm.Dashboard.DashboardCurrentPeriodAvgDuration)}</td></tr>");
                html.AppendLine($"<tr><th>Зона внимания</th><td>{H(vm.Dashboard.DashboardRiskEquipment)} ({H(vm.Dashboard.DashboardRiskEquipmentValue)})</td></tr>");
                html.AppendLine("</tbody></table></section>");
            }

            if (options.IncludeDowntime)
            {
                html.AppendLine("<section><h2>Анализ простоев</h2><table><tbody>");
                html.AppendLine($"<tr><th>События</th><td>{downtimeTotalIssues}</td></tr>");
                html.AppendLine($"<tr><th>Ремонты</th><td>{downtimeRepairs}</td></tr>");
                html.AppendLine($"<tr><th>Настройки</th><td>{downtimeSetups}</td></tr>");
                html.AppendLine($"<tr><th>Задействовано единиц оборудования</th><td>{downtimeAffectedEquipment}</td></tr>");
                html.AppendLine($"<tr><th>Суммарный простой</th><td>{FormatDuration(downtimeTotalMinutes)}</td></tr>");
                html.AppendLine("</tbody></table></section>");
            }

            if (options.IncludeEmployee)
            {
                html.AppendLine("<section><h2>Анализ сотрудников</h2><table><tbody>");
                html.AppendLine($"<tr><th>Сотрудники</th><td>{vm.EmployeeTotalCount}</td></tr>");
                html.AppendLine($"<tr><th>События</th><td>{vm.EmployeeTotalIssues}</td></tr>");
                html.AppendLine($"<tr><th>SLA</th><td>{vm.EmployeeSlaCompliancePercent:0.#}%</td></tr>");
                html.AppendLine($"<tr><th>Лидер по событиям</th><td>{H(vm.EmployeeTopByIssues)} ({H(vm.EmployeeTopByIssuesValue)})</td></tr>");
                html.AppendLine("</tbody></table></section>");
            }

            html.AppendLine($"<section><h2>Группировка: {H(GetGroupByCaption(groupByKey))}</h2><table><thead><tr><th>Группа</th><th>События</th><th>Рем.</th><th>Наст.</th><th>Ср. длительность</th><th>Суммарно</th></tr></thead><tbody>");
            foreach (var g in grouped)
                html.AppendLine($"<tr><td>{H(g.GroupName)}</td><td>{g.Total}</td><td>{g.Repairs}</td><td>{g.Setups}</td><td>{FormatTimeSpan(TimeSpan.FromMinutes(g.AvgMinutes))}</td><td>{FormatTimeSpan(TimeSpan.FromMinutes(g.TotalMinutes))}</td></tr>");
            if (grouped.Count == 0)
                html.AppendLine("<tr><td colspan=\"6\">Нет данных за выбранный период.</td></tr>");
            html.AppendLine("</tbody></table></section>");

            html.AppendLine($"<section><h2>Детализация событий</h2><table><thead><tr>{string.Join(string.Empty, detailsColumns.Select(c => $"<th>{H(c.Header)}</th>"))}</tr></thead><tbody>");
            foreach (var item in reportRows.OrderByDescending(x => x.Start).Take(300))
            {
                var rowCells = string.Join(string.Empty, detailsColumns.Select(c => $"<td>{H(c.Value(item))}</td>"));
                html.AppendLine($"<tr>{rowCells}</tr>");
            }

            if (reportRows.Count == 0)
                html.AppendLine($"<tr><td colspan=\"{detailsColumns.Count}\">Нет событий в выбранном периоде.</td></tr>");

            html.AppendLine("</tbody></table></section>");
            html.AppendLine("</body></html>");

            var outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EquipmentFailureAnalysis", "reports");
            Directory.CreateDirectory(outputDir);
            var reportPath = Path.Combine(outputDir, $"dea_report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            File.WriteAllText(reportPath, html.ToString(), new UTF8Encoding(false));

            return new HtmlReportResult
            {
                Success = true,
                ReportPath = reportPath
            };
        }

        private static string NormalizeGroupByKey(string? value)
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
                return "day";

            if (string.Equals(normalized, "day", StringComparison.OrdinalIgnoreCase))
                return "day";
            if (string.Equals(normalized, "month", StringComparison.OrdinalIgnoreCase))
                return "month";
            if (string.Equals(normalized, "employee", StringComparison.OrdinalIgnoreCase))
                return "employee";
            if (string.Equals(normalized, "equipment", StringComparison.OrdinalIgnoreCase))
                return "equipment";

            if (normalized.Contains("меся", StringComparison.CurrentCultureIgnoreCase))
                return "month";
            if (normalized.Contains("сотруд", StringComparison.CurrentCultureIgnoreCase))
                return "employee";
            if (normalized.Contains("оборуд", StringComparison.CurrentCultureIgnoreCase))
                return "equipment";

            return "day";
        }

        private static string GetGroupByCaption(string key) => key switch
        {
            "month" => "По месяцам",
            "employee" => "По сотрудникам",
            "equipment" => "По оборудованию",
            _ => "По дням"
        };

        private static string FormatTimeSpan(TimeSpan ts)
        {
            var hours = (int)ts.TotalHours;
            return $"{hours:00}:{ts.Minutes:00}";
        }

        private static string FormatDuration(double totalMinutes)
        {
            var safeMinutes = Math.Max(0, totalMinutes);
            var wholeMinutes = (int)Math.Round(safeMinutes, MidpointRounding.AwayFromZero);
            var hours = wholeMinutes / 60;
            var minutes = wholeMinutes % 60;
            return $"{hours} ч {minutes:00} мин";
        }
    }
}
