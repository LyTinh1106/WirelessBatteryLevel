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
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Rect = System.Windows.Rect;

namespace WirelessBatteryLevel.App.Controls
{
    public partial class CircularRingIcon : UserControl
    {
        private DeviceItemViewModel? _viewModel;
        private bool _isRingHovered = false;

        public CircularRingIcon()
        {
            InitializeComponent();
            DataContextChanged += CircularRingIcon_DataContextChanged;

            // Hover triggers when mouse enters/leaves this specific CircularRingIcon control
            MouseEnter += (s, e) =>
            {
                _isRingHovered = true;
                UpdateRingVisuals();
            };
            MouseLeave += (s, e) =>
            {
                _isRingHovered = false;
                UpdateRingVisuals();
            };

            // Left-click on Ring toggles Sub-mode (Progress Arc <-> Rise Up)
            MouseLeftButtonDown += (s, e) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.ToggleRingSubMode();
                    e.Handled = true;
                }
            };
        }

        private void CircularRingIcon_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
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

            UpdateRingVisuals();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DeviceItemViewModel.BatteryLevel) ||
                e.PropertyName == nameof(DeviceItemViewModel.BatteryFillBrush) ||
                e.PropertyName == nameof(DeviceItemViewModel.IsMonochromeMode) ||
                e.PropertyName == nameof(DeviceItemViewModel.RingSubMode))
            {
                Dispatcher.Invoke(() => UpdateRingVisuals());
            }
        }

        private void UpdateRingVisuals()
        {
            if (_viewModel == null) return;

            int level = _viewModel.BatteryLevel ?? 0;
            double ratio = Math.Max(0.0, Math.Min(1.0, level / 100.0));
            bool isRiseUp = _viewModel.IsRiseUpSubMode;

            Brush activeBrush = _viewModel.BatteryFillBrush;
            bool isMonochrome = _viewModel.IsMonochromeMode;
            Brush accentBrush = (Application.Current?.Resources["SystemAccentBrush"] as Brush) ?? activeBrush;

            // 0. Update Dynamic Tooltip with Left-click interaction instruction
            string levelText = _viewModel.BatteryTooltip;
            string hint = isRiseUp 
                ? $"{levelText} • Click to switch to Progress Arc Mode" 
                : $"{levelText} • Click to switch to Rise Up Mode";
            MainGrid.ToolTip = hint;

            // 1. Update Stroke Color based on direct Ring Hover & Monochrome Mode
            if (_isRingHovered && isMonochrome)
            {
                RingArcPath.Stroke = accentBrush;
                RingBrightHoverPath.Opacity = 0;
            }
            else
            {
                RingArcPath.Stroke = activeBrush;
                RingBrightHoverPath.Opacity = (_isRingHovered && !isMonochrome) ? 0.35 : 0;
            }

            // 2. Build Exact Co-axial Ring Geometry (57px x 57px, Center = (28.5, 28.5), Radius = 23.5, StrokeThickness = 5.0)
            double cx = 28.5;
            double cy = 28.5;
            double radius = 23.5;
            double strokeWidth = 5.0;

            var fullRingGeo = new EllipseGeometry(new Point(cx, cy), radius, radius);
            RingTrackPath.Data = fullRingGeo;

            if (isRiseUp)
            {
                // Rise Up Sub-Mode: Full ring stroke geometry clipped vertically from bottom to top along the ring border
                double minY = cy - radius - (strokeWidth / 2.0);
                double maxY = cy + radius + (strokeWidth / 2.0);
                double totalH = maxY - minY;

                double fillH = Math.Max(0, Math.Min(totalH, ratio * totalH));
                double clipY = maxY - fillH;

                var clipRect = new RectangleGeometry(new Rect(0, clipY, 57, fillH));

                RingArcPath.Data = fullRingGeo;
                RingArcPath.Clip = clipRect;

                RingBrightHoverPath.Data = fullRingGeo;
                RingBrightHoverPath.Clip = clipRect;
            }
            else
            {
                // Progress Arc Sub-Mode: Radial Arc Geometry starting from 12 o'clock
                RingArcPath.Clip = null;
                RingBrightHoverPath.Clip = null;

                if (ratio <= 0.001)
                {
                    RingArcPath.Data = null;
                    RingBrightHoverPath.Data = null;
                    return;
                }

                if (ratio >= 0.999)
                {
                    RingArcPath.Data = fullRingGeo;
                    RingBrightHoverPath.Data = fullRingGeo;
                    return;
                }

                double angleDegrees = ratio * 360.0;
                double angleRadians = (angleDegrees - 90.0) * Math.PI / 180.0;

                double startX = cx;
                double startY = cy - radius;

                double endX = cx + radius * Math.Cos(angleRadians);
                double endY = cy + radius * Math.Sin(angleRadians);

                bool isLargeArc = angleDegrees > 180.0;

                var pathFigure = new PathFigure
                {
                    StartPoint = new Point(startX, startY),
                    IsClosed = false
                };

                pathFigure.Segments.Add(new ArcSegment
                {
                    Point = new Point(endX, endY),
                    Size = new Size(radius, radius),
                    IsLargeArc = isLargeArc,
                    SweepDirection = SweepDirection.Clockwise,
                    IsStroked = true
                });

                var pathGeometry = new PathGeometry();
                pathGeometry.Figures.Add(pathFigure);

                RingArcPath.Data = pathGeometry;
                RingBrightHoverPath.Data = pathGeometry;
            }
        }
    }
}
