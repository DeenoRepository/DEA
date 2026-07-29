using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Services;
using EquipmentFailureAnalysis.ViewModels;
using Xunit;

namespace EquipmentFailureAnalysis.Tests
{
    public class HtmlReportServiceTests
    {
        [Fact]
        public void GenerateHtmlReport_ShouldFail_WhenEndDateBeforeStartDate()
        {
            // Arrange
            var vm = new MainWindowViewModel();
            var service = new HtmlReportService();
            var options = new HtmlReportOptions
            {
                StartDate = new DateTime(2026, 6, 15),
                EndDate = new DateTime(2026, 6, 14) // Invalid: before StartDate
            };

            // Act
            var result = service.GenerateHtmlReport(vm, options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Дата окончания периода не может быть меньше даты начала.", result.ErrorMessage);
        }

        [Fact]
        public void GenerateHtmlReport_ShouldFail_WhenNoDetailColumnsSelected()
        {
            // Arrange
            var vm = new MainWindowViewModel();
            var service = new HtmlReportService();
            var options = new HtmlReportOptions
            {
                StartDate = new DateTime(2026, 6, 1),
                EndDate = new DateTime(2026, 6, 15),
                ShowStart = false,
                ShowEnd = false,
                ShowEquipment = false,
                ShowSubdivision = false,
                ShowType = false,
                ShowResponsible = false,
                ShowDescription = false // None selected
            };

            // Act
            var result = service.GenerateHtmlReport(vm, options);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("Выберите хотя бы одно поле в блоке «Поля детализации отчета».", result.ErrorMessage);
        }

        [Fact]
        public void GenerateHtmlReport_ShouldSucceed_AndWriteHtmlFile()
        {
            // Arrange
            var vm = new MainWindowViewModel();
            var service = new HtmlReportService();
            
            // Populate sample equipment
            var equipment = new EquipmentInfo
            {
                Uid = 999,
                Title = "Test CNC Machine",
                InventoryNumber = "CNC-999",
                Subdivision = "Сектор сборки"
            };
            var issue = new Issue
            {
                Start = new DateTime(2026, 6, 2, 10, 0, 0),
                End = new DateTime(2026, 6, 2, 11, 30, 0),
                Description = "Replaced motor belt",
                Type = IssueType.Ремонт,
                Responsible = "Алексей Иванов"
            };
            equipment.Issues.Add(issue);
            
            var list = new ObservableCollection<EquipmentInfo> { equipment };
            vm.ImportEquipment(list);

            var options = new HtmlReportOptions
            {
                StartDate = new DateTime(2026, 6, 1),
                EndDate = new DateTime(2026, 6, 15),
                GroupBy = "employee",
                IncludeDashboard = true,
                IncludeDowntime = true,
                IncludeEmployee = true,
                ShowStart = true,
                ShowEnd = true,
                ShowEquipment = true,
                ShowSubdivision = true,
                ShowType = true,
                ShowResponsible = true,
                ShowDescription = true
            };

            // Act
            var result = service.GenerateHtmlReport(vm, options);

            // Assert
            Assert.True(result.Success);
            Assert.NotEmpty(result.ReportPath);
            Assert.True(File.Exists(result.ReportPath));

            // Verify content contains written data
            var htmlContent = File.ReadAllText(result.ReportPath);
            Assert.Contains("Test CNC Machine", htmlContent);
            Assert.Contains("CNC-999", htmlContent);
            Assert.Contains("Алексей Иванов", htmlContent);
            Assert.Contains("Replaced motor belt", htmlContent);

            // Clean up
            try
            {
                File.Delete(result.ReportPath);
            }
            catch
            {
                // ignore deletion error in test environment
            }
        }
    }
}
