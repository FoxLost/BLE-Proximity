using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BLEProximity.Models;
using BLEProximity.Services;
using BLEProximity.ViewModels;

namespace BLEProximity;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private WindowBehaviorManager? _windowBehaviorManager;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Attaches the WindowBehaviorManager to this window.
    /// Call this after services are initialized (e.g., from App.xaml.cs startup).
    /// </summary>
    public void AttachWindowBehavior(WindowBehaviorManager windowBehaviorManager)
    {
        _windowBehaviorManager = windowBehaviorManager ?? throw new ArgumentNullException(nameof(windowBehaviorManager));
        _windowBehaviorManager.Attach(this);
    }

    private void ScannedDevicesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is ScannedDevice device)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.AddTrustedDeviceCommand.Execute(device);
            }
        }
    }

    private void AddToTrusted_Click(object sender, RoutedEventArgs e)
    {
        if (ScannedDevicesGrid.SelectedItem is ScannedDevice device)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.AddTrustedDeviceCommand.Execute(device);
            }
        }
    }

    private void TrustedDevicesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row == null)
            return;

        row.Focus();
        row.IsSelected = true;
        TrustedDevicesGrid.SelectedItem = row.Item;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;

            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }

        return null;
    }
}
