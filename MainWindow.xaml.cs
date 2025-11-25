using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BeatSequencer.ViewModels;
using Microsoft.Win32;
using System.Windows.Controls.Primitives; // for ToggleButton
using BeatSequencer.ViewModels;


namespace BeatSequencer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    private bool _isDraggingSteps;
    private bool _dragValue;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
        Application.Current.Shutdown();
    }

    private void ExportWav_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Export Beat as WAV",
            Filter = "WAV files (*.wav)|*.wav",
            DefaultExt = ".wav",
            FileName = "beat_export.wav"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _viewModel.StartExport(dlg.FileName);
        }
    }

    private void StepButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ToggleButton tb && tb.DataContext is StepViewModel step)
        {
            // Decide what we're painting: flip from the current state
            _isDraggingSteps = true;
            _dragValue = !step.IsActive;

            // Apply to the first clicked step
            step.IsActive = _dragValue;

            tb.CaptureMouse();
            e.Handled = true; // prevent default toggle fighting us
        }
    }

    private void StepButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSteps) return;

        if (sender is ToggleButton tb && tb.DataContext is StepViewModel step)
        {
            // While dragging, any step we enter gets painted to the same value
            step.IsActive = _dragValue;
        }
    }

    private void StepButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingSteps = false;

        if (sender is ToggleButton tb)
        {
            tb.ReleaseMouseCapture();
        }
    }

    private void StepButton_PreviewMouseMove(object sender, MouseEventArgs e)
{
    if (!_isDraggingSteps) return;
    if (sender is ToggleButton tb && tb.DataContext is StepViewModel step)
    {
        step.IsActive = _dragValue;  // paint ON or OFF
    }
}

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        _isDraggingSteps = false;
    }


    // Change step velocity with mouse wheel over a pad.
    private void StepVelocity_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is StepViewModel svm)
        {
            double delta = (e.Delta > 0 ? 0.05 : -0.05);
            svm.Velocity = Math.Clamp(svm.Velocity + delta, 0.1, 1.0);
            e.Handled = true;
        }
    }

    // Pattern bank handlers (store)
    private void BankStoreA_Click(object sender, RoutedEventArgs e) => _viewModel.StorePatternToBank('A');
    private void BankStoreB_Click(object sender, RoutedEventArgs e) => _viewModel.StorePatternToBank('B');
    private void BankStoreC_Click(object sender, RoutedEventArgs e) => _viewModel.StorePatternToBank('C');
    private void BankStoreD_Click(object sender, RoutedEventArgs e) => _viewModel.StorePatternToBank('D');

    // Pattern bank handlers (recall)
    private void BankRecallA_Click(object sender, RoutedEventArgs e) => _viewModel.RecallPatternFromBank('A');
    private void BankRecallB_Click(object sender, RoutedEventArgs e) => _viewModel.RecallPatternFromBank('B');
    private void BankRecallC_Click(object sender, RoutedEventArgs e) => _viewModel.RecallPatternFromBank('C');
    private void BankRecallD_Click(object sender, RoutedEventArgs e) => _viewModel.RecallPatternFromBank('D');

    private void BankClearA_Click(object sender, RoutedEventArgs e) => _viewModel.ClearBank('A');
    private void BankClearB_Click(object sender, RoutedEventArgs e) => _viewModel.ClearBank('B');
    private void BankClearC_Click(object sender, RoutedEventArgs e) => _viewModel.ClearBank('C');
    private void BankClearD_Click(object sender, RoutedEventArgs e) => _viewModel.ClearBank('D');

}
