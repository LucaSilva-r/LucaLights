using Avalonia.Controls;
using LTEK_ULed.ViewModels;

namespace LTEK_ULed.Views;

public partial class OsuPlayerWindow : Window
{
    public OsuPlayerWindow()
    {
        DataContext = new OsuPlayerViewModel();
        InitializeComponent();
    }
}
