using Avalonia;
using Avalonia.Controls;
using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace EquipmentFailureAnalysis.Views
{
    public partial class HeatmapGridControl : UserControl
    {
        public static readonly StyledProperty<IEnumerable<int>?> DayHeadersProperty =
            AvaloniaProperty.Register<HeatmapGridControl, IEnumerable<int>?>(nameof(DayHeaders));

        public static readonly StyledProperty<IEnumerable<MonthRow>?> MonthRowsProperty =
            AvaloniaProperty.Register<HeatmapGridControl, IEnumerable<MonthRow>?>(nameof(MonthRows));

        public static readonly StyledProperty<ICommand?> CellCommandProperty =
            AvaloniaProperty.Register<HeatmapGridControl, ICommand?>(nameof(CellCommand));

        public static readonly StyledProperty<string> ConverterModeProperty =
            AvaloniaProperty.Register<HeatmapGridControl, string>(nameof(ConverterMode), "FailureAnalysis");

        public static readonly StyledProperty<DateTime> SelectedDateProperty =
            AvaloniaProperty.Register<HeatmapGridControl, DateTime>(nameof(SelectedDate), DateTime.Today);

        public IEnumerable<int>? DayHeaders
        {
            get => GetValue(DayHeadersProperty);
            set => SetValue(DayHeadersProperty, value);
        }

        public IEnumerable<MonthRow>? MonthRows
        {
            get => GetValue(MonthRowsProperty);
            set => SetValue(MonthRowsProperty, value);
        }

        public ICommand? CellCommand
        {
            get => GetValue(CellCommandProperty);
            set => SetValue(CellCommandProperty, value);
        }

        public string ConverterMode
        {
            get => GetValue(ConverterModeProperty);
            set => SetValue(ConverterModeProperty, value);
        }

        public System.DateTime SelectedDate
        {
            get => GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        public HeatmapGridControl()
        {
            InitializeComponent();
        }
    }
}
