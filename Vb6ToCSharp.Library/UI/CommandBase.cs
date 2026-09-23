using System;
using System.Windows.Input;

namespace Vb6ToCSharp.UI;

public class CommandBase : ICommand
{
    private readonly Func<bool> mCanExecute;
    private readonly Action<object> mExecute;

    public CommandBase(Action<object> vExecute, Func<bool> fCanExecute = null)
    {
        mCanExecute = fCanExecute;
        mExecute = vExecute;
    }

#pragma warning disable CS0067
    public event EventHandler CanExecuteChanged;
#pragma warning restore CS0067

    public bool CanExecute(object parameter)
    {
        return mCanExecute == null ? true : mCanExecute.Invoke();
    }

    public void Execute(object parameter)
    {
        mExecute.Invoke(parameter);
    }
}