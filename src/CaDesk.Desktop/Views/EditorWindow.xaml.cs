using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CaDesk.Desktop.ViewModels;
namespace CaDesk.Desktop.Views;
public partial class EditorWindow : Window
{
    public EditorWindow(EditorViewModel vm)
    {
        InitializeComponent();DataContext=vm;vm.Saved+=(_,_)=>Close();
        Loaded+=(_,_)=>{UpdateLayout();var box=FindBox(this);if(box!=null){box.Focus();Keyboard.Focus(box);box.SelectAll();}};
    }
    private static TextBox? FindBox(DependencyObject parent)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is TextBox box)return box;var nested=FindBox(child);if(nested!=null)return nested;}return null;
    }
}
