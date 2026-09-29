# YOU / Guihua .NET Web API Templates

`Guihua.Templates` provides three lightweight `dotnet new` templates for internal, offline, simulation, device-integration, and other customized .NET services.

The base Web API stays deliberately small: it starts without a database, Redis, Docker, authentication service, or any other external dependency. Additional capabilities are added explicitly when a project actually needs them.

## Included templates

| Short name | Purpose |
|---|---|
| `guihua` | Controller-based Web API with explicit Api, Core, and Infrastructure boundaries |
| `guihua-module` | Compile-time module plus tests for an existing Guihua solution |
| `guihua-tool` | Configurable console or integration tool suitable for standalone deployment |

All templates are created from one package. The application and tool templates generate `net8.0` by default and can explicitly generate `net10.0`.

## Install

This is a prerelease package, so install the exact version:

```powershell
dotnet new install Guihua.Templates@1.0.0-beta.5
dotnet new list guihua
```

## Create a Web API

Create the default .NET 8 application:

```powershell
dotnet new guihua -n DeviceService -o .\DeviceService
```

Generated API routes begin with the project name. For this example, the greeting endpoint is `/DeviceService/demo/greeting`, so services have distinct route prefixes at a shared gateway.

Create a project with the optional FreeSql persistence module, SQLite development configuration, and a `BaseEntity` sample:

```powershell
dotnet new guihua -n DeviceService -o .\DeviceService --persistence freesql
```

Create a project with Mapperly compile-time mapping and an API boundary mapping example:

```powershell
dotnet new guihua -n DeviceService -o .\DeviceService --mapping mapperly
```

The options compose when both capabilities are needed:

```powershell
dotnet new guihua -n DeviceService -o .\DeviceService --persistence freesql --mapping mapperly
```

Or create a .NET 10 application:

```powershell
dotnet new guihua -n DeviceService -o .\DeviceService --framework net10.0
```

Then build, test, and run it:

```powershell
Set-Location .\DeviceService
dotnet restore .\DeviceService.sln
dotnet build .\DeviceService.sln -c Release --no-restore
dotnet test .\DeviceService.sln -c Release --no-build --no-restore
dotnet run --project .\src\DeviceService.Api\DeviceService.Api.csproj
```

The default generated application does not include FreeSql, Redis, JWT, WebSocket, MediatR, Mapperly, AutoMapper, NLog, RestSharp, or Refit. Selecting `--persistence freesql` explicitly includes FreeSql and its local SQLite development setup. Selecting `--mapping mapperly` includes only Mapperly's compile-time source generator and a typed mapping example; it adds no runtime reflection service.

## Add an explicit module

Run the module template from an existing generated solution so it can inherit the host's target framework and central package versions:

```powershell
dotnet new guihua-module `
  -n DeviceCommunication `
  -o .\src\Modules\DeviceCommunication
```

The generated README gives the exact `dotnet sln add`, project-reference, dependency-injection, and endpoint-registration steps. The template never scans assemblies or rewrites an unknown `Program.cs` automatically.

## Create a standalone tool

```powershell
dotnet new guihua-tool -n UdpSimulator -o .\UdpSimulator
```

The generated tool supports configuration, console logging, cancellation, stable exit codes, and optional `win-x64` self-contained publishing. Its sample loop does not contact a device or production endpoint.

## API response convention

Handled JSON business results use HTTP 200 and a numeric application result code:

```json
{
  "success": true,
  "code": 200,
  "message": "Operation succeeded.",
  "result": {},
  "errors": null,
  "traceId": "..."
}
```

Unknown routes and other protocol-level behavior keep their real HTTP status codes. Development and Testing environments expose OpenAPI JSON at `/swagger/v1/swagger.json`.

## Compatibility and status

- `net8.0` is the default compatibility channel for existing work projects.
- `net10.0` is available for projects whose deployment environment supports it.
- This beta is intended for evaluation before a stable release.
- The package is licensed under the MIT License.

Source and documentation: [Guihua Templates repository](https://github.com/WuXain/guihua-templates)
