using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Services;
using EquipmentFailureAnalysis.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace EquipmentFailureAnalysis.Tests
{
    public class PprTests
    {
        private readonly string? _excelPath;

        public PprTests()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, "EquipmentFailureAnalysis", "Data", "ПРР.xlsx");
                if (File.Exists(candidate))
                {
                    _excelPath = candidate;
                    break;
                }
                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }
        }

        [Fact]
        public void Import_ShouldCorrectlyParseExcelFile()
        {
            // Arrange
            Assert.NotNull(_excelPath);
            var service = new PprExcelService();

            // Act
            var items = service.Import(_excelPath!);

            // Assert
            Assert.NotNull(items);
            Assert.NotEmpty(items);

            // Validate that we got a known row
            var firstItem = items.First();
            Assert.False(string.IsNullOrWhiteSpace(firstItem.EquipmentTitle));
            Assert.False(string.IsNullOrWhiteSpace(firstItem.Subdivision));
            Assert.NotNull(firstItem.MonthlyPlans);
            Assert.Equal(12, firstItem.MonthlyPlans.Length);
            Assert.Equal(12, firstItem.MonthlyCompletions.Length);
        }

        [Fact]
        public void PprViewModel_ShouldFilterItemsAndCalculateKPIs()
        {
            // Arrange
            var shell = new MainWindowViewModel();
            var vm = new PprViewModel(shell);

            var items = new List<PprScheduleItem>
            {
                new PprScheduleItem
                {
                    Subdivision = "10 цех",
                    EquipmentTitle = "Станок А",
                    InventoryNumber = "111",
                    CommissionYear = "2020",
                    MonthlyPlans = new string?[] { "ТО", null, "ТР", null, "ТО", null, "ТО", null, "ТО", null, "ТО", null },
                    MonthlyCompletions = new bool[] { true, false, false, false, false, false, false, false, false, false, false, false }
                },
                new PprScheduleItem
                {
                    Subdivision = "20 цех",
                    EquipmentTitle = "Пресс Б",
                    InventoryNumber = "222",
                    CommissionYear = "2018",
                    MonthlyPlans = new string?[] { null, "ТО", null, "ТО", null, "ТО", null, "ТО", null, "ТО", null, "ТР" },
                    MonthlyCompletions = new bool[] { false, true, false, true, false, false, false, false, false, false, false, false }
                }
            };

            // Set private field or trigger through a custom data load
            // Since we want to test VM logic directly, let's invoke vm properties
            // We can call private method or let's import custom file. 
            // Better yet, we can write a test method or test it using reflection to populate _allPprItems.
            var allPprItemsField = typeof(PprViewModel).GetField("_allPprItems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(allPprItemsField);
            allPprItemsField.SetValue(vm, items);

            // Rebuild subdivision options
            var subdivisionFilters = vm.SubdivisionFilters;
            subdivisionFilters.Clear();
            subdivisionFilters.Add("Все группы");
            subdivisionFilters.Add("10 цех");
            subdivisionFilters.Add("20 цех");

            // Act: Reset filter (triggers ApplyFilterAndCalculateKPIs)
            vm.SelectedSubdivisionFilter = "Все группы";

            // Assert KPIs
            Assert.Equal(2, vm.TotalEquipmentCount);
            // Completed: Month 1 (index 0) for Item 1 is Completed (true). Month 2 (index 1) and Month 4 (index 3) for Item 2 are Completed (true).
            // Total completed: 1 + 2 = 3
            Assert.Equal(3, vm.TotalCompletedCount);

            // Act: Test subdivision filtering
            vm.SelectedSubdivisionFilter = "10 цех";
            Assert.Equal(1, vm.TotalEquipmentCount);
            Assert.Equal("Станок А", vm.FilteredPprItems.First().EquipmentTitle);

            // Act: Test search query
            vm.SelectedSubdivisionFilter = "Все группы";
            vm.SearchQuery = "Пресс";
            Assert.Equal(1, vm.TotalEquipmentCount);
            Assert.Equal("Пресс Б", vm.FilteredPprItems.First().EquipmentTitle);

            // Act: Test Toggle Completion
            vm.SearchQuery = string.Empty;
            var itemToToggle = vm.FilteredPprItems.First(i => i.EquipmentTitle == "Станок А");
            // Month 2 (index 2) is "ТР" and not completed. Toggle it.
            vm.ToggleCompletionCommand.Execute(new PprViewModel.ToggleCompletionArgs
            {
                Item = itemToToggle,
                MonthIndex = 2
            }).Subscribe();

            // Assert completed count increased
            Assert.True(itemToToggle.MonthlyCompletions[2]);
            Assert.Equal(4, vm.TotalCompletedCount);
        }
    }
}
