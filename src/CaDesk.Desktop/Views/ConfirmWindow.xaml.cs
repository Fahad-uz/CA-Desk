using System.Windows;
namespace CaDesk.Desktop.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(string title, string description)
    {
        InitializeComponent();
        DataContext = new
        {
            Title = title,
            Description = description
        };
    }
    private void Delete(object sender, RoutedEventArgs e) => DialogResult = true;
}
