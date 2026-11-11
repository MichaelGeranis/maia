using System.Windows.Input;

namespace Maia.App.Controls;

/// <summary>
/// A tap that runs a drawn control's command the way a Button runs its own: only while the command can run. A busy
/// screen, or a command still running from the last tap, keeps a second tap from starting it again (so a double tap on
/// "Link from contacts" opens the contacts app once), and the control reads as unavailable and its content dims
/// meanwhile. For the design's pills and panels, which are borders rather than buttons.
/// </summary>
internal sealed class CommandTap
{
    /// <summary>How far an unavailable control's content fades.</summary>
    private const double UnavailableOpacity = 0.5;

    private readonly Border _owner;
    private readonly TapGestureRecognizer _tap = new();
    private ICommand? _command;

    public CommandTap(Border owner)
    {
        _owner = owner;
        owner.GestureRecognizers.Add(_tap);
    }

    public void SetCommand(ICommand? command)
    {
        if (_command is not null)
        {
            _command.CanExecuteChanged -= OnCanExecuteChanged;
        }

        _command = command;
        _tap.Command = command;
        if (command is not null)
        {
            command.CanExecuteChanged += OnCanExecuteChanged;
        }

        Refresh();
    }

    public void SetParameter(object? parameter)
    {
        _tap.CommandParameter = parameter;
        Refresh();
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        if (_owner.Dispatcher.IsDispatchRequired)
        {
            _owner.Dispatcher.Dispatch(Refresh);
        }
        else
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var available = _command?.CanExecute(_tap.CommandParameter) != false;
        _owner.IsEnabled = available;
        if (_owner.Content is { } content)
        {
            content.Opacity = available ? 1 : UnavailableOpacity;
        }
    }
}
