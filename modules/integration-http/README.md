# HTTP Integration 模块

这个模块为一个外部系统提供一个显式的 typed client 示例：`IExternalStatusClient` / `ExternalStatusClient`。它使用 `IHttpClientFactory` 管理生命周期，不提供 `Get<T>`、`Post<T>` 等全局万能 HTTP 包装器。

## 接入 YOU 主模板

在生成的 YOU 解决方案中执行以下步骤（假设本模块源码位于仓库的 `modules/integration-http`）：

1. 将模块项目复制到宿主解决方案，例如 `src/Modules/Guihua.Integration.Http/`。
2. 将 `src/Guihua.Integration.Http/Guihua.Integration.Http.csproj` 加入解决方案：

   ```powershell
   dotnet sln .\YOU.sln add .\src\Modules\Guihua.Integration.Http\Guihua.Integration.Http.csproj
   ```

3. 为实际宿主项目添加项目引用，使组合根可以使用模块注册入口：

   ```powershell
   dotnet add .\src\YOU.Api\YOU.Api.csproj reference .\src\Modules\Guihua.Integration.Http\Guihua.Integration.Http.csproj
   ```

4. 在宿主组合根（通常是 `Program.cs`）显式注册模块：

   ```csharp
   using Guihua.Integration.Http;

   builder.Services.AddHttpIntegration(builder.Configuration);
   ```

5. 在配置文件中提供地址和超时。模块会在宿主启动时校验配置：

   ```json
   {
     "IntegrationHttp": {
       "BaseAddress": "https://example.invalid/",
       "Timeout": "00:00:10",
       "EnableResilience": false
     }
   }
   ```

   `BaseAddress` 必须是绝对的 `http` 或 `https` URI，`Timeout` 必须大于零。示例地址使用 `.invalid` 保证不会被误当成生产地址；真实地址应由部署配置提供。

6. 在自己的 Feature 中为每个外部系统定义窄的 typed client 接口和实现，沿用 `ExternalStatusClient` 的方式。不要把所有外部系统收敛成一个跨领域的 `IHttpService`。

## 弹性与重试边界

`EnableResilience` 默认是 `false`。开启后，内置的轻量策略只对 `GET`、`HEAD`、`OPTIONS` 在连接异常或 `408`、`429`、`5xx` 响应时按 `MaxRetries` 重试；`MaxRetries` 默认是 2。`POST`、`PATCH` 等非幂等请求不会被该模块自动重试，避免重复创建或重复提交。`PUT`、`DELETE` 即使在业务语义上可能幂等，也不在默认安全重试集合中。

如果业务确认某个写操作具备端到端幂等键和可重放请求体，应在对应 typed client 的组合根中显式加入经过验证的专用策略，并单独测试；不要通过修改全局 HTTP 注册来放宽所有请求的重试边界。

测试和示例均使用 mock `HttpMessageHandler`，模块本身不会调用公网服务。
