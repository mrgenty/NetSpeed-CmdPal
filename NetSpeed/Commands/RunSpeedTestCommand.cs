using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NetSpeed;

internal sealed partial class RunSpeedTestCommand : InvokableCommand
{
    private readonly Action _run;

    public RunSpeedTestCommand(Action run)
    {
        _run = run;
    }

    public override string Name => "Run speed test";

    public override CommandResult Invoke()
    {
        _run();
        return CommandResult.KeepOpen();
    }
}
