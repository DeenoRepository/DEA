using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Services;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.Json;

namespace EquipmentFailureAnalysis.ViewModels
{
    public class PprViewModel : ViewModelBase
    {
        private readonly MainWindowViewModel _shell;
        private readonly PprExcelService _excelService = new PprExcelService();

        private List<PprScheduleItem> _allPprItems = new List<PprScheduleItem>();
        private ObservableCollection<PprScheduleItem> _filteredPprItems = new ObservableCollection<PprScheduleItem>();
        private ObservableCollection<string> _subdivisionFilters = new ObservableCollection<string>();
        
        private string _selectedSubdivisionFilter = "Все группы";
        private string _searchQuery = string.Empty;
        private int _selectedYear = DateTime.Today.Year;
        private string _loadedFilePath = string.Empty;

        // KPI Properties
        private int _totalEquipmentCount;
        private int _totalCompletedCount;
        private int _totalOverdueCount;
        private double _completionRate;

        public PprViewModel(MainWindowViewModel shell)
        {
            _shell = shell;

            _subdivisionFilters.Add("Все группы");

            ImportFromExcelCommand = ReactiveCommand.Create<string>(ImportFromExcel);
            ExportToExcelCommand = ReactiveCommand.Create<string>(ExportToExcel);
            ToggleCompletionCommand = ReactiveCommand.Create<ToggleCompletionArgs>(ToggleCompletion);
            ResetCompletionsCommand = ReactiveCommand.Create(ResetCompletions);

            // Auto-load default file if exists
            AutoLoadDefaultFile();
        }

        public ObservableCollection<PprScheduleItem> FilteredPprItems
        {
            get => _filteredPprItems;
            set => this.RaiseAndSetIfChanged(ref _filteredPprItems, value);
        }

        public ObservableCollection<string> SubdivisionFilters => _subdivisionFilters;

