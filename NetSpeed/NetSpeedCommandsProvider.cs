using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NetSpeed;

public partial class NetSpeedCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;

    public NetSpeedCommandsProvider()
    {
        DisplayName = "NetSpeed";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");

        _commands =
        [
            new CommandItem(new NetSpeedPage())
            {
                Title = "NetSpeed",
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
}
