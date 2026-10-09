using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CaDesk.Desktop.ViewModels;
namespace CaDesk.Desktop.Views;

public partial class EditorWindow : Window
{
    public EditorWindow(EditorViewModel vm, Func<bool>? confirmDiscard = null)
    {
        InitializeComponent();
        DataContext = vm;
        var saved = false;
        if (vm is NoteEditorViewModel) { Width = 760; Height = 820; MinWidth = 560; }
        vm.Saved += (_, _) => { saved = true; Close(); };
        Closing += (_, e) =>
        {
            if (vm.IsSaving) { e.Cancel = true; return; }
            if (!saved && vm is NoteEditorViewModel { IsDirty: true })
                e.Cancel = !(confirmDiscard?.Invoke() ?? MessageBox.Show(this, "Discard the changes to this study note?", "Unsaved changes", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes);
        };
        Loaded += (_, _) => { UpdateLayout(); var box = FindBox(this); if (box != null) { box.Focus(); Keyboard.Focus(box); box.SelectAll(); } };
    }
    private static TextBox? FindBox(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBox box)
                return box;
            var nested = FindBox(child);
            if (nested != null)
                return nested;
        }
        return null;
    }
}
