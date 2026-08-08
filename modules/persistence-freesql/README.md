# Guihua FreeSql Persistence Module

This module is an explicit compile-time integration for the generated YOU host. It supports SQLite and MySQL and defaults to SQLite in the sample configuration.

## Explicit integration

From the generated solution directory:

```powershell
dotnet add .\src\Product.Api\Product.Api.csproj reference .\modules\persistence-freesql\Guihua.Persistence.FreeSql.csproj
```

In the host composition root, add the module reference and registration explicitly:

```csharp
using Guihua.Persistence.FreeSql;

builder.Services.AddFreeSqlPersistence(builder.Configuration);
```

Add configuration under `FreeSqlPersistence`:

```json
{
  "FreeSqlPersistence": {
    "Provider": "Sqlite",
    "ConnectionString": "Data Source=./app.db",
    "AutoSyncStructure": false
  }
}
```

Set `Provider` to `MySql` and use a MySQL connection string when deploying against MySQL. `AutoSyncStructure` is off by default; enabling it logs a warning and should be an intentional development or migration decision.

The module does not scan assemblies and does not rewrite `Program.cs`. Add the project reference, the single registration call, and configuration explicitly.
