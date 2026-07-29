using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace EquipmentFailureAnalysis.Views
{
    public partial class FailureAnalysisView : UserControl
    {
        public FailureAnalysisView()
        {
            InitializeComponent();

            var scroller = this.FindControl<ScrollViewer>("FailureHeatmapScroller");
            if (scroller != null)
            {
                Border? wellBorder = null;
                var parent = scroller.Parent;
                while (parent != null)
                {
                    if (parent is Border b && b.Classes.Contains("well"))
                    {
                        wellBorder = b;
                        break;
                    }
                    parent = parent.Parent;
                }

                if (wellBorder != null)
                {
                    wellBorder.GetObservable(Visual.BoundsProperty).Subscribe(bounds =>
                    {
                        if (bounds.Width > 0)
                        {
                            double availableWidthForScroller = bounds.Width - 96.0;
                            int maxFits = (int)Math.Floor(availableWidthForScroller / 256.0);
                            int targetCount = Math.Max(1, maxFits);
                            double targetWidth = targetCount * 256.0;
                            if (double.IsNaN(scroller.Width) || Math.Abs(scroller.Width - targetWidth) > 0.001)
                            {
                                scroller.Width = targetWidth;
                            }
                        }
                    });
                }

                scroller.GetObservable(Visual.BoundsProperty).Subscribe(_ =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => TriggerScrollIfReady(), Avalonia.Threading.DispatcherPriority.Loaded);
                });

                scroller.GetObservable(ScrollViewer.ExtentProperty).Subscribe(_ =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => TriggerScrollIfReady(), Avalonia.Threading.DispatcherPriority.Loaded);
                });
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void FailureHeatmapSettingsButton_Click(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow host)
                host.FailureHeatmapSettingsButton_Click(sender, e);
        }

        private async void CopyInventoryNumberButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel vm && vm.SelectedEquipment != null && !string.IsNullOrWhiteSpace(vm.SelectedEquipment.InventoryNumber))
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(vm.SelectedEquipment.InventoryNumber);
                    vm.StatusMessage = $"Инвентарный номер скопирован: {vm.SelectedEquipment.InventoryNumber}";
                }
            }
        }

        private async void CopyJiraKeyButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainWindowViewModel vm && !string.IsNullOrWhiteSpace(vm.SelectedTimelineJiraKey) && vm.SelectedTimelineJiraKey != "-")
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(vm.SelectedTimelineJiraKey);
                    vm.StatusMessage = $"Ключ Jira скопирован: {vm.SelectedTimelineJiraKey}";
                }
            }
        }

        private void FailureEventRow_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control control)
                return;

            if (control.DataContext is not Models.Annotation annotation)
                return;

            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                vm.SelectedTimelineAnnotation = annotation;
            }
        }

        /// <summary>Reset-button click hook. The toast is shown by MainWindow via FiltersResetCounter.</summary>
        public void ResetFiltersWithToast_Click(object? sender, RoutedEventArgs e)
        {
            // No-op: toast is triggered by MainWindow observing FiltersResetCounter.
        }

        private void HeatmapScrollLeft_Click(object? sender, RoutedEventArgs e)
        {
            var scroller = this.FindControl<ScrollViewer>("FailureHeatmapScroller");
            if (scroller != null)
            {
                var offset = scroller.Offset;
                scroller.Offset = new Avalonia.Vector(Math.Max(0, offset.X - 256), offset.Y);
            }
        }

        private void HeatmapScrollRight_Click(object? sender, RoutedEventArgs e)
        {
            var scroller = this.FindControl<ScrollViewer>("FailureHeatmapScroller");
            if (scroller != null)
            {
                var offset = scroller.Offset;
                scroller.Offset = new Avalonia.Vector(offset.X + 256, offset.Y);
            }
        }

        private bool _needsScrollToCurrentMonth = true;
        private System.Collections.Specialized.INotifyCollectionChanged? _subscribedMonthRows;
        private ViewModels.MainWindowViewModel? _subscribedVm;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SubscribeMonthRows();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            UnsubscribeMonthRows();
        }

        private void SubscribeMonthRows()
        {
            UnsubscribeMonthRows();
            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                _subscribedVm = vm;
                _subscribedVm.PropertyChanged += OnVmPropertyChanged;
                _subscribedMonthRows = vm.MonthRows;
                if (_subscribedMonthRows != null)
                {
                    _subscribedMonthRows.CollectionChanged += OnMonthRowsChanged;
                }
            }
        }

        private void UnsubscribeMonthRows()
        {
            if (_subscribedVm != null)
            {
                _subscribedVm.PropertyChanged -= OnVmPropertyChanged;
                _subscribedVm = null;
            }
            if (_subscribedMonthRows != null)
            {
                _subscribedMonthRows.CollectionChanged -= OnMonthRowsChanged;
                _subscribedMonthRows = null;
            }
        }

        private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MainWindowViewModel.AnalysisDate))
            {
                _needsScrollToCurrentMonth = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(() => TriggerScrollIfReady(), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            SubscribeMonthRows();
            _needsScrollToCurrentMonth = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => TriggerScrollIfReady(), Avalonia.Threading.DispatcherPriority.Loaded);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BoundsProperty)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => TriggerScrollIfReady(), Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnMonthRowsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            _needsScrollToCurrentMonth = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => TriggerScrollIfReady(), Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void TriggerScrollIfReady()
        {
            var scroller = this.FindControl<ScrollViewer>("FailureHeatmapScroller");
            if (scroller == null || scroller.Bounds.Width <= 0)
                return;

            var leftBtn = this.FindControl<Button>("FailureHeatmapScrollLeftButton");
            var rightBtn = this.FindControl<Button>("FailureHeatmapScrollRightButton");

            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                var monthRows = vm.MonthRows;
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

                var activeDate = vm.AnalysisDate;
                int targetIdx = -1;
                double minDiffDays = double.MaxValue;
                for (int i = 0; i < monthRows.Count; i++)
                {
                    var r = monthRows[i];
                    var diff = Math.Abs((new DateTime(r.Year, r.Month, 1) - activeDate).TotalDays);
                    if (diff < minDiffDays)
                    {
                        minDiffDays = diff;
                        targetIdx = i;
                    }
                }

                if (targetIdx >= 0)
                {
                    double monthLeft = targetIdx * 256;
                    double monthRight = (targetIdx + 1) * 256;
                    double currentScrollX = scroller.Offset.X;

                    // If the target month is already fully visible, we don't need to change the scroll position
                    if (monthLeft >= currentScrollX && monthRight <= currentScrollX + viewportWidth + 1.0)
                    {
                        _needsScrollToCurrentMonth = false;
                        return;
                    }

                    double targetX = (targetIdx + 1) * 256 - viewportWidth;
                    targetX = Math.Max(0, targetX);
                    double maxScrollX = Math.Max(0, scroller.Extent.Width - scroller.Viewport.Width);
                    if (maxScrollX >= targetX || targetX == 0)
                    {
                        scroller.Offset = new Avalonia.Vector(targetX, scroller.Offset.Y);
                        _needsScrollToCurrentMonth = false;
                    }
                    else
                    {
                        scroller.Offset = new Avalonia.Vector(maxScrollX, scroller.Offset.Y);
                    }
                }
            }
        }
    }
}