        public string SelectedSubdivisionFilter
        {
            get => _selectedSubdivisionFilter;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedSubdivisionFilter, value);
                ApplyFilterAndCalculateKPIs();
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                this.RaiseAndSetIfChanged(ref _searchQuery, value);
                ApplyFilterAndCalculateKPIs();
            }
        }

        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedYear, value);
                ApplyFilterAndCalculateKPIs();
            }
        }

        public string LoadedFilePath
        {
            get => _loadedFilePath;
            private set => this.RaiseAndSetIfChanged(ref _loadedFilePath, value);
        }

        // KPI Bindings
        public int TotalEquipmentCount
        {
            get => _totalEquipmentCount;
            set => this.RaiseAndSetIfChanged(ref _totalEquipmentCount, value);
        }

        public int TotalCompletedCount
        {
            get => _totalCompletedCount;
            set => this.RaiseAndSetIfChanged(ref _totalCompletedCount, value);
        }

        public int TotalOverdueCount
        {
            get => _totalOverdueCount;
            set => this.RaiseAndSetIfChanged(ref _totalOverdueCount, value);
        }

        public double CompletionRate
        {
            get => _completionRate;
            set => this.RaiseAndSetIfChanged(ref _completionRate, value);
        }

        public ReactiveCommand<string, Unit> ImportFromExcelCommand { get; }
        public ReactiveCommand<string, Unit> ExportToExcelCommand { get; }
        public ReactiveCommand<ToggleCompletionArgs, Unit> ToggleCompletionCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetCompletionsCommand { get; }

        public class ToggleCompletionArgs
        {
            public required PprScheduleItem Item { get; init; }
            public required int MonthIndex { get; init; }
        }

        private void AutoLoadDefaultFile()
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ПРР.xlsx"),
                Path.Combine(Directory.GetCurrentDirectory(), "Data", "ПРР.xlsx"),
                Path.Combine(Directory.GetCurrentDirectory(), "EquipmentFailureAnalysis", "Data", "ПРР.xlsx")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    ImportFromExcel(path);
                    break;
                }
            }
        }

        private void ImportFromExcel(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                _shell.IsLoading = true;
                var items = _excelService.Import(filePath);
                if (items == null || items.Count == 0)
                {
                    _shell.AddStatusEvent($"Файл {Path.GetFileName(filePath)} пуст или некорректен.");
                    return;
                }

                _allPprItems = items;
                LoadedFilePath = filePath;

                // Load cached completions
                RestoreCompletions();

                // Rebuild subdivision filter options
                var currentSelection = SelectedSubdivisionFilter;
                _subdivisionFilters.Clear();
                _subdivisionFilters.Add("Все группы");
                foreach (var sub in _allPprItems.Select(i => i.Subdivision).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s))
                {
                    _subdivisionFilters.Add(sub);
                }

                if (_subdivisionFilters.Contains(currentSelection))
                    SelectedSubdivisionFilter = currentSelection;
                else
                    SelectedSubdivisionFilter = "Все группы";

                ApplyFilterAndCalculateKPIs();
                _shell.AddStatusEvent($"Импортировано {items.Count} позиций графиков ППР из {Path.GetFileName(filePath)}.");
            }
            catch (Exception ex)
            {
                _shell.AddStatusEvent($"Ошибка импорта Excel: {ex.Message}");
            }
            finally
            {
                _shell.IsLoading = false;
            }
        }

        private void ExportToExcel(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || _allPprItems.Count == 0)
                return;

            try
            {
                _shell.IsLoading = true;
                bool success = _excelService.Export(filePath, _allPprItems);
                if (success)
                {
                    _shell.AddStatusEvent($"График ППР успешно экспортирован в {Path.GetFileName(filePath)}.");
                }
                else
                {
                    _shell.AddStatusEvent("Не удалось записать файл Excel.");
                }
            }
            catch (Exception ex)
            {
                _shell.AddStatusEvent($"Ошибка экспорта Excel: {ex.Message}");
            }
            finally
            {
                _shell.IsLoading = false;
            }
        }

        private void ToggleCompletion(ToggleCompletionArgs args)
        {
            if (args == null || args.Item == null || args.MonthIndex < 0 || args.MonthIndex >= 12)
                return;

            var plan = args.Item.MonthlyPlans[args.MonthIndex];
            if (string.IsNullOrEmpty(plan))
                return; // Can only toggle if a maintenance plan exists

            args.Item.MonthlyCompletions[args.MonthIndex] = !args.Item.MonthlyCompletions[args.MonthIndex];
            
            // Save state
            SaveCompletions();
            
            // Recalculate KPIs
            ApplyFilterAndCalculateKPIs();
        }

        private void ResetCompletions()
        {
            if (_allPprItems == null || _allPprItems.Count == 0)
                return;

            foreach (var item in _allPprItems)
            {
                for (int i = 0; i < 12; i++)
                {
                    item.MonthlyCompletions[i] = false;
                }
            }

            SaveCompletions();
            ApplyFilterAndCalculateKPIs();
            _shell.AddStatusEvent("Все отметки выполнения сброшены.");
        }

        private void ApplyFilterAndCalculateKPIs()
        {
            var query = (SearchQuery ?? string.Empty).Trim().ToLowerInvariant();
            var subdivision = SelectedSubdivisionFilter;

            var filtered = _allPprItems.AsEnumerable();

            if (subdivision != "Все группы")
            {
                filtered = filtered.Where(i => string.Equals(i.Subdivision, subdivision, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(i => 
                    i.EquipmentTitle.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                    (i.InventoryNumber != null && i.InventoryNumber.Contains(query, StringComparison.OrdinalIgnoreCase)));
            }

            var list = filtered.ToList();
            FilteredPprItems = new ObservableCollection<PprScheduleItem>(list);

            // Calculate KPIs
            TotalEquipmentCount = list.Count;

            int completed = 0;
            int totalPlanned = 0;
            int overdue = 0;

            int currentYear = DateTime.Today.Year;
            int currentMonthIndex = DateTime.Today.Month - 1; // 0-based month index

            foreach (var item in list)
            {
                for (int m = 0; m < 12; m++)
                {
                    if (!string.IsNullOrEmpty(item.MonthlyPlans[m]))
                    {
                        totalPlanned++;
                        if (item.MonthlyCompletions[m])
                        {
                            completed++;
                        }
                        else
                        {
                            // It is overdue if it is in the past
                            // Or if we are viewing the current year, and the month is in the past
                            if (SelectedYear < currentYear)
                            {
                                overdue++;
                            }
                            else if (SelectedYear == currentYear && m < currentMonthIndex)
                            {
                                overdue++;
                            }
                        }
                    }
                }
            }

            TotalCompletedCount = completed;
            TotalOverdueCount = overdue;
            CompletionRate = totalPlanned == 0 ? 100.0 : Math.Round(completed * 100.0 / totalPlanned, 1);
        }

        // Cache file logic
        private string GetCacheFilePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EquipmentFailureAnalysis");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return Path.Combine(dir, "ppr_completion_cache.json");
        }

        private void SaveCompletions()
        {
            try
            {
                var cacheMap = new Dictionary<string, bool[]>();
                foreach (var item in _allPprItems)
                {
                    var key = $"{item.EquipmentTitle}|{item.InventoryNumber}|{item.Subdivision}";
                    cacheMap[key] = item.MonthlyCompletions;
                }

                var json = JsonSerializer.Serialize(cacheMap);
                File.WriteAllText(GetCacheFilePath(), json);
            }
            catch
            {
                // Ignore caching errors
            }
        }

        private void RestoreCompletions()
        {
            var cacheFile = GetCacheFilePath();
            if (!File.Exists(cacheFile))
                return;

            try
            {
                var json = File.ReadAllText(cacheFile);
                var cacheMap = JsonSerializer.Deserialize<Dictionary<string, bool[]>>(json);
                if (cacheMap == null)
                    return;

                foreach (var item in _allPprItems)
                {
                    var key = $"{item.EquipmentTitle}|{item.InventoryNumber}|{item.Subdivision}";
                    if (cacheMap.TryGetValue(key, out var completions))
                    {
                        for (int i = 0; i < 12 && i < completions.Length; i++)
                        {
                            item.MonthlyCompletions[i] = completions[i];
                        }
                    }
                }
            }
            catch
            {
                // Ignore restoration errors
            }
        }
    }
}
