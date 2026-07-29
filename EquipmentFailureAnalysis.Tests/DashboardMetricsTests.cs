using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.ViewModels;
using Xunit;

namespace EquipmentFailureAnalysis.Tests
{
    public class DashboardMetricsTests
    {
        [Fact]
        public void BuildDashboard_ShouldFilterEquipmentAndCalculateMetricsBySubdivision()
        {
            // Arrange
            var vm = new MainWindowViewModel();

            var eqA = new EquipmentInfo
            {
                Uid = 1,
                Title = "Станок А",
                InventoryNumber = "001",
                Subdivision = "Группа А"
            };
            // Ремонт на 10 часов (600 минут) полностью внутри 30-дневного окна
            eqA.Issues.Add(new Issue
            {
                Start = DateTime.Now.Date.AddDays(-5),
                End = DateTime.Now.Date.AddDays(-5).AddHours(10),
                Type = IssueType.Ремонт,
                Description = "Ремонт А",
                Responsible = "Сотрудник"
            });

            var eqB = new EquipmentInfo
            {
                Uid = 2,
                Title = "Станок Б",
                InventoryNumber = "002",
                Subdivision = "Группа Б"
            };
            // Ремонт на 20 часов (1200 минут) полностью внутри 30-дневного окна
            eqB.Issues.Add(new Issue
            {
                Start = DateTime.Now.Date.AddDays(-10),
                End = DateTime.Now.Date.AddDays(-10).AddHours(20),
                Type = IssueType.Ремонт,
                Description = "Ремонт Б",
                Responsible = "Сотрудник"
            });

            var list = new ObservableCollection<EquipmentInfo> { eqA, eqB };
            vm.ImportEquipment(list);

            // Act - Фильтруем по "Группа А"
            vm.Dashboard.SelectedDashboardSubdivisionFilter = "Группа А";

            // Assert
            // Для "Группа А":
            // totalEquipmentCount = 1 (только eqA)
            // totalAvailableMinutes = 1 * 31 * 8 * 60 = 14880 минут
            // totalDowntimeMinutes = 600 минут (10 часов)
            // totalFailures = 1
            // MTBF = (14880 - 600) / 1 = 14280 минут = 238 часов = "238:00"
            // MTTR = 600 минут = 10 часов = "10:00"
            Assert.Equal("238:00", vm.Dashboard.DashboardCurrentPeriodMtbf);
            Assert.Equal("10:00", vm.Dashboard.DashboardCurrentPeriodMttr);
            Assert.Equal("96.0%", vm.Dashboard.DashboardAvailabilityPercentText);

            // Act - Фильтруем по "Группа Б"
            vm.Dashboard.SelectedDashboardSubdivisionFilter = "Группа Б";

            // Assert
            // Для "Группа Б":
            // totalEquipmentCount = 1 (только eqB)
            // totalAvailableMinutes = 14880 минут
            // totalDowntimeMinutes = 1200 минут (20 часов)
            // totalFailures = 1
            // MTBF = (14880 - 1200) / 1 = 13680 минут = 228 часов = "228:00"
            // MTTR = 1200 минут = 20 часов = "20:00"
            Assert.Equal("228:00", vm.Dashboard.DashboardCurrentPeriodMtbf);
            Assert.Equal("20:00", vm.Dashboard.DashboardCurrentPeriodMttr);
            Assert.Equal("91.9%", vm.Dashboard.DashboardAvailabilityPercentText);
        }

        [Fact]
        public void BuildDashboard_ShouldClampDowntimeToPeriodBoundaries()
        {
            // Arrange
            var vm = new MainWindowViewModel();

            var eq = new EquipmentInfo
            {
                Uid = 1,
                Title = "Станок А",
                InventoryNumber = "001",
                Subdivision = "Группа А"
            };

            // Ремонт начинается за 40 дней до сегодня (на 10 дней раньше начала 30-дневного окна)
            // и заканчивается за 20 дней до сегодня (через 10 дней после начала 30-дневного окна).
            // Полная длительность = 20 дней.
            // Длительность внутри 30-дневного окна = 10 дней (от -30 до -20 дней).
            var issue = new Issue
            {
                Start = DateTime.Now.Date.AddDays(-40),
                End = DateTime.Now.Date.AddDays(-20),
                Type = IssueType.Ремонт,
                Description = "Длинный ремонт",
                Responsible = "Сотрудник"
            };
            eq.Issues.Add(issue);

            var list = new ObservableCollection<EquipmentInfo> { eq };
            vm.ImportEquipment(list);

            // Act
            vm.Dashboard.SelectedDashboardSubdivisionFilter = "Группа А";

            // Assert
            // totalEquipmentCount = 1
            // totalAvailableMinutes = 14880 минут (31 день)
            // downtime within period = 10 дней = 14400 минут (вместо всей длительности 20 дней)
            // MTBF = (14880 - 14400) / 1 = 480 минут = 8 часов = "08:00"
            // MTTR использует полную длительность инцидента = 20 дней = 480 часов = "480:00"
            Assert.Equal("08:00", vm.Dashboard.DashboardCurrentPeriodMtbf);
            Assert.Equal("480:00", vm.Dashboard.DashboardCurrentPeriodMttr);
            Assert.Equal("3.2%", vm.Dashboard.DashboardAvailabilityPercentText);
        }

