# YOU Console Tool

This template creates a configurable console tool for local utilities,
simulation and offline integration work. The sample loop only writes logs and
never contacts a device, network address or production service.

## Run

```powershell
dotnet run --project .\src\YOU.Tool
```

Set `GuihuaTool:Iterations` to a positive number for a finite run. The default
value `0` means run until Ctrl+C. Press Ctrl+C for a normal cancellation; the
process returns exit code `130`. Invalid configuration returns `2`, an
unexpected failure returns `1`, and successful completion returns `0`.

## Configuration and logging

Edit `src/YOU.Tool/appsettings.json`, or use the normal .NET configuration
providers supported by `Host.CreateDefaultBuilder`. Logging uses
`Microsoft.Extensions.Logging` with the console provider.

## win-x64 self-contained publishing

Self-contained publishing is optional. For an offline Windows deployment:

```powershell
dotnet publish .\src\YOU.Tool\YOU.Tool.csproj -c Release -r win-x64 --self-contained true -o .\artifacts\win-x64
```

The generated project does not force self-contained publishing, so framework-dependent
and other runtime-specific deployments remain available.
