# YOU Web API

这是由 Guihua 模板生成的简洁 Controller WebApi。默认只包含 Api、Core、Infrastructure 边界和测试，不连接数据库、缓存、设备或外部服务。

## 开始使用

```powershell
dotnet restore .\YOU.sln
dotnet build .\YOU.sln -c Release --no-restore -v minimal
dotnet test .\YOU.sln -c Release --no-build --no-restore -v minimal
dotnet run --project .\src\YOU.Api\YOU.Api.csproj
```

默认模板使用生成时选择的单一目标框架：

- `dotnet new guihua -n MyService`：net8.0；
- `dotnet new guihua -n MyService --framework net10.0`：net10.0。

选择带 SQLite 开发配置、`BaseEntity` 示例和启动建表的 FreeSql 持久化：

```powershell
dotnet new guihua -n MyService --persistence freesql
```

选择编译期生成代码、无运行时反射的 Mapperly 对象映射：

```powershell
dotnet new guihua -n MyService --mapping mapperly
```

模板会同时选择匹配的 SDK 和条件包版本。需要切换大版本时，建议重新生成后迁移业务代码，或者同时审查 `global.json`、`Directory.Build.props` 和 `Directory.Packages.props`，不要只修改 csproj。

## API 合同

API 路由以生成时的项目名开头。例如执行 `dotnet new guihua -n DeviceService` 后，问候接口是 `/DeviceService/demo/greeting`；多个服务共用网关时，可用各自的项目前缀区分路由。生成后若重命名项目，需要同步调整路由和调用方。

普通业务结果使用 HTTP 200，JSON 中的数值 `code` 表示应用结果：

```json
{
  "success": true,
  "code": 200,
  "message": "操作成功。",
  "result": {},
  "errors": null,
  "traceId": "..."
}
```

模型校验、业务失败和未处理异常也遵循这一合同；未知路由等协议级结果仍保持真实 HTTP 404。不要改成字符串业务码，除非具体项目已明确承担前后端合同迁移成本。

开发或测试环境的 OpenAPI JSON 位于 `/swagger/v1/swagger.json`，生产环境默认不暴露。

## 项目边界

- `src/YOU.Api`：HTTP、Controller、统一响应与组合根；
- `src/YOU.Core`：与传输和基础设施无关的 Feature/UseCase；
- `src/YOU.Infrastructure`：显式基础设施注册；默认不连接外部资源；
- `tests/YOU.Api.Tests`：API 合同与启动测试；
- `tests/YOU.ArchitectureTests`：Core 依赖方向测试。

按 Feature 组织新增业务，不要先创建全局 Repository、UnitOfWork、MediatR 或 AutoMapper 样板。

## 可选模块

FreeSql 可通过 `--persistence freesql` 在生成时显式带入；Mapperly 可通过 `--mapping mapperly` 显式带入，两者可以组合使用。未选择时，默认生成物不包含对应依赖。HostedService 和 typed HttpClient 仍需从 [Guihua Templates 仓库](https://github.com/WuXain/guihua-templates) 的 `modules/` 目录按模块 README 显式接入，因为它们需要具体的业务适配器或外部地址。
__GUIHUA_FREESQL_README__
__GUIHUA_MAPPERLY_README__
