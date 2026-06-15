using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;

namespace EquipmentFailureAnalysis.Views
{
    public partial class DowntimeView : UserControl
    {
        public DowntimeView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void DowntimeHeatmapSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.DowntimeHeatmapSettingsButton_Click(sender, e);
        }

        private void DowntimeEquipmentButton_Click(object? sender, PointerPressedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.DowntimeEquipmentButton_Click(sender, e);
        }

        private void DowntimeSetToday_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.SetDowntimeAnalysisDate(DateTime.Today);
        }

        private void DowntimeSetYesterday_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.SetDowntimeAnalysisDate(DateTime.Today.AddDays(-1));
        }

        /// <summary>Reset-button click hook. The toast is shown by MainWindow via FiltersResetCounter.</summary>
        public void ResetFiltersWithToast_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // No-op: toast is triggered by MainWindow observing FiltersResetCounter.
        }

        private void HeatmapScrollLeft_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var scroller = this.FindControl<ScrollViewer>("DowntimeHeatmapScroller");
            if (scroller != null)
            {
                var offset = scroller.Offset;
                scroller.Offset = new Avalonia.Vector(Math.Max(0, offset.X - 256), offset.Y);
            }
        }

        private void HeatmapScrollRight_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var scroller = this.FindControl<ScrollViewer>("DowntimeHeatmapScroller");
            if (scroller != null)
            {
                var offset = scroller.Offset;
                scroller.Offset = new Avalonia.Vector(offset.X + 256, offset.Y);
            }
        }

        private bool _needsScrollToCurrentMonth = true;
        private System.Collections.Specialized.INotifyCollectionChanged? _subscribedMonthRows;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            
            if (_subscribedMonthRows != null)
            {
                _subscribedMonthRows.CollectionChanged -= OnMonthRowsChanged;
                _subscribedMonthRows = null;
            }

            if (DataContext is ViewModels.DowntimeViewModel vm)
            {
                _subscribedMonthRows = vm.DowntimeMonthRows;
                if (_subscribedMonthRows != null)
                {
                    _subscribedMonthRows.CollectionChanged += OnMonthRowsChanged;
                }
            }

            _needsScrollToCurrentMonth = true;
            TriggerScrollIfReady();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BoundsProperty)
            {
                TriggerScrollIfReady();
            }
        }

        private void OnMonthRowsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _needsScrollToCurrentMonth = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => TriggerScrollIfReady(), Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void TriggerScrollIfReady()
        {
            var scroller = this.FindControl<ScrollViewer>("DowntimeHeatmapScroller");
            if (scroller == null || scroller.Bounds.Width <= 0)
                return;

            var leftBtn = this.FindControl<Button>("DowntimeHeatmapScrollLeftButton");
            var rightBtn = this.FindControl<Button>("DowntimeHeatmapScrollRightButton");

            if (DataContext is ViewModels.DowntimeViewModel vm)
            {
                var monthRows = vm.DowntimeMonthRows;
                if (monthRows == null || monthRows.Count == 0)
                {
                    if (leftBtn != null) leftBtn.IsVisible = false;
                    if (rightBtn != null) rightBtn.IsVisible = false;
                    return;
                }

                double totalWidth = monthRows.Count * 256;
                double viewportWidth = scroller.Bounds.Width;
                bool canScroll = totalWidth > viewportWidth;

                if (leftBtn != null) leftBtn.IsVisible = canScroll;
                if (rightBtn != null) rightBtn.IsVisible = canScroll;

                if (!_needsScrollToCurrentMonth)
                    return;

                var today = DateTime.Today;
                int targetIdx = -1;
                double minDiffDays = double.MaxValue;
                for (int i = 0; i < monthRows.Count; i++)
                {
                    var r = monthRows[i];
                    var diff = Math.Abs((new DateTime(r.Year, r.Month, 1) - today).TotalDays);
                    if (diff < minDiffDays)
                    {
                        minDiffDays = diff;
                        targetIdx = i;
                    }
                }

                if (targetIdx >= 0)
                {
                    double targetX = (targetIdx + 1) * 256 - viewportWidth;
                    scroller.Offset = new Avalonia.Vector(Math.Max(0, targetX), scroller.Offset.Y);
                    _needsScrollToCurrentMonth = false;
                }
            }
        }
    }
}
