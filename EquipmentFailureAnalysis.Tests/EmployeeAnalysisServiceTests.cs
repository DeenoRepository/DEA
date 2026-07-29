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

        [Fact]
        public void Analyze_ShouldClampDurationToSelectedMonth()
        {
            // Arrange
            var service = new EmployeeAnalysisService();
            var equipment = new EquipmentInfo
            {
                Uid = 102,
                Title = "Станок 2",
                InventoryNumber = "102",
                Subdivision = "Сектор сборки"
            };

            // Issue starting before month, ending in month
            // 2026-05-28 12:00:00 to 2026-06-02 12:00:00
            // Clamped duration: 2026-06-01 00:00:00 to 2026-06-02 12:00:00 = 36 hours (2160 mins)
            var issue1 = new Issue
            {
                Start = new DateTime(2026, 5, 28, 12, 0, 0),
                End = new DateTime(2026, 6, 2, 12, 0, 0),
                Description = "Ремонт 1",
                Type = IssueType.Ремонт,
                Responsible = "Петр Петров"
            };

            // Issue starting in month, ending after month
            // 2026-06-28 12:00:00 to 2026-07-05 12:00:00
            // Clamped duration: 2026-06-28 12:00:00 to 2026-07-01 00:00:00 = 60 hours (3600 mins)
            var issue2 = new Issue
            {
                Start = new DateTime(2026, 6, 28, 12, 0, 0),
                End = new DateTime(2026, 7, 5, 12, 0, 0),
                Description = "Ремонт 2",
                Type = IssueType.Ремонт,
                Responsible = "Петр Петров"
            };

            equipment.Issues.Add(issue1);
            equipment.Issues.Add(issue2);

            var masterList = new List<EquipmentInfo> { equipment };

            // Act
            var result = service.Analyze(masterList, "Июнь 2026", "Все группы", 30.0);

            // Assert
            Assert.Single(result.Rows);
            var row = result.Rows[0];
            Assert.Equal("Петр Петров", row.Name);
            Assert.Equal(2, row.IssuesCount);
            // 2160 + 3600 = 5760 minutes = 96 hours => "96:00"
            Assert.Equal("96:00", row.TotalDurationText);
            Assert.Equal(TimeSpan.FromHours(96), row.TotalDuration);
        }

        [Fact]
        public void Analyze_ShouldHandleInProgressIssuesWithDynamicDuration()
        {
            // Arrange
            var service = new EmployeeAnalysisService();
            var equipment = new EquipmentInfo
            {
                Uid = 103,
                Title = "Станок 3",
                InventoryNumber = "103",
                Subdivision = "Сектор сборки"
            };

            // In progress issue starting 2 hours ago
            var issue = new Issue
            {
                Start = DateTime.Now.AddHours(-2),
                Description = "Ремонт в процессе",
                Type = IssueType.Ремонт,
                Responsible = "Сергей Сергеев",
                IsInProgress = true
            };

            equipment.Issues.Add(issue);
            var masterList = new List<EquipmentInfo> { equipment };

            // Act
            var result = service.Analyze(masterList, "Все месяцы", "Все группы", 30.0);

            // Assert
            Assert.Single(result.Rows);
            var row = result.Rows[0];
            Assert.Equal("Сергей Сергеев", row.Name);
            // TotalDuration should be around 2 hours (120 minutes)
            Assert.True(row.TotalDuration.TotalMinutes >= 119.9, $"Expected total duration to be >= 120 mins, but was {row.TotalDuration.TotalMinutes}");
            Assert.True(row.TotalDuration.TotalMinutes < 130, $"Expected total duration to be reasonably close to 120 mins, but was {row.TotalDuration.TotalMinutes}");
        }
    }
}
