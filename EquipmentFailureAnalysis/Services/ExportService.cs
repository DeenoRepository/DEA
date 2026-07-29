using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EquipmentFailureAnalysis.Services
{
    public sealed class ExportService
    {
        public bool ExportToCsv(MainWindowViewModel vm, HtmlReportOptions options, string filePath)
        {
            try
            {
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
                            return new
                            {
                                Start = issue.Start,
                                End = issue.End,
                                EquipmentTitle = eq.Title ?? string.Empty,
                                InventoryNumber = eq.InventoryNumber ?? string.Empty,
                                Subdivision = eq.Subdivision ?? string.Empty,
                                Responsible = string.IsNullOrWhiteSpace(issue.Responsible) ? "Без ответственного" : issue.Responsible.Trim(),
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

                var csv = new StringBuilder();
                csv.Append("\uFEFF"); // UTF-8 BOM

                csv.AppendLine("Отчет по метрикам оборудования (DEA);");
                csv.AppendLine($"Период:;{options.StartDate:dd.MM.yyyy} - {options.EndDate:dd.MM.yyyy}");
                csv.AppendLine($"Сформирован:;{DateTime.Now:dd.MM.yyyy HH:mm}");
                csv.AppendLine();

                if (options.IncludeDashboard)
                {
                    csv.AppendLine("Панель управления;");
                    csv.AppendLine($"События (30 дней);{vm.Dashboard.DashboardCurrentPeriodIssues}");
                    csv.AppendLine($"SLA;{vm.Dashboard.DashboardCurrentPeriodSlaCompliancePercent:0.#}%");
                    csv.AppendLine($"Средняя длительность;{vm.Dashboard.DashboardCurrentPeriodAvgDuration}");
                    csv.AppendLine($"Зона внимания;{vm.Dashboard.DashboardRiskEquipment} ({vm.Dashboard.DashboardRiskEquipmentValue})");
                    csv.AppendLine();
                }

                if (options.IncludeDowntime)
                {
                    var downtimeTotalIssues = reportRows.Count;
                    var downtimeRepairs = reportRows.Count(x => x.Type == IssueType.Ремонт);
                    var downtimeSetups = reportRows.Count(x => x.Type == IssueType.Настройка);
                    var downtimeTotalMinutes = reportRows.Sum(x => x.DurationMinutes);
                    var downtimeAffectedEquipment = reportRows
                        .Select(x => x.InventoryNumber)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();

                    csv.AppendLine("Анализ простоев;");
                    csv.AppendLine($"События;{downtimeTotalIssues}");
                    csv.AppendLine($"Ремонты;{downtimeRepairs}");
                    csv.AppendLine($"Настройки;{downtimeSetups}");
                    csv.AppendLine($"Задействовано оборудования;{downtimeAffectedEquipment}");
                    csv.AppendLine($"Суммарный простой;{FormatDuration(downtimeTotalMinutes)}");
                    csv.AppendLine();
                }

                // Grouping Table
                var ru = new CultureInfo("ru-RU");
                var groupByKey = options.GroupBy;
                if (string.IsNullOrWhiteSpace(groupByKey)) groupByKey = "day";
                if (groupByKey.Contains("меся", StringComparison.CurrentCultureIgnoreCase)) groupByKey = "month";
                else if (groupByKey.Contains("сотруд", StringComparison.CurrentCultureIgnoreCase)) groupByKey = "employee";
                else if (groupByKey.Contains("оборуд", StringComparison.CurrentCultureIgnoreCase)) groupByKey = "equipment";
                else groupByKey = "day";

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

                var groupHeaderName = groupByKey switch
                {
                    "month" => "Месяц",
                    "employee" => "Сотрудник",
                    "equipment" => "Оборудование",
                    _ => "День"
                };

                csv.AppendLine($"Группировка: {groupHeaderName};");
                csv.AppendLine("Группа;События;Ремонты;Настройки;Средняя длительность;Суммарная длительность");
                foreach (var g in grouped)
                {
                    csv.AppendLine($"{g.GroupName};{g.Total};{g.Repairs};{g.Setups};{FormatTimeSpan(TimeSpan.FromMinutes(g.AvgMinutes))};{FormatTimeSpan(TimeSpan.FromMinutes(g.TotalMinutes))}");
                }
                csv.AppendLine();

                // Details Table
                var headers = new List<string>();
                var rowExtractors = new List<Func<dynamic, string>>();

                if (options.ShowStart) { headers.Add("Начало"); rowExtractors.Add(x => x.Start.ToString("dd.MM.yyyy HH:mm")); }
                if (options.ShowEnd) { headers.Add("Окончание"); rowExtractors.Add(x => x.IsInProgress ? "В процессе" : x.End.ToString("dd.MM.yyyy HH:mm")); }
                if (options.ShowEquipment) { headers.Add("Оборудование"); rowExtractors.Add(x => string.IsNullOrWhiteSpace(x.InventoryNumber) ? x.EquipmentTitle : $"{x.EquipmentTitle} ({x.InventoryNumber})"); }
                if (options.ShowSubdivision) { headers.Add("Группа"); rowExtractors.Add(x => string.IsNullOrWhiteSpace(x.Subdivision) ? "-" : x.Subdivision); }
                if (options.ShowType) { headers.Add("Тип"); rowExtractors.Add(x => x.Type.ToString()); }
                if (options.ShowResponsible) { headers.Add("Ответственный"); rowExtractors.Add(x => x.Responsible); }
                if (options.ShowDescription) { headers.Add("Описание"); rowExtractors.Add(x => EscapeCsvCell(x.Description)); }

                if (headers.Count > 0)
                {
                    csv.AppendLine("Детализация событий;");
                    csv.AppendLine(string.Join(";", headers));
                    foreach (var item in reportRows.OrderByDescending(x => x.Start))
                    {
                        var cells = rowExtractors.Select(ext => ext(item));
                        csv.AppendLine(string.Join(";", cells));
                    }
                }

                File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool ExportToPdf(MainWindowViewModel vm, HtmlReportOptions options, string filePath)
        {
            try
            {
                var now = DateTime.Now;
                var periodEndExclusive = options.EndDate.AddDays(1);
                var reportRows = vm.GetEquipmentForReports()
                    .SelectMany(eq => (eq.Issues ?? new ObservableCollection<Issue>())
                        .Where(issue => issue.End > options.StartDate && issue.Start < periodEndExclusive)
                        .Select(issue =>
                        {
                            var issueEnd = issue.IsInProgress ? now : issue.End;
                            var overlapStart = issue.Start < options.StartDate ? options.StartDate : issue.Start;
                            var overlapEnd = issueEnd > periodEndExclusive ? periodEndExclusive : issueEnd;
                            var duration = Math.Max(0, (overlapEnd - overlapStart).TotalMinutes);
                            return new
                            {
                                Start = issue.Start,
                                End = issue.End,
                                EquipmentTitle = eq.Title ?? string.Empty,
                                InventoryNumber = eq.InventoryNumber ?? string.Empty,
                                Subdivision = eq.Subdivision ?? string.Empty,
                                Responsible = string.IsNullOrWhiteSpace(issue.Responsible) ? "Без ответственного" : issue.Responsible.Trim(),
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

                using (var stream = File.Create(filePath))
                using (var document = SkiaSharp.SKDocument.CreatePdf(stream))
                {
                    float width = 595;
                    float height = 842;
                    float margin = 40;

                    var paintTitle = new SkiaSharp.SKPaint
                    {
                        Color = SkiaSharp.SKColors.Black,
                        TextSize = 16,
                        IsAntialias = true,
                        Typeface = SkiaSharp.SKTypeface.FromFamilyName("Arial", SkiaSharp.SKFontStyleWeight.Bold, SkiaSharp.SKFontStyleWidth.Normal, SkiaSharp.SKFontStyleSlant.Upright)
                    };

                    var paintHeader = new SkiaSharp.SKPaint
                    {
                        Color = SkiaSharp.SKColors.DarkSlateGray,
                        TextSize = 12,
                        IsAntialias = true,
                        Typeface = SkiaSharp.SKTypeface.FromFamilyName("Arial", SkiaSharp.SKFontStyleWeight.Bold, SkiaSharp.SKFontStyleWidth.Normal, SkiaSharp.SKFontStyleSlant.Upright)
                    };

                    var paintText = new SkiaSharp.SKPaint
                    {
                        Color = SkiaSharp.SKColors.Black,
                        TextSize = 9,
                        IsAntialias = true,
                        Typeface = SkiaSharp.SKTypeface.FromFamilyName("Arial")
                    };

                    var paintTextBold = new SkiaSharp.SKPaint
                    {
                        Color = SkiaSharp.SKColors.Black,
                        TextSize = 9,
                        IsAntialias = true,
                        Typeface = SkiaSharp.SKTypeface.FromFamilyName("Arial", SkiaSharp.SKFontStyleWeight.Bold, SkiaSharp.SKFontStyleWidth.Normal, SkiaSharp.SKFontStyleSlant.Upright)
                    };

                    var paintMuted = new SkiaSharp.SKPaint
                    {
                        Color = SkiaSharp.SKColors.Gray,
                        TextSize = 9,
                        IsAntialias = true,
                        Typeface = SkiaSharp.SKTypeface.FromFamilyName("Arial")
                    };

                    float y = margin;
                    var canvas = document.BeginPage(width, height);

                    void DrawHeader(SkiaSharp.SKCanvas c, int pageNum)
                    {
                        c.DrawText("DEA - Equipment Failure Analysis Report", margin, margin - 15, paintMuted);
                        c.DrawLine(margin, margin - 10, width - margin, margin - 10, new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.LightGray });
                    }

                    void DrawFooter(SkiaSharp.SKCanvas c, int pageNum)
                    {
                        c.DrawLine(margin, height - margin + 10, width - margin, height - margin + 10, new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.LightGray });
                        c.DrawText($"Страница {pageNum}", width - margin - 50, height - margin + 22, paintMuted);
                        c.DrawText($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}", margin, height - margin + 22, paintMuted);
                    }

                    int pageNumber = 1;
                    DrawHeader(canvas, pageNumber);
                    DrawFooter(canvas, pageNumber);

                    // Title
                    canvas.DrawText("Отчет по надежности оборудования", margin, y + 20, paintTitle);
                    y += 35;

                    canvas.DrawText($"Период: {options.StartDate:dd.MM.yyyy} - {options.EndDate:dd.MM.yyyy}", margin, y, paintText);
                    y += 20;

                    // Summary statistics block
                    if (options.IncludeDashboard || options.IncludeDowntime)
                    {
                        canvas.DrawText("Сводные показатели", margin, y, paintHeader);
                        y += 15;

                        if (options.IncludeDashboard)
                        {
                            canvas.DrawText($"События (30д): {vm.Dashboard.DashboardCurrentPeriodIssues}", margin + 10, y, paintText);
                            canvas.DrawText($"SLA compliance: {vm.Dashboard.DashboardCurrentPeriodSlaCompliancePercent:0.#}%", margin + 150, y, paintText);
                            canvas.DrawText($"Avg Duration: {vm.Dashboard.DashboardCurrentPeriodAvgDuration}", margin + 320, y, paintText);
                            y += 15;
                            canvas.DrawText($"Зона внимания: {vm.Dashboard.DashboardRiskEquipment} ({vm.Dashboard.DashboardRiskEquipmentValue})", margin + 10, y, paintText);
                            y += 20;
                        }

                        if (options.IncludeDowntime)
                        {
                            var totalIssues = reportRows.Count;
                            var totalMinutes = reportRows.Sum(x => x.DurationMinutes);
                            canvas.DrawText($"Всего простоев: {totalIssues}", margin + 10, y, paintText);
                            canvas.DrawText($"Общее время простоя: {FormatDuration(totalMinutes)}", margin + 180, y, paintText);
                            y += 25;
                        }
                    }

                    canvas.DrawText("Группировка показателей", margin, y, paintHeader);
                    y += 18;

                    canvas.DrawRect(margin, y - 11, width - 2 * margin, 16, new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.GhostWhite });
                    canvas.DrawText("Группа", margin + 5, y, paintTextBold);
                    canvas.DrawText("События", margin + 180, y, paintTextBold);
                    canvas.DrawText("Рем.", margin + 240, y, paintTextBold);
                    canvas.DrawText("Наст.", margin + 280, y, paintTextBold);
                    canvas.DrawText("Ср. длит.", margin + 330, y, paintTextBold);
                    canvas.DrawText("Суммарно", margin + 410, y, paintTextBold);
                    y += 14;

                    var ru = new CultureInfo("ru-RU");
                    var groupByKey = options.GroupBy;
                    if (string.IsNullOrWhiteSpace(groupByKey)) groupByKey = "day";
                    if (groupByKey.Contains("меся", StringComparison.CurrentCultureIgnoreCase)) groupByKey = "month";
                    else if (groupByKey.Contains("сотруд", StringComparison.CurrentCultureIgnoreCase)) groupByKey = "employee";
                    else if (groupByKey.Contains("оборуд", StringComparison.CurrentCultureIgnoreCase)) groupByKey = "equipment";
                    else groupByKey = "day";

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

                    foreach (var g in grouped.Take(12))
                    {
                        if (y > height - margin - 40)
                        {
                            document.EndPage();
                            pageNumber++;
                            canvas = document.BeginPage(width, height);
                            DrawHeader(canvas, pageNumber);
                            DrawFooter(canvas, pageNumber);
                            y = margin + 20;
                        }

                        canvas.DrawLine(margin, y - 10, width - margin, y - 10, new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.WhiteSmoke });
                        
                        var nameStr = g.GroupName;
                        if (nameStr.Length > 32) nameStr = nameStr.Substring(0, 30) + "..";
                        canvas.DrawText(nameStr, margin + 5, y, paintText);
                        canvas.DrawText(g.Total.ToString(), margin + 180, y, paintText);
                        canvas.DrawText(g.Repairs.ToString(), margin + 240, y, paintText);
                        canvas.DrawText(g.Setups.ToString(), margin + 280, y, paintText);
                        canvas.DrawText(FormatTimeSpan(TimeSpan.FromMinutes(g.AvgMinutes)), margin + 330, y, paintText);
                        canvas.DrawText(FormatTimeSpan(TimeSpan.FromMinutes(g.TotalMinutes)), margin + 410, y, paintText);
                        y += 16;
                    }

                    y += 15;

                    if (y > height - margin - 60)
                    {
                        document.EndPage();
                        pageNumber++;
                        canvas = document.BeginPage(width, height);
                        DrawHeader(canvas, pageNumber);
                        DrawFooter(canvas, pageNumber);
                        y = margin + 20;
                    }

                    canvas.DrawText("Детализация простоев (последние 20)", margin, y, paintHeader);
                    y += 18;

                    canvas.DrawRect(margin, y - 11, width - 2 * margin, 16, new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.GhostWhite });
                    canvas.DrawText("Дата", margin + 5, y, paintTextBold);
                    canvas.DrawText("Оборудование", margin + 100, y, paintTextBold);
                    canvas.DrawText("Тип", margin + 260, y, paintTextBold);
                    canvas.DrawText("Ответственный", margin + 320, y, paintTextBold);
                    canvas.DrawText("Длительность", margin + 430, y, paintTextBold);
                    y += 14;

                    foreach (var item in reportRows.OrderByDescending(x => x.Start).Take(20))
                    {
                        if (y > height - margin - 30)
                        {
                            document.EndPage();
                            pageNumber++;
                            canvas = document.BeginPage(width, height);
                            DrawHeader(canvas, pageNumber);
                            DrawFooter(canvas, pageNumber);
                            y = margin + 20;

                            canvas.DrawRect(margin, y - 11, width - 2 * margin, 16, new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.GhostWhite });
                            canvas.DrawText("Дата", margin + 5, y, paintTextBold);
                            canvas.DrawText("Оборудование", margin + 100, y, paintTextBold);
                            canvas.DrawText("Тип", margin + 260, y, paintTextBold);
                            canvas.DrawText("Ответственный", margin + 320, y, paintTextBold);
                            canvas.DrawText("Длительность", margin + 430, y, paintTextBold);
                            y += 14;
                        }

                        canvas.DrawLine(margin, y - 10, width - margin, y - 10, new SkiaSharp.SKPaint { Color = SkiaSharp.SKColors.WhiteSmoke });
                        canvas.DrawText(item.Start.ToString("dd.MM.yyyy HH:mm"), margin + 5, y, paintText);
                        
                        var eqStr = item.EquipmentTitle;
                        if (eqStr.Length > 28) eqStr = eqStr.Substring(0, 26) + "..";
                        canvas.DrawText(eqStr, margin + 100, y, paintText);
                        canvas.DrawText(item.Type.ToString(), margin + 260, y, paintText);

                        var respStr = item.Responsible;
                        if (respStr.Length > 18) respStr = respStr.Substring(0, 16) + "..";
                        canvas.DrawText(respStr, margin + 320, y, paintText);
                        canvas.DrawText(FormatTimeSpan(TimeSpan.FromMinutes(item.DurationMinutes)), margin + 430, y, paintText);
                        y += 16;
                    }

                    document.EndPage();
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeCsvCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace(";", " ").Replace("\r", " ").Replace("\n", " ");
        }

        private static string FormatDuration(double totalMinutes)
        {
            var safeMinutes = Math.Max(0, totalMinutes);
            var wholeMinutes = (int)Math.Round(safeMinutes, MidpointRounding.AwayFromZero);
            var hours = wholeMinutes / 60;
            var minutes = wholeMinutes % 60;
            return $"{hours} ч {minutes:00} мин";
        }

        private static string FormatTimeSpan(TimeSpan ts)
        {
            var hours = (int)ts.TotalHours;
            return $"{hours:00}:{ts.Minutes:00}";
        }
    }
}
