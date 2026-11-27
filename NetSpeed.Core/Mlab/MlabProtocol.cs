namespace NetSpeed.Core.Mlab;

public enum MlabProtocolKind
{
    /// <summary>MSAK throughput1: multi-stream, WebSocket over TLS.</summary>
    Msak,

    /// <summary>ndt7: single stream, WebSocket over TLS.</summary>
    Ndt7,
}

public enum TestDirection
{
    Download,
    Upload,
}

/// <summary>Static description of an M-Lab throughput protocol.</summary>
public sealed class MlabProtocol
{
    /// <summary>
    /// MSAK throughput1. Values from github.com/m-lab/msak pkg/throughput1/spec/spec.go
    /// and the Locate v2 answer for "msak/throughput1".
    /// </summary>
    public static readonly MlabProtocol Msak = new(
        MlabProtocolKind.Msak,
        displayName: "MSAK throughput1",
        locateService: "msak/throughput1",
        downloadKey: "wss:///throughput/v1/download",
        uploadKey: "wss:///throughput/v1/upload",
        subProtocol: "net.measurementlab.throughput.v1",
        maxMessageSize: 1 << 20);

    /// <summary>ndt7, from github.com/m-lab/ndt-server spec/ndt7-protocol.md (v0.11.0).</summary>
    public static readonly MlabProtocol Ndt7 = new(
        MlabProtocolKind.Ndt7,
        displayName: "ndt7",
        locateService: "ndt/ndt7",
        downloadKey: "wss:///ndt/v7/download",
        uploadKey: "wss:///ndt/v7/upload",
        subProtocol: "net.measurementlab.ndt.v7",
        maxMessageSize: 1 << 20);

    private MlabProtocol(
        MlabProtocolKind kind,
        string displayName,
        string locateService,
        string downloadKey,
        string uploadKey,
        string subProtocol,
        int maxMessageSize)
    {
        Kind = kind;
        DisplayName = displayName;
        LocateService = locateService;
        DownloadKey = downloadKey;
        UploadKey = uploadKey;
        SubProtocol = subProtocol;
        MaxMessageSize = maxMessageSize;
    }

    public MlabProtocolKind Kind { get; }

    public string DisplayName { get; }

    /// <summary>Service path appended to https://locate.measurementlab.net/v2/nearest/.</summary>
    public string LocateService { get; }

    /// <summary>Key of the download URL in the Locate "urls" map.</summary>
    public string DownloadKey { get; }

    public string UploadKey { get; }

    /// <summary>Value for Sec-WebSocket-Protocol.</summary>
    public string SubProtocol { get; }

    /// <summary>Initial upload binary message size (ndt7 spec: 1 &lt;&lt; 13).</summary>
    public int InitialMessageSize { get; } = 1 << 13;

    /// <summary>
    /// Upper bound for upload messages. ndt7 allows up to 1 &lt;&lt; 24; MSAK uses 1 &lt;&lt; 20
    /// (spec.MaxScaledMessageSize). 1 MiB is used for both.
    /// </summary>
    public int MaxMessageSize { get; }

    /// <summary>True when the protocol supports several parallel TCP streams per test.</summary>
    public bool SupportsMultipleStreams => Kind == MlabProtocolKind.Msak;
}
