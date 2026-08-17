# Guihua Templates

`Guihua.Templates` 是一组保持核心简洁、通过显式模块扩展能力的 .NET 项目模板。它主要面向内网服务、仿真系统、设备集成和定制化后台等场景。

解决方案沿用 `YOU` 这一项目名称；NuGet 包与 `dotnet new` 模板使用更易区分的 `Guihua` 名称。

## 设计原则

- 默认生成 `net8.0`，并支持选择 `net10.0`；
- 基础 Web API 不预装数据库、Redis、JWT、AutoMapper、MediatR 等可选能力；
- 模块通过项目引用、依赖注入和端点注册显式接入，不使用运行时程序集扫描；
- 生成物本身必须可以还原、构建、测试和启动；
- HTTP 200 承载已处理的 JSON 业务结果，使用数值 `code`；未知路由等协议级结果保留真实 HTTP 状态码。

## 包含的模板

| Short name | 用途 |
|---|---|
| `guihua` | Controller Web API，包含 Api、Core、Infrastructure 和测试边界 |
| `guihua-module` | 为现有 Guihua 解决方案生成显式模块及测试 |
| `guihua-tool` | 生成可独立部署的控制台或集成工具 |

## 安装与使用

公开试用版发布到 NuGet 后，可安装指定版本：

```powershell
dotnet new install Guihua.Templates::1.0.0-beta.2
dotnet new list guihua
```

创建默认的 .NET 8 Web API：

```powershell
dotnet new guihua -n DeviceService -o .\DeviceService
```

创建 .NET 10 Web API：

```powershell
dotnet new guihua -n DeviceService -o .\DeviceService --framework net10.0
```

更多模板参数、模块接入和独立工具说明见 [NuGet 页面说明](packaging/README.md)。

## 本地验证

```powershell
dotnet restore .\Guihua.Template.sln
dotnet build .\Guihua.Template.sln -c Release --no-restore
dotnet test .\Guihua.Template.sln -c Release --no-build --no-restore
```

完整发布门禁会打包真实 `.nupkg`，在隔离模板环境中安装，并分别生成、构建、测试和启动 `net8.0`、`net10.0` 项目：

```powershell
.\eng\Test-GeneratedTemplate.ps1
```

## 可选模块

`modules/` 当前提供 FreeSql 持久化、HostedService 和 typed HttpClient 示例。它们不会进入默认生成物，需要项目按实际需求显式接入。

## 版本状态

`1.0.0-beta.1` 是迁移到 GitHub 后的首个公开试用版本；`1.0.0-beta.2` 补齐了 Web API 模板的 Development Swagger UI 启动配置。试用稳定后再发布 `1.0.0`，NuGet 包发布与 GitHub 源码版本保持一致。

## License

[MIT](LICENSE)
