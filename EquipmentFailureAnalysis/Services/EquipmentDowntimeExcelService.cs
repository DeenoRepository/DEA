using ClosedXML.Excel;
using EquipmentFailureAnalysis.Models;
using System;
using System.IO;

namespace EquipmentFailureAnalysis.Services
{
    public sealed class EquipmentDowntimeExcelService
    {
        public bool ExportToExcel(EquipmentDowntimeLossReport report, string filePath)
        {
            try
            {
                using var workbook = new XLWorkbook();

                // ==========================================
                // SHEET 1: Структура потерь
                // ==========================================
                var sheet1 = workbook.Worksheets.Add("Структура потерь");

                var headerStyle = workbook.Style;
                headerStyle.Font.Bold = true;
                headerStyle.Fill.BackgroundColor = XLColor.FromHtml("#1E293B"); // Slate dark
                headerStyle.Font.FontColor = XLColor.White;
                headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerStyle.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                var subHeaderStyle = workbook.Style;
                subHeaderStyle.Font.Bold = true;
                subHeaderStyle.Fill.BackgroundColor = XLColor.FromHtml("#334155"); // Slate 700
                subHeaderStyle.Font.FontColor = XLColor.White;
                subHeaderStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                subHeaderStyle.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Fixed metadata headers (Col 1 to 3)
                sheet1.Range(1, 1, 1, 3).Merge().Value = "Информация об оборудовании";
                sheet1.Range(1, 1, 1, 3).Style = headerStyle;

                sheet1.Cell(2, 1).Value = "Подразделение";
                sheet1.Cell(2, 2).Value = "Наименование оборудования";
                sheet1.Cell(2, 3).Value = "Инвентарный номер";

                sheet1.Cell(2, 1).Style = subHeaderStyle;
                sheet1.Cell(2, 2).Style = subHeaderStyle;
                sheet1.Cell(2, 3).Style = subHeaderStyle;

                int col = 4;
                var periodHeaders = report.PeriodHeaders;

                // Period headers
                foreach (var period in periodHeaders)
                {
                    sheet1.Range(1, col, 1, col + 2).Merge().Value = period.PeriodLabel;
                    sheet1.Range(1, col, 1, col + 2).Style = headerStyle;

                    sheet1.Cell(2, col).Value = "Ремонт (ч)";
                    sheet1.Cell(2, col + 1).Value = "Настройка (ч)";
                    sheet1.Cell(2, col + 2).Value = "Всего (ч)";

                    sheet1.Cell(2, col).Style = subHeaderStyle;
                    sheet1.Cell(2, col + 1).Style = subHeaderStyle;
                    sheet1.Cell(2, col + 2).Style = subHeaderStyle;

                    col += 3;
                }

                // Grand total headers
                sheet1.Range(1, col, 1, col + 2).Merge().Value = "ИТОГО ЗА ПЕРИОД";
                sheet1.Range(1, col, 1, col + 2).Style = headerStyle;

                sheet1.Cell(2, col).Value = "Ремонт (ч)";
                sheet1.Cell(2, col + 1).Value = "Настройка (ч)";
                sheet1.Cell(2, col + 2).Value = "Всего (ч)";
                sheet1.Cell(2, col).Style = subHeaderStyle;
                sheet1.Cell(2, col + 1).Style = subHeaderStyle;
                sheet1.Cell(2, col + 2).Style = subHeaderStyle;

                int row = 3;

                foreach (var eqRow in report.Rows)
                {
                    sheet1.Cell(row, 1).Value = eqRow.Subdivision;
                    sheet1.Cell(row, 2).Value = eqRow.EquipmentTitle;
                    sheet1.Cell(row, 3).Value = eqRow.InventoryNumber;

                    sheet1.Cell(row, 2).Style.Font.Bold = true;

                    int curCol = 4;
                    foreach (var period in periodHeaders)
                    {
                        eqRow.PeriodBuckets.TryGetValue(period.PeriodKey, out var bucket);
                        double rHours = (bucket?.RepairMinutes ?? 0) / 60.0;
                        double sHours = (bucket?.SetupMinutes ?? 0) / 60.0;
                        double tHours = (bucket?.TotalMinutes ?? 0) / 60.0;

                        sheet1.Cell(row, curCol).Value = rHours;
                        sheet1.Cell(row, curCol + 1).Value = sHours;
                        sheet1.Cell(row, curCol + 2).Value = tHours;

                        sheet1.Cell(row, curCol).Style.NumberFormat.Format = "0.0";
                        sheet1.Cell(row, curCol + 1).Style.NumberFormat.Format = "0.0";
                        sheet1.Cell(row, curCol + 2).Style.NumberFormat.Format = "0.0";
                        sheet1.Cell(row, curCol + 2).Style.Font.Bold = true;

                        curCol += 3;
                    }

                    double totalR = eqRow.TotalRepairMinutes / 60.0;
                    double totalS = eqRow.TotalSetupMinutes / 60.0;
                    double totalT = eqRow.TotalDowntimeMinutes / 60.0;

                    sheet1.Cell(row, curCol).Value = totalR;
                    sheet1.Cell(row, curCol + 1).Value = totalS;
                    sheet1.Cell(row, curCol + 2).Value = totalT;

                    sheet1.Cell(row, curCol).Style.NumberFormat.Format = "0.0";
                    sheet1.Cell(row, curCol + 1).Style.NumberFormat.Format = "0.0";
                    sheet1.Cell(row, curCol + 2).Style.NumberFormat.Format = "0.0";
                    sheet1.Cell(row, curCol + 2).Style.Font.Bold = true;

                    // Highlight total cell if equipment downtime > 10 hours
                    if (totalT >= 10.0)
                    {
                        sheet1.Cell(row, curCol + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2"); // Light red
                        sheet1.Cell(row, curCol + 2).Style.Font.FontColor = XLColor.FromHtml("#991B1B"); // Dark red
                    }

                    row++;
                }

                // Summary Total Row
                var totalRowStyle = workbook.Style;
                totalRowStyle.Font.Bold = true;
                totalRowStyle.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0"); // Slate 200

                sheet1.Cell(row, 1).Value = "ИТОГО ПО ВСЕМУ ОБОРУДОВАНИЮ";
                sheet1.Range(row, 1, row, 3).Merge().Style = totalRowStyle;

                int sumCol = 4;
                foreach (var period in periodHeaders)
                {
                    double periodR = 0, periodS = 0, periodT = 0;
                    foreach (var eqRow in report.Rows)
                    {
                        if (eqRow.PeriodBuckets.TryGetValue(period.PeriodKey, out var b))
                        {
                            periodR += b.RepairMinutes;
                            periodS += b.SetupMinutes;
                            periodT += b.TotalMinutes;
                        }
                    }

                    sheet1.Cell(row, sumCol).Value = periodR / 60.0;
                    sheet1.Cell(row, sumCol + 1).Value = periodS / 60.0;
                    sheet1.Cell(row, sumCol + 2).Value = periodT / 60.0;

                    sheet1.Cell(row, sumCol).Style = totalRowStyle;
                    sheet1.Cell(row, sumCol + 1).Style = totalRowStyle;
                    sheet1.Cell(row, sumCol + 2).Style = totalRowStyle;

                    sheet1.Cell(row, sumCol).Style.NumberFormat.Format = "0.0";
                    sheet1.Cell(row, sumCol + 1).Style.NumberFormat.Format = "0.0";
                    sheet1.Cell(row, sumCol + 2).Style.NumberFormat.Format = "0.0";

                    sumCol += 3;
                }

                sheet1.Cell(row, sumCol).Value = report.TotalRepairMinutes / 60.0;
                sheet1.Cell(row, sumCol + 1).Value = report.TotalSetupMinutes / 60.0;
                sheet1.Cell(row, sumCol + 2).Value = report.GrandTotalMinutes / 60.0;

                sheet1.Cell(row, sumCol).Style = totalRowStyle;
                sheet1.Cell(row, sumCol + 1).Style = totalRowStyle;
                sheet1.Cell(row, sumCol + 2).Style = totalRowStyle;

                sheet1.Cell(row, sumCol).Style.NumberFormat.Format = "0.0";
                sheet1.Cell(row, sumCol + 1).Style.NumberFormat.Format = "0.0";
                sheet1.Cell(row, sumCol + 2).Style.NumberFormat.Format = "0.0";

                sheet1.Columns().AdjustToContents();
                sheet1.ShowGridLines = true;

                // ==========================================
                // SHEET 2: Описания и комментарии
                // ==========================================
                var sheet2 = workbook.Worksheets.Add("Описания и комментарии");

                // Banner headers
                sheet2.Range(1, 1, 1, 12).Merge().Value = "Журнал инцидентов, описаний и комментариев к оборудованию";
                sheet2.Range(1, 1, 1, 12).Style = headerStyle;
                sheet2.Row(1).Height = 26;

                var noteStyle = workbook.Style;
                noteStyle.Font.Italic = true;
                noteStyle.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9"); // Slate 100
                noteStyle.Font.FontColor = XLColor.FromHtml("#334155");
                noteStyle.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                sheet2.Range(2, 1, 2, 12).Merge().Value = "Связь с Листом 1 происходит по полю 'Наименование оборудования'. На основе текстов описаний и комментариев формируется общая формулировка причин неисправностей и частых проблем по каждому оборудованию.";
                sheet2.Range(2, 1, 2, 12).Style = noteStyle;
                sheet2.Row(2).Height = 22;

                // Table headers on Row 3
                string[] headersSheet2 = new[]
                {
                    "Подразделение",
                    "Наименование оборудования",
                    "Инвентарный номер",
                    "Ключ Jira",
                    "Тип события",
                    "Дата начала",
                    "Дата окончания",
                    "Длительность (ч)",
                    "Ответственный",
                    "Заявитель",
                    "Описание неисправности",
                    "Комментарии и примечания"
                };

                for (int i = 0; i < headersSheet2.Length; i++)
                {
                    var cell = sheet2.Cell(3, i + 1);
                    cell.Value = headersSheet2[i];
                    cell.Style = subHeaderStyle;
                }
                sheet2.Row(3).Height = 24;

                int s2Row = 4;
                foreach (var detail in report.AllIssueDetails)
                {
                    sheet2.Cell(s2Row, 1).Value = detail.Subdivision;
                    sheet2.Cell(s2Row, 2).Value = detail.EquipmentTitle;
                    sheet2.Cell(s2Row, 3).Value = detail.InventoryNumber;
                    sheet2.Cell(s2Row, 4).Value = detail.JiraIssueKey;
                    sheet2.Cell(s2Row, 5).Value = detail.IssueType;
                    sheet2.Cell(s2Row, 6).Value = detail.Start.ToString("yyyy-MM-dd HH:mm");
                    sheet2.Cell(s2Row, 7).Value = detail.End.ToString("yyyy-MM-dd HH:mm");

                    double durHours = detail.DurationMinutes / 60.0;
                    sheet2.Cell(s2Row, 8).Value = durHours;
                    sheet2.Cell(s2Row, 8).Style.NumberFormat.Format = "0.0";

                    sheet2.Cell(s2Row, 9).Value = detail.Responsible;
                    sheet2.Cell(s2Row, 10).Value = detail.Reporter;

                    var descCell = sheet2.Cell(s2Row, 11);
                    descCell.Value = detail.Description;
                    descCell.Style.Alignment.WrapText = true;

                    var commCell = sheet2.Cell(s2Row, 12);
                    commCell.Value = detail.Comments;
                    commCell.Style.Alignment.WrapText = true;

                    sheet2.Cell(s2Row, 2).Style.Font.Bold = true;

                    s2Row++;
                }

                sheet2.Columns(1, 10).AdjustToContents();
                sheet2.Column(11).Width = 50; // Description column width
                sheet2.Column(12).Width = 50; // Comments column width
                sheet2.ShowGridLines = true;

                workbook.SaveAs(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
