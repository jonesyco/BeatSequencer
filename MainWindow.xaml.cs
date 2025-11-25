using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BeatSequencer.ViewModels;
using Microsoft.Win32;

namespace BeatSequencer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

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
}
