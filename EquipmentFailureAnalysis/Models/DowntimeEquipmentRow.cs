using System.Collections.ObjectModel;

namespace EquipmentFailureAnalysis.Models
{
    public class DowntimeEquipmentRow
    {
        public string Title { get; set; } = string.Empty;
        public string InventoryNumber { get; set; } = string.Empty;
        public int IssuesCount { get; set; }

        public ObservableCollection<TimelinePoint> TimelinePoints { get; set; } = new ObservableCollection<TimelinePoint>();
        public ObservableCollection<TimelinePoint> RepairsTimelinePoints { get; set; } = new ObservableCollection<TimelinePoint>();
        public ObservableCollection<TimelinePoint> SetupsTimelinePoints { get; set; } = new ObservableCollection<TimelinePoint>();
        public ObservableCollection<Annotation> Annotations { get; set; } = new ObservableCollection<Annotation>();
    }
}
