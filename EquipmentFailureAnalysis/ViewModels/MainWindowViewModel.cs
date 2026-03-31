using EquipmentFailureAnalysis.Models;
using EquipmentFailureAnalysis.Utility;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace EquipmentFailureAnalysis.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<EquipmentInfo> EquipmentCollection { get; set; }
        public ObservableCollection<DailyDowntimeIndex> DailyDowntimeIndexCollection { get; set; }

        public MainWindowViewModel()
        {
            XmlDataDecoder xmlDataDecoder = new XmlDataDecoder();
            EquipmentCollection = new ObservableCollection<EquipmentInfo>(xmlDataDecoder.DecodeEquipment().Where(x => x.Uid == 331908));

            DailyDowntimeIndexCollection = new ObservableCollection<DailyDowntimeIndex>();

            for (int i = 0; i < 365; i++)
            {
                DailyDowntimeIndexCollection.Add(new DailyDowntimeIndex
                {
                    Day = DateTime.Now.AddDays(-i),
                    Index = 0
                });
            }

            foreach (var equipment in EquipmentCollection)
            {
                DailyDowntimeIndexCollection[equipment.Issue.Created.DayOfYear].Index++;
            }
        }
    }
}
