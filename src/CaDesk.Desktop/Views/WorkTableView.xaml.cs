using System.Windows.Controls;
using CaDesk.Core;
using CaDesk.Desktop.ViewModels;
namespace CaDesk.Desktop.Views;
public partial class WorkTableView : UserControl
{
    public WorkTableView()=>InitializeComponent();
    private void OnSorting(object sender,DataGridSortingEventArgs e){e.Handled=true;if(DataContext is WorkTableViewModel vm&&Enum.TryParse<WorkSort>(e.Column.SortMemberPath,out var sort)){vm.SortBy(sort);}}
}
