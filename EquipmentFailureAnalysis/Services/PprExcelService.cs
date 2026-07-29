using ClosedXML.Excel;
using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EquipmentFailureAnalysis.Services
{
    public class PprExcelService
    {
        public List<PprScheduleItem> Import(string filePath)
        {
            var result = new List<PprScheduleItem>();

            if (!File.Exists(filePath))
                return result;

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null)
                    return result;

                // Find header row and map columns dynamically
                int headerRowIndex = -1;
                int colSubdivision = -1;
                int colTitle = -1;
                int colInventory = -1;
                int colCommissionYear = -1;
                var monthCols = new int[12];
                for (int i = 0; i < 12; i++) monthCols[i] = -1;

                var lastRowUsed = worksheet.LastRowUsed();
                var lastColUsed = worksheet.LastColumnUsed();
                if (lastRowUsed == null || lastColUsed == null)
                    return result;

                var lastRow = lastRowUsed.RowNumber();
                var lastCol = lastColUsed.ColumnNumber();

                // Scan first 15 rows for the headers
                for (int r = 1; r <= Math.Min(15, lastRow); r++)
                {
                    bool isHeaderRow = false;
                    for (int c = 1; c <= lastCol; c++)
                    {
                        var cellVal = worksheet.Cell(r, c).GetString()?.Trim() ?? string.Empty;
                        if (cellVal.Contains("Наименование", StringComparison.OrdinalIgnoreCase) || 
                            cellVal.Contains("Инвентарный", StringComparison.OrdinalIgnoreCase))
                        {
                            isHeaderRow = true;
                            break;
                        }
                    }

                    if (isHeaderRow)
                    {
                        headerRowIndex = r;
                        for (int c = 1; c <= lastCol; c++)
                        {
                            var cellVal = worksheet.Cell(r, c).GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
                            if (cellVal.Contains("подразделение")) colSubdivision = c;
                            else if (cellVal.Contains("наименование")) colTitle = c;
                            else if (cellVal.Contains("инвентарный")) colInventory = c;
                            else if (cellVal.Contains("год ввода") || cellVal.Contains("дата ввода")) colCommissionYear = c;
                            else if (cellVal.StartsWith("янв")) monthCols[0] = c;
                            else if (cellVal.StartsWith("фев")) monthCols[1] = c;
                            else if (cellVal.StartsWith("мар")) monthCols[2] = c;
                            else if (cellVal.StartsWith("апр")) monthCols[3] = c;
                            else if (cellVal.StartsWith("май") || cellVal.StartsWith("мая")) monthCols[4] = c;
                            else if (cellVal.StartsWith("июн")) monthCols[5] = c;
                            else if (cellVal.StartsWith("июл")) monthCols[6] = c;
                            else if (cellVal.StartsWith("авг")) monthCols[7] = c;
                            else if (cellVal.StartsWith("сен")) monthCols[8] = c;
                            else if (cellVal.StartsWith("окт")) monthCols[9] = c;
                            else if (cellVal.StartsWith("ноя")) monthCols[10] = c;
                            else if (cellVal.StartsWith("дек")) monthCols[11] = c;
                        }
                        break;
                    }
                }

                // If headers not found, try default mapping
                if (headerRowIndex == -1)
                {
                    headerRowIndex = 1;
                    colSubdivision = 1;
                    colTitle = 2;
                    colInventory = 3;
                    colCommissionYear = 4;
                    for (int i = 0; i < 12; i++)
                    {
                        monthCols[i] = 5 + i;
                    }
                }

                // Parse data rows
                for (int r = headerRowIndex + 1; r <= lastRow; r++)
                {
                    var title = colTitle > 0 ? worksheet.Cell(r, colTitle).GetString()?.Trim() : string.Empty;
                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    var subdivision = colSubdivision > 0 ? worksheet.Cell(r, colSubdivision).GetString()?.Trim() : string.Empty;
                    var inventory = colInventory > 0 ? worksheet.Cell(r, colInventory).GetString()?.Trim() : string.Empty;
                    var year = colCommissionYear > 0 ? worksheet.Cell(r, colCommissionYear).GetString()?.Trim() : string.Empty;

                    var item = new PprScheduleItem
                    {
                        Subdivision = string.IsNullOrWhiteSpace(subdivision) ? "Без группы" : subdivision,
                        EquipmentTitle = title,
                        InventoryNumber = string.IsNullOrWhiteSpace(inventory) ? "б/н" : inventory,
                        CommissionYear = string.IsNullOrWhiteSpace(year) ? string.Empty : year
                    };

                    for (int m = 0; m < 12; m++)
                    {
                        var mCol = monthCols[m];
                        if (mCol > 0)
                        {
                            var plan = worksheet.Cell(r, mCol).GetString()?.Trim();
                            item.MonthlyPlans[m] = string.IsNullOrWhiteSpace(plan) ? null : plan;
                        }
                    }

                    result.Add(item);
                }
            }

            return result;
        }

        public bool Export(string filePath, IEnumerable<PprScheduleItem> items)
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Лист1");

                    // Set header style
                    var headerStyle = workbook.Style;
                    headerStyle.Font.Bold = true;
                    headerStyle.Fill.BackgroundColor = XLColor.FromHtml("#1E293B"); // Slate dark
                    headerStyle.Font.FontColor = XLColor.White;
                    headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerStyle.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    // Headers
                    string[] headers = new[]
                    {
                        "Подразделение", "Наименование оборудования", "Инвентарный номер", "Год ввода",
                        "Январь", "Февраль", "Март", "Апрель", "Май", "Июнь",
                        "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"
                    };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = worksheet.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style = headerStyle;
                    }

                    int r = 2;
                    foreach (var item in items)
                    {
                        worksheet.Cell(r, 1).Value = item.Subdivision;
                        worksheet.Cell(r, 2).Value = item.EquipmentTitle;
                        worksheet.Cell(r, 3).Value = item.InventoryNumber;
                        worksheet.Cell(r, 4).Value = item.CommissionYear;

                        for (int m = 0; m < 12; m++)
                        {
                            var cell = worksheet.Cell(r, m + 5);
                            var plan = item.MonthlyPlans[m];
                            if (!string.IsNullOrEmpty(plan))
                            {
                                cell.Value = plan;
                                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                                if (item.MonthlyCompletions[m])
                                {
                                    // Completed is Green
                                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D1FAE5"); // Light green
                                    cell.Style.Font.FontColor = XLColor.FromHtml("#065F46"); // Dark green
                                    cell.Style.Font.Bold = true;
                                }
                                else
                                {
                                    // Planned/Uncompleted is Muted Gray-Blue
                                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9"); // Slate 100
                                    cell.Style.Font.FontColor = XLColor.FromHtml("#475569"); // Slate 600
                                }
                            }
                        }
                        r++;
                    }

                    // Auto-fit columns
                    worksheet.Columns().AdjustToContents();
                    
                    // Style grid lines
                    worksheet.ShowGridLines = true;

                    // Save workbook
                    workbook.SaveAs(filePath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