        [Fact]
        public void BuildDashboard_ShouldCalculateDurationCorrectlyForInProgressIssues()
        {
            // Arrange
            var vm = new MainWindowViewModel();

            var eq = new EquipmentInfo
            {
                Uid = 1,
                Title = "Станок А",
                InventoryNumber = "001",
                Subdivision = "Группа А"
            };

            // Ремонт со статусом В процессе, начался 5 дней назад.
            // Его эффективное окончание должно считаться как `now`.
            var issue = new Issue
            {
                Start = DateTime.Now.Date.AddDays(-5),
                End = DateTime.Now.Date.AddDays(-10), // старая дата, должна игнорироваться
                IsInProgress = true,
                Type = IssueType.Ремонт,
                Description = "Активный ремонт",
                Responsible = "Сотрудник"
            };
            eq.Issues.Add(issue);

            var list = new ObservableCollection<EquipmentInfo> { eq };
            vm.ImportEquipment(list);

            // Act
            vm.Dashboard.SelectedDashboardSubdivisionFilter = "Группа А";

            // Assert
            var actualMtbfMin = ParseDurationToMinutes(vm.Dashboard.DashboardCurrentPeriodMtbf);
            var actualMttrMin = ParseDurationToMinutes(vm.Dashboard.DashboardCurrentPeriodMttr);

            var now = DateTime.Now;
            var expectedDowntime = (now - issue.Start).TotalMinutes;
            var expectedMtbf = 14880.0 - expectedDowntime;
            var expectedMttr = expectedDowntime;

            Assert.InRange(actualMtbfMin, expectedMtbf - 2.0, expectedMtbf + 2.0);
            Assert.InRange(actualMttrMin, expectedMttr - 2.0, expectedMttr + 2.0);

            var expectedAvailability = expectedMtbf * 100.0 / 14880.0;
            var actualAvailability = double.Parse(vm.Dashboard.DashboardAvailabilityPercentText.Replace("%", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(actualAvailability, expectedAvailability - 0.5, expectedAvailability + 0.5);
        }

        [Fact]
        public void BuildDashboard_ShouldFilterByUniversalFiltersAndPeriod()
        {
            // Arrange
            var vm = new MainWindowViewModel();

            var eqA = new EquipmentInfo
            {
                Uid = 10,
                Title = "Станок А1",
                InventoryNumber = "A-001",
                Subdivision = "Сборочный"
            };

            // Issue inside range: 2026-06-05 to 2026-06-06 (24 hours / 1440 mins)
            // Type = Ремонт, Status = Completed, Responsible = "Иванов"
            eqA.Issues.Add(new Issue
            {
                Start = new DateTime(2026, 6, 5, 12, 0, 0),
                End = new DateTime(2026, 6, 6, 12, 0, 0),
                Type = IssueType.Ремонт,
                Description = "Поломка А1",
                Responsible = "Иванов",
                IsInProgress = false
            });

            // Issue inside range but different type (Настройка): 2026-06-08 to 2026-06-09 (24 hours)
            // Type = Настройка, Status = Completed, Responsible = "Петров"
            eqA.Issues.Add(new Issue
            {
                Start = new DateTime(2026, 6, 8, 12, 0, 0),
                End = new DateTime(2026, 6, 9, 12, 0, 0),
                Type = IssueType.Настройка,
                Description = "Наладка А1",
                Responsible = "Петров",
                IsInProgress = false
            });

            // Issue outside range (July): 2026-07-02 to 2026-07-03
            eqA.Issues.Add(new Issue
            {
                Start = new DateTime(2026, 7, 2, 12, 0, 0),
                End = new DateTime(2026, 7, 3, 12, 0, 0),
                Type = IssueType.Ремонт,
                Description = "Поломка А2",
                Responsible = "Иванов",
                IsInProgress = false
            });

            var list = new ObservableCollection<EquipmentInfo> { eqA };
            vm.ImportEquipment(list);

            // Act 1: Set custom period to June 2026 (2026-06-01 to 2026-06-30)
            vm.Dashboard.DashboardStartDate = new DateTime(2026, 6, 1);
            vm.Dashboard.DashboardEndDate = new DateTime(2026, 6, 30);

            // Verify both issues in June are counted (total 2 issues: 1 repair, 1 setup)
            Assert.Equal(2, vm.Dashboard.DashboardCurrentPeriodIssues);

            // Act 2: Filter by Type = Ремонты
            vm.Dashboard.SelectedDashboardIssueTypeFilter = "Ремонты";
            Assert.Equal(1, vm.Dashboard.DashboardCurrentPeriodIssues); // Setup should be filtered out

            // Act 3: Filter by Responsible = Иванов
            vm.Dashboard.SelectedDashboardIssueTypeFilter = "Все типы";
            vm.Dashboard.SelectedDashboardResponsibleFilter = "Иванов";
            Assert.Equal(1, vm.Dashboard.DashboardCurrentPeriodIssues); // Petrov setup filtered out

            // Act 4: Search query = "А1" (matches eqA)
            vm.Dashboard.DashboardEquipmentSearchQuery = "А1";
            Assert.Equal(1, vm.Dashboard.DashboardCurrentPeriodIssues);

            // Act 5: Search query = "Б2" (does not match eqA)
            vm.Dashboard.DashboardEquipmentSearchQuery = "Б2";
            Assert.Equal(0, vm.Dashboard.DashboardCurrentPeriodIssues);
        }

        private static double ParseDurationToMinutes(string duration)
        {
            var parts = duration.Split(':');
            return double.Parse(parts[0]) * 60 + double.Parse(parts[1]);
        }
    }
}
