using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CaDesk.Desktop.ViewModels;

namespace CaDesk.Desktop.Controls;

public static class DateInput
{
    public static readonly DependencyProperty ValidateProperty = DependencyProperty.RegisterAttached(
        "Validate", typeof(bool), typeof(DateInput), new PropertyMetadata(false, Changed));

    public static bool GetValidate(DependencyObject target) => (bool)target.GetValue(ValidateProperty);
    public static void SetValidate(DependencyObject target, bool value) => target.SetValue(ValidateProperty, value);

    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not DatePicker picker || args.NewValue is not true)
            return;
        picker.Loaded += OnLoaded;
        picker.DateValidationError += (_, e) =>
        {
            e.ThrowException = false;
            if (picker.DataContext is EditorViewModel editor)
            {
                editor.DateInputValid = false;
                editor.Error = WorkEditorViewModel.InvalidDateMessage;
            }
            else if (picker.DataContext is WorkItemViewModel item)
                item.ReportInvalidDate();
        };
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        var picker = (DatePicker)sender;
        if (picker.Template.FindName("PART_TextBox", picker) is not DatePickerTextBox input)
            return;
        input.TextChanged -= OnTextChanged;
        input.TextChanged += OnTextChanged;
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        var input = (DatePickerTextBox)sender;
        if (input.DataContext is not EditorViewModel editor)
            return;
        editor.DateInputValid = string.IsNullOrWhiteSpace(input.Text) ||
            DateTime.TryParse(input.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out _);
        if (editor.DateInputValid && editor.Error == WorkEditorViewModel.InvalidDateMessage)
            editor.Error = "";
    }
}
