using Avalonia.Controls;
using OlcuAtolyesi.Desktop.ViewModels;

namespace OlcuAtolyesi.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new DesktopMainViewModel();
    }
}
