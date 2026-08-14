using System;
using System.Windows;
using System.Windows.Forms;
using WirelessBatteryLevel.App.ViewModels;

namespace WirelessBatteryLevel.App.Controls
{
    public partial class TrayHoverPopup : Window
    {
        public TrayHoverPopup(TrayViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public void PositionNearTray()
        {
            var workingArea = SystemParameters.WorkArea;
            Left = workingArea.Right - Width - 8;
            Top = workingArea.Bottom - Height - 8;
        }
    }
}
