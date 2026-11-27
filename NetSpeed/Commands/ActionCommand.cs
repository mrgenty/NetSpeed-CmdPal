using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NetSpeed;

/// <summary>Runs an action and keeps Command Palette open on the current page.</summary>
internal sealed partial class ActionCommand : InvokableCommand
{
    private readonly Action _action;
    private readonly string _name;

    public ActionCommand(string name, Action action)
    {
        _name = name;
        _action = action;
    }

    public override string Name => _name;

    public override CommandResult Invoke()
    {
        _action();
        return CommandResult.KeepOpen();
    }
}
