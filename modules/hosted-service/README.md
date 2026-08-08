# Guihua HostedService 模块

这个模块为 Guihua 宿主提供一个显式注册的后台循环。它只负责宿主生命周期、配置校验、取消传播、周期等待和异常策略，不包含 UDP、DDS、串口或任何真实设备连接。

## 接入 YOU 主模板

先将模块的 `src/Guihua.HostedService` 复制到生成项目的 `src/Modules/Guihua.HostedService`，然后在生成项目根目录执行：

```powershell
dotnet sln .\YOU.sln add `
  .\src\Modules\Guihua.HostedService\Guihua.HostedService.csproj
dotnet add .\src\YOU.Api\YOU.Api.csproj reference `
  .\src\Modules\Guihua.HostedService\Guihua.HostedService.csproj
```

然后在 `src/YOU.Api/Program.cs` 的组合根显式注册模块。模块不会自动扫描或修改该文件。

## 接入宿主

1. 将 `src/Guihua.HostedService` 加入宿主解决方案，或引用对应模块包。
2. 在宿主组合根显式注册适配器和模块：

```csharp
using Guihua.HostedService;

builder.Services.AddGuihuaHostedService<DeviceCommunicationAdapter>(builder.Configuration);
```

`DeviceCommunicationAdapter` 由业务项目实现 `IHostedServiceAdapter`。模块不会扫描程序集，也不会自动修改 `Program.cs`。适配器的依赖可以继续通过宿主的 DI 显式注册。

只想验证接入流程时，也可以使用内置的无害日志示例：

```csharp
builder.Services.AddGuihuaHostedService(builder.Configuration);
```

3. 在宿主配置中加入强类型配置。模块会在宿主启动时执行校验，非法配置会阻止启动：

```json
{
  "GuihuaHostedService": {
    "IntervalMilliseconds": 1000,
    "FailurePolicy": "Continue"
  }
}
```

`IntervalMilliseconds` 必须为 1 到 86400000；`FailurePolicy` 为 `Continue` 或 `Stop`。`Continue` 记录异常并等待下一周期，`Stop` 记录异常后退出后台循环，但不会强制关闭整个 Web 宿主。

## 适配器落点

```csharp
public sealed class DeviceCommunicationAdapter(
    ILogger<DeviceCommunicationAdapter> logger,
    IDeviceTransport transport) : IHostedServiceAdapter
{
    public async ValueTask ExecuteCycleAsync(CancellationToken cancellationToken)
    {
        var packet = await transport.ReceiveAsync(cancellationToken);
        logger.LogDebug("Received {Length} bytes", packet.Length);
    }
}
```

设备、协议和传输抽象属于业务项目或独立适配器项目；不要把具体 UDP/DDS 类型放进本模块。每次循环都必须使用传入的 `CancellationToken`，以便应用正常停止。

## 验证

在模块目录执行：

```powershell
dotnet restore .\src\Guihua.HostedService\Guihua.HostedService.csproj
dotnet build .\src\Guihua.HostedService\Guihua.HostedService.csproj -c Release --no-restore
dotnet test .\tests\Guihua.HostedService.Tests\Guihua.HostedService.Tests.csproj -c Release
```

测试使用内存配置、替换的 `TimeProvider` 和内存日志捕获器，不连接真实设备、UDP/DDS 网络或外部服务。
