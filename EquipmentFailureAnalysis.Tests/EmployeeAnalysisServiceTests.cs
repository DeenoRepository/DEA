using System;
using System.Collections.Generic;
using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Services;
using Xunit;

namespace EquipmentFailureAnalysis.Tests
{
    public class EmployeeAnalysisServiceTests
    {
        [Fact]
        public void Analyze_ShouldCalculateSlaCorrectly()
        {
            // Arrange
            var service = new EmployeeAnalysisService();
            var equipment = new EquipmentInfo
            {
                Uid = 101,
                Title = "Тестовый станок",
                InventoryNumber = "101",
                Subdivision = "Сектор сборки"
            };

            var issueWithinSla = new Issue
            {
                Start = new DateTime(2026, 6, 1, 10, 0, 0),
                End = new DateTime(2026, 6, 1, 10, 20, 0), // 20 minutes (within 30 mins SLA)
                Description = "Мелкий ремонт",
                Type = IssueType.Ремонт,
                Responsible = "Иван Иванов"
            };

            var issueBreachingSla = new Issue
            {
                Start = new DateTime(2026, 6, 1, 11, 0, 0),
                End = new DateTime(2026, 6, 1, 12, 0, 0), // 60 minutes (breaches 30 mins SLA)
                Description = "Крупный ремонт",
                Type = IssueType.Ремонт,
                Responsible = "Иван Иванов"
            };

            equipment.Issues.Add(issueWithinSla);
            equipment.Issues.Add(issueBreachingSla);

            var masterList = new List<EquipmentInfo> { equipment };

            // Act
            var result = service.Analyze(masterList, "Все месяцы", "Все группы", 30.0);

            // Assert
            Assert.Equal(2, result.TotalIssues);
            Assert.Equal(0, result.UnassignedIssues);
            Assert.Equal(1, result.SlaBreaches);
            Assert.Equal(50.0, result.SlaCompliancePercent);
            Assert.Single(result.Rows);
            
            var row = result.Rows[0];
            Assert.Equal("Иван Иванов", row.Name);
            Assert.Equal(2, row.IssuesCount);
            Assert.Equal(1, row.SlaMetCount);
            Assert.Equal(50.0, row.SlaCompliancePercent);
            Assert.Equal(1.0, row.EquipmentCount); // 1 unique equipment
        }

        [Theory]
        [InlineData(79.9, true, false, false)]
        [InlineData(80.0, false, true, false)]
        [InlineData(94.9, false, true, false)]
        [InlineData(95.0, false, false, true)]
        public void EmployeeAnalysisRow_SlaStatusFlags_ShouldBeCorrect(
            double compliance, bool expectedDanger, bool expectedWarning, bool expectedSuccess)
        {
            // Arrange
            var row = new EmployeeAnalysisRow { SlaCompliancePercent = compliance };

            // Act & Assert
            Assert.Equal(expectedDanger, row.IsSlaDanger);
            Assert.Equal(expectedWarning, row.IsSlaWarning);
            Assert.Equal(expectedSuccess, row.IsSlaSuccess);
        }
    }
}
