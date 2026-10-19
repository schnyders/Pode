# Performance

Pode runs your routes, middleware, and endware as PowerShell, and every PowerShell function or scriptblock call allocates memory. Even a simple request allocates over 1MB, so the .NET garbage collector (GC) runs very often - and with the default settings, this is what limits how many requests a server can handle, more so the more threads you use.

You can change how the GC behaves via environment variables. These have to be set *before* PowerShell starts, as the GC can't be changed once a process is running.

## Gen0 Size

The simplest, and recommended, option is to increase the size of the GC's youngest generation (gen0), so it runs less often:

```powershell
# PowerShell
$env:DOTNET_GCgen0size = '0x4000000'
pwsh -File ./server.ps1
```

```bash
# Linux/macOS
DOTNET_GCgen0size=0x4000000 pwsh -File ./server.ps1
```

The value is in hex, and `0x4000000` is 64MB. This has little effect on the memory used by the server, and is set by default in Pode's Docker images (except ARM32).

## Server GC

For servers using several threads (`Start-PodeServer -Threads`), the server GC scales best. It uses a GC heap per CPU core, so it uses roughly twice as much memory:

```bash
DOTNET_gcServer=1 pwsh -File ./server.ps1
```

You can limit how many heaps (and so how much memory) it uses via `DOTNET_GCHeapCount`, for example `DOTNET_GCHeapCount=4`.

## Comparison

The following was measured on PowerShell 7.6, on a machine with 18 logical CPUs, for a route returning a small JSON response, with 12 concurrent connections (4 for the 1 thread column):

| Settings | 1 Thread | 4 Threads | 12 Threads | Memory (12 Threads) |
| -------- | -------- | --------- | ---------- | ------------------- |
| Default | 1,559 req/s | 1,900 req/s | 1,565 req/s | 640MB |
| `DOTNET_GCgen0size=0x4000000` | 1,914 req/s | 3,481 req/s | 2,835 req/s | 649MB |
| `DOTNET_gcServer=1` | - | 3,469 req/s | 4,753 req/s | 1,240MB |

!!! note
    Your results will depend on your routes and machine, so it's worth measuring with your own server. With the default settings, more threads can even be *slower* than fewer threads.

## Running as a Service

When running Pode as a service, or in a container, set the environment variable(s) in the service's or container's definition. For example, in a Dockerfile:

```dockerfile
ENV DOTNET_GCgen0size=0x4000000
```

Or in a systemd unit:

```ini
[Service]
Environment=DOTNET_GCgen0size=0x4000000
```
