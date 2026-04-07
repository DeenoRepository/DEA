using System.Collections.Generic;
using ReactiveUI;

namespace EquipmentFailureAnalysis.Models
{
    public class EquipmentInfo : ReactiveObject
    {
        public int Uid { get; set; }
        public string Title { get; set; } = string.Empty;
        // Inventory number (инвентарный номер)
        public string? InventoryNumber { get; set; }

        // List of issues (неисправностей) related to this equipment
        public System.Collections.ObjectModel.ObservableCollection<Issue> Issues { get; set; }

        // convenience property (notify when Issues changes)
        public bool HasIssues => Issues != null && Issues.Count > 0;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => this.RaiseAndSetIfChanged(ref _isSelected, value);
        }

        // total number of issues
        public int IssueCount => Issues?.Count ?? 0;

        public EquipmentInfo()
        {
            Issues = new System.Collections.ObjectModel.ObservableCollection<Issue>();
            Issues.CollectionChanged += (s, e) =>
            {
                this.RaisePropertyChanged(nameof(IssueCount));
                this.RaisePropertyChanged(nameof(HasIssues));
            };
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(InventoryNumber)
                ? Title
                : $"{Title} ({InventoryNumber})";
        }
    }
}
