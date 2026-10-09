using System.Windows;
using CaDesk.Core;
using CaDesk.Desktop.ViewModels;
namespace CaDesk.Desktop.Views;
public sealed class DialogService(ShellViewModel shell,Window owner):IDialogService
{
    public void EditClient(Client? client)=>new EditorWindow(new ClientEditorViewModel(shell,client)){Owner=owner}.ShowDialog();
    public void EditWork(WorkRow? row,long? clientId=null)=>new EditorWindow(new WorkEditorViewModel(shell,row,clientId)){Owner=owner}.ShowDialog();
    public bool Confirm(string title,string description)=>new ConfirmWindow(title,description){Owner=owner}.ShowDialog()==true;
}
