using System.Windows;
using System.Windows.Input;
using OlcuAtolyesi.PC.ViewModels;

namespace OlcuAtolyesi.PC.Views;

/// <summary>
/// Fotoğraftan ölçüm için bağımsız, tam ekran çalışma penceresi.
/// Ana penceredeki <see cref="PhotoMeasurementViewModel"/> örneğini paylaşır; böylece noktalar,
/// sonuç ve büyüteç ayarları iki pencere arasında anında senkron kalır.
/// </summary>
public partial class MeasurementWindow : Window
{
    public MeasurementWindow(PhotoMeasurementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // F11: tam ekran / normal pencere geçişi
        if (e.Key == Key.F11)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            e.Handled = true;
        }
    }
}
