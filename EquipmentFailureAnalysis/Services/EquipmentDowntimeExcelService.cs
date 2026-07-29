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
                var worksheet = workbook.Worksheets.Add("Структура потерь");

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

                // Fixed metadata headers
                worksheet.Range(1, 1, 1, 3).Merge().Value = "Информация об оборудовании";
                worksheet.Range(1, 1, 1, 3).Style = headerStyle;

                worksheet.Cell(2, 1).Value = "Подразделение";
                worksheet.Cell(2, 2).Value = "Наименование оборудования";
                worksheet.Cell(2, 3).Value = "Инвентарный номер";
                worksheet.Cell(2, 1).Style = subHeaderStyle;
                worksheet.Cell(2, 2).Style = subHeaderStyle;
                worksheet.Cell(2, 3).Style = subHeaderStyle;

                int col = 4;
                var periodHeaders = report.PeriodHeaders;

                // Period headers
                foreach (var period in periodHeaders)
                {
                    worksheet.Range(1, col, 1, col + 2).Merge().Value = period.PeriodLabel;
                    worksheet.Range(1, col, 1, col + 2).Style = headerStyle;

                    worksheet.Cell(2, col).Value = "Ремонт (ч)";
                    worksheet.Cell(2, col + 1).Value = "Настройка (ч)";
                    worksheet.Cell(2, col + 2).Value = "Всего (ч)";

                    worksheet.Cell(2, col).Style = subHeaderStyle;
                    worksheet.Cell(2, col + 1).Style = subHeaderStyle;
                    worksheet.Cell(2, col + 2).Style = subHeaderStyle;

                    col += 3;
                }

                // Grand total headers
                worksheet.Range(1, col, 1, col + 2).Merge().Value = "ИТОГО ЗА ПЕРИОД";
                worksheet.Range(1, col, 1, col + 2).Style = headerStyle;

                worksheet.Cell(2, col).Value = "Ремонт (ч)";
                worksheet.Cell(2, col + 1).Value = "Настройка (ч)";
                worksheet.Cell(2, col + 2).Value = "Всего (ч)";
                worksheet.Cell(2, col).Style = subHeaderStyle;
                worksheet.Cell(2, col + 1).Style = subHeaderStyle;
                worksheet.Cell(2, col + 2).Style = subHeaderStyle;

                int row = 3;

                foreach (var eqRow in report.Rows)
                {
                    worksheet.Cell(row, 1).Value = eqRow.Subdivision;
                    worksheet.Cell(row, 2).Value = eqRow.EquipmentTitle;
                    worksheet.Cell(row, 3).Value = eqRow.InventoryNumber;

                    int curCol = 4;
                    foreach (var period in periodHeaders)
                    {
                        eqRow.PeriodBuckets.TryGetValue(period.PeriodKey, out var bucket);
                        double rHours = (bucket?.RepairMinutes ?? 0) / 60.0;
                        double sHours = (bucket?.SetupMinutes ?? 0) / 60.0;
                        double tHours = (bucket?.TotalMinutes ?? 0) / 60.0;

                        worksheet.Cell(row, curCol).Value = rHours;
                        worksheet.Cell(row, curCol + 1).Value = sHours;
                        worksheet.Cell(row, curCol + 2).Value = tHours;

                        worksheet.Cell(row, curCol).Style.NumberFormat.Format = "0.0";
                        worksheet.Cell(row, curCol + 1).Style.NumberFormat.Format = "0.0";
                        worksheet.Cell(row, curCol + 2).Style.NumberFormat.Format = "0.0";
                        worksheet.Cell(row, curCol + 2).Style.Font.Bold = true;

                        curCol += 3;
                    }

                    double totalR = eqRow.TotalRepairMinutes / 60.0;
                    double totalS = eqRow.TotalSetupMinutes / 60.0;
                    double totalT = eqRow.TotalDowntimeMinutes / 60.0;

                    worksheet.Cell(row, curCol).Value = totalR;
                    worksheet.Cell(row, curCol + 1).Value = totalS;
                    worksheet.Cell(row, curCol + 2).Value = totalT;

                    worksheet.Cell(row, curCol).Style.NumberFormat.Format = "0.0";
                    worksheet.Cell(row, curCol + 1).Style.NumberFormat.Format = "0.0";
                    worksheet.Cell(row, curCol + 2).Style.NumberFormat.Format = "0.0";
                    worksheet.Cell(row, curCol + 2).Style.Font.Bold = true;

                    // Highlight total cell if equipment downtime > 10 hours
                    if (totalT >= 10.0)
                    {
                        worksheet.Cell(row, curCol + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2"); // Light red
                        worksheet.Cell(row, curCol + 2).Style.Font.FontColor = XLColor.FromHtml("#991B1B"); // Dark red
                    }

                    row++;
                }

                // Summary Total Row
                var totalRowStyle = workbook.Style;
                totalRowStyle.Font.Bold = true;
                totalRowStyle.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0"); // Slate 200

                worksheet.Cell(row, 1).Value = "ИТОГО ПО ВСЕМУ ОБОРУДОВАНИЮ";
                worksheet.Range(row, 1, row, 3).Merge().Style = totalRowStyle;

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

                    worksheet.Cell(row, sumCol).Value = periodR / 60.0;
                    worksheet.Cell(row, sumCol + 1).Value = periodS / 60.0;
                    worksheet.Cell(row, sumCol + 2).Value = periodT / 60.0;

                    worksheet.Cell(row, sumCol).Style = totalRowStyle;
                    worksheet.Cell(row, sumCol + 1).Style = totalRowStyle;
                    worksheet.Cell(row, sumCol + 2).Style = totalRowStyle;

                    worksheet.Cell(row, sumCol).Style.NumberFormat.Format = "0.0";
                    worksheet.Cell(row, sumCol + 1).Style.NumberFormat.Format = "0.0";
                    worksheet.Cell(row, sumCol + 2).Style.NumberFormat.Format = "0.0";

                    sumCol += 3;
                }

                worksheet.Cell(row, sumCol).Value = report.TotalRepairMinutes / 60.0;
                worksheet.Cell(row, sumCol + 1).Value = report.TotalSetupMinutes / 60.0;
                worksheet.Cell(row, sumCol + 2).Value = report.GrandTotalMinutes / 60.0;

                worksheet.Cell(row, sumCol).Style = totalRowStyle;
                worksheet.Cell(row, sumCol + 1).Style = totalRowStyle;
                worksheet.Cell(row, sumCol + 2).Style = totalRowStyle;

                worksheet.Cell(row, sumCol).Style.NumberFormat.Format = "0.0";
                worksheet.Cell(row, sumCol + 1).Style.NumberFormat.Format = "0.0";
                worksheet.Cell(row, sumCol + 2).Style.NumberFormat.Format = "0.0";

                worksheet.Columns().AdjustToContents();
                worksheet.ShowGridLines = true;

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
