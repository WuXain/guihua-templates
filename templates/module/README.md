# Guihua explicit module template

This template creates `ModuleName` and `ModuleName.Tests`. It is intentionally an explicit compile-time module: it does not scan assemblies, discover a host, or edit `Program.cs`.

## Add it to a host

Run these commands from the existing solution directory after generation:

```powershell
dotnet new guihua-module -n DeviceCommunication -o .\src\Modules\DeviceCommunication
dotnet sln .\YOU.sln add `
  .\src\Modules\DeviceCommunication\DeviceCommunication\DeviceCommunication.csproj `
  .\src\Modules\DeviceCommunication\DeviceCommunication.Tests\DeviceCommunication.Tests.csproj
dotnet add .\src\YOU.Api\YOU.Api.csproj reference `
  .\src\Modules\DeviceCommunication\DeviceCommunication\DeviceCommunication.csproj
```

The generated test project already references the module project. The explicit `dotnet add reference` command above connects the host composition root to the module; generating files alone never changes the host.

In the host's composition root, register and map the module explicitly:

```csharp
builder.Services.AddDeviceCommunication(builder.Configuration);

var app = builder.Build();
app.MapDeviceCommunicationEndpoints();
```

The module binds the `DeviceCommunication` configuration section and validates it at startup:

```json
{
  "DeviceCommunication": {
    "ServiceName": "device-communication",
    "Enabled": true
  }
}
```

The sample endpoint is `GET /modules/DeviceCommunication/status`. Replace the sample endpoint and options with the module's narrow use case as the module evolves.
