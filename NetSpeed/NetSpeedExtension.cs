using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CommandPalette.Extensions;

namespace NetSpeed;

[Guid("C71F961F-3B9A-4380-B42C-7E5712AEE055")]
public sealed partial class NetSpeedExtension : IExtension, IDisposable
{
    private readonly ManualResetEvent _extensionDisposedEvent;
    private readonly NetSpeedCommandsProvider _provider = new();

    public NetSpeedExtension(ManualResetEvent extensionDisposedEvent)
    {
        _extensionDisposedEvent = extensionDisposedEvent;
    }

    public object? GetProvider(ProviderType providerType) => providerType switch
    {
        ProviderType.Commands => _provider,
        _ => null,
    };

    public void Dispose() => _extensionDisposedEvent.Set();
}
