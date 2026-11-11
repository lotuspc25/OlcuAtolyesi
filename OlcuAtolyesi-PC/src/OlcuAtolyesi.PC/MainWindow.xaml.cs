using System.Windows;
using System.Windows.Input;
using OlcuAtolyesi.PC.ViewModels;

namespace OlcuAtolyesi.PC;

public partial class MainWindow : Window
{
    public MainWindow() : this(new MainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        ViewModel = viewModel;
        Title = viewModel.WindowTitle;
        Closed += (_, _) => ViewModel.Dispose();
    }

    public MainWindowViewModel ViewModel { get; }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var photo = vm.PhotoMeasurementPage;

        if (e.Key == Key.F2)
        {
            vm.OpenMeasurementWindow();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Left:
                    photo.ImageAdjustment.RotateLeftCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Right:
                    photo.ImageAdjustment.RotateRightCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.H:
                    photo.ImageAdjustment.FlipHorizontalCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.V:
                    photo.ImageAdjustment.FlipVerticalCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.L:
                    photo.ImageAdjustment.ResetAllCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }

        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None)
        {
            photo.FitViewCommand.Execute(null);
            e.Handled = true;
        }
    }
}
