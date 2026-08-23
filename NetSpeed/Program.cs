using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer.CsWinRT;
using System.Threading;

namespace NetSpeed;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            global::Shmuelie.WinRTServer.ComServer server = new();
            ManualResetEvent extensionDisposedEvent = new(false);
            NetSpeedExtension extensionInstance = new(extensionDisposedEvent);

            server.RegisterClass<NetSpeedExtension, IExtension>(() => extensionInstance);
            server.Start();
            extensionDisposedEvent.WaitOne();
            server.Stop();
            server.UnsafeDispose();
        }
    }
}
