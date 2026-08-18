using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WirelessBatteryLevel.App.ViewModels;
using UserControl = System.Windows.Controls.UserControl;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace WirelessBatteryLevel.App.Controls
{
    public partial class SegmentedBarIcon : UserControl
    {
        private DeviceItemViewModel? _viewModel;
        private int _hoveredSegmentIndex = -1;

        public SegmentedBarIcon()
        {
            InitializeComponent();
            DataContextChanged += SegmentedBarIcon_DataContextChanged;

            var containers = new[] { SegContainer1, SegContainer2, SegContainer3, SegContainer4, SegContainer5 };
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                containers[i].MouseEnter += (s, e) =>
                {
                    _hoveredSegmentIndex = index;
                    UpdateSegmentVisuals();
                };
                containers[i].MouseLeave += (s, e) =>
                {
                    if (_hoveredSegmentIndex == index)
                    {
                        _hoveredSegmentIndex = -1;
                        UpdateSegmentVisuals();
                    }
                };
            }
        }

        private void SegmentedBarIcon_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _viewModel = e.NewValue as DeviceItemViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            UpdateSegmentVisuals();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeviceItemViewModel.BatteryLevel) ||
                e.PropertyName == nameof(DeviceItemViewModel.BatteryFillBrush) ||
                e.PropertyName == nameof(DeviceItemViewModel.IsMonochromeMode))
            {
                Dispatcher.Invoke(() => UpdateSegmentVisuals());
            }
        }

        private void SegContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateSegmentVisuals();
        }

        private void UpdateSegmentVisuals()
        {
            if (_viewModel == null) return;

            int level = _viewModel.BatteryLevel ?? 0;
            Brush activeBrush = _viewModel.BatteryFillBrush;
            bool isMonochrome = _viewModel.IsMonochromeMode;

            var containers = new[] { SegContainer1, SegContainer2, SegContainer3, SegContainer4, SegContainer5 };
            var litContainers = new[] { SegLitContainer1, SegLitContainer2, SegLitContainer3, SegLitContainer4, SegLitContainer5 };
            var litGrids = new[] { SegLitGrid1, SegLitGrid2, SegLitGrid3, SegLitGrid4, SegLitGrid5 };
            var litFills = new[] { SegLitFill1, SegLitFill2, SegLitFill3, SegLitFill4, SegLitFill5 };
            var accentHovers = new[] { SegAccentHover1, SegAccentHover2, SegAccentHover3, SegAccentHover4, SegAccentHover5 };
            var brightHovers = new[] { SegBrightHover1, SegBrightHover2, SegBrightHover3, SegBrightHover4, SegBrightHover5 };

            var unlitBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0x3F, 0x46));
            Brush accentBrush = (Application.Current?.Resources["SystemAccentBrush"] as Brush) ?? activeBrush;

            for (int i = 0; i < 5; i++)
            {
                double containerWidth = containers[i].ActualWidth;
                if (containerWidth <= 0) containerWidth = 20;

                int segMin = i * 20;
                int segMax = (i + 1) * 20;

                double fillRatio = 0.0;

                if (level >= segMax)
                {
                    fillRatio = 1.0;
                }
                else if (level <= segMin)
                {
                    fillRatio = 0.0;
                }
                else
                {
                    int rem = level - segMin;
                    int subRem = rem % 10;
                    if (subRem > 0 && subRem <= 5)
                    {
                        fillRatio = 0.5;
                    }
                    else
                    {
                        fillRatio = 1.0;
                    }
                }

                litContainers[i].Width = containerWidth * fillRatio;
                litGrids[i].Width = containerWidth;

                bool isThisSegHovered = (_hoveredSegmentIndex == i);

                if (fillRatio > 0)
                {
                    litFills[i].Fill = (isThisSegHovered && isMonochrome) ? accentBrush : activeBrush;
                    accentHovers[i].Opacity = 0;
                    brightHovers[i].Opacity = (isThisSegHovered && !isMonochrome) ? 0.35 : 0;
                }
                else
                {
                    litFills[i].Fill = activeBrush;
                    accentHovers[i].Opacity = isThisSegHovered ? 0.4 : 0;
                    brightHovers[i].Opacity = 0;
                }
            }
        }
    }
}
