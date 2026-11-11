using System.Windows;
using OlcuAtolyesi.PC.ViewModels;

namespace OlcuAtolyesi.PC.Views;

public partial class DiagnosticConsoleWindow : Window
{
    private readonly DiagnosticConsoleViewModel _viewModel;

    public DiagnosticConsoleWindow(DiagnosticConsoleViewModel? viewModel = null)
    {
        InitializeComponent();
        _viewModel = viewModel ?? new DiagnosticConsoleViewModel();
        DataContext = _viewModel;
        Title = _viewModel.WindowTitle;

        _viewModel.RequestScrollToEnd += OnRequestScrollToEnd;
        Closed += OnClosed;
    }

    public DiagnosticConsoleViewModel ViewModel => _viewModel;

    private void OnRequestScrollToEnd(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (LogDataGrid.Items.Count > 0)
            {
                var lastItem = LogDataGrid.Items[^1];
                LogDataGrid.ScrollIntoView(lastItem);
            }
        });
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.RequestScrollToEnd -= OnRequestScrollToEnd;
    }
}
