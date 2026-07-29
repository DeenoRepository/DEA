using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Services;
using System;
using System.Collections.ObjectModel;
using Xunit;

namespace EquipmentFailureAnalysis.Tests
{
    public class EquipmentDowntimeLossServiceTests
    {
        [Fact]
        public void BuildReport_MonthlyGranularity_CorrectlyBucketsTimeLosses()
        {
            var service = new EquipmentDowntimeLossService();
            var eq = new EquipmentInfo
            {
                Title = "Тестовый станок №1",
                InventoryNumber = "INV-001",
                Subdivision = "Сектор сборки",
                Issues = new ObservableCollection<Issue>
                {
                    new Issue
                    {
                        Description = "Ремонт оборудования",
                        Type = IssueType.Ремонт,
                        Start = new DateTime(2026, 1, 10, 10, 0, 0),
                        End = new DateTime(2026, 1, 10, 12, 0, 0) // 120 mins Repair
                    },
                    new Issue
                    {
                        Description = "Настройка оборудования",
                        Type = IssueType.Настройка,
                        Start = new DateTime(2026, 2, 5, 9, 0, 0),
                        End = new DateTime(2026, 2, 5, 10, 0, 0) // 60 mins Setup
                    }
                }
            };

            var start = new DateTime(2026, 1, 1);
            var end = new DateTime(2026, 2, 28);

            var report = service.BuildReport(new[] { eq }, start, end, PeriodGranularity.Monthly);

            Assert.NotNull(report);
            Assert.Equal(2, report.PeriodHeaders.Count); // Jan and Feb
            Assert.Single(report.Rows);

            var row = report.Rows[0];
            Assert.Equal("Тестовый станок №1", row.EquipmentTitle);
            Assert.Equal(180, row.TotalDowntimeMinutes);
            Assert.Equal(120, row.TotalRepairMinutes);
            Assert.Equal(60, row.TotalSetupMinutes);

            var janBucket = row.PeriodBuckets["2026-01"];
            Assert.Equal(120, janBucket.RepairMinutes);
            Assert.Equal(0, janBucket.SetupMinutes);

            var febBucket = row.PeriodBuckets["2026-02"];
            Assert.Equal(0, febBucket.RepairMinutes);
            Assert.Equal(60, febBucket.SetupMinutes);
        }

        [Fact]
        public void BuildReport_QuarterlyGranularity_AggregatesQuarterlyBuckets()
        {
            var service = new EquipmentDowntimeLossService();
            var eq = new EquipmentInfo
            {
                Title = "Установка 2",
                InventoryNumber = "INV-002",
                Subdivision = "Сектор измерений",
                Issues = new ObservableCollection<Issue>
                {
                    new Issue
                    {
                        Description = "Ремонт оборудования",
                        Type = IssueType.Ремонт,
                        Start = new DateTime(2026, 1, 15, 10, 0, 0),
                        End = new DateTime(2026, 1, 15, 14, 0, 0) // 240 mins Repair in Q1
                    },
                    new Issue
                    {
                        Description = "Настройка оборудования",
                        Type = IssueType.Настройка,
                        Start = new DateTime(2026, 4, 10, 8, 0, 0),
                        End = new DateTime(2026, 4, 10, 10, 0, 0) // 120 mins Setup in Q2
                    }
                }
            };

            var start = new DateTime(2026, 1, 1);
            var end = new DateTime(2026, 6, 30);

            var report = service.BuildReport(new[] { eq }, start, end, PeriodGranularity.Quarterly);

            Assert.NotNull(report);
            Assert.Equal(2, report.PeriodHeaders.Count); // Q1 and Q2
            Assert.Single(report.Rows);

            var row = report.Rows[0];
            Assert.Equal(360, row.TotalDowntimeMinutes);

            var q1Bucket = row.PeriodBuckets["2026-Q1"];
            Assert.Equal(240, q1Bucket.RepairMinutes);

            var q2Bucket = row.PeriodBuckets["2026-Q2"];
            Assert.Equal(120, q2Bucket.SetupMinutes);
        }

        [Fact]
        public void BuildReport_MultiYearPeriod_GeneratesAllSubPeriodBucketsAcrossYears()
        {
            var service = new EquipmentDowntimeLossService();
            var eq = new EquipmentInfo
            {
                Title = "Пресс многолетний",
                InventoryNumber = "INV-2024",
                Subdivision = "Штамповка",
                Issues = new ObservableCollection<Issue>
                {
                    new Issue
                    {
                        Description = "Ремонт 2024",
                        Type = IssueType.Ремонт,
                        Start = new DateTime(2024, 5, 10, 10, 0, 0),
                        End = new DateTime(2024, 5, 10, 15, 0, 0) // 300 mins
                    },
                    new Issue
                    {
                        Description = "Настройка 2026",
                        Type = IssueType.Настройка,
                        Start = new DateTime(2026, 11, 1, 8, 0, 0),
                        End = new DateTime(2026, 11, 1, 12, 0, 0) // 240 mins
                    }
                }
            };

            var start = new DateTime(2024, 1, 1);
            var end = new DateTime(2026, 12, 31);

            // Quarterly over 3 years (2024, 2025, 2026) -> 12 quarters
            var reportQuarterly = service.BuildReport(new[] { eq }, start, end, PeriodGranularity.Quarterly);
            Assert.Equal(12, reportQuarterly.PeriodHeaders.Count);
            Assert.Equal(540, reportQuarterly.GrandTotalMinutes);

            var q2024Q2 = reportQuarterly.Rows[0].PeriodBuckets["2024-Q2"];
            Assert.Equal(300, q2024Q2.RepairMinutes);

            var q2026Q4 = reportQuarterly.Rows[0].PeriodBuckets["2026-Q4"];
            Assert.Equal(240, q2026Q4.SetupMinutes);

            // Monthly over 3 years -> 36 months
            var reportMonthly = service.BuildReport(new[] { eq }, start, end, PeriodGranularity.Monthly);
            Assert.Equal(36, reportMonthly.PeriodHeaders.Count);
        }
    }
}
