# Guihua FreeSql Persistence Module

This module is an explicit compile-time integration for the generated YOU host. It supports SQLite and MySQL and defaults to SQLite in the sample configuration.

When the host is created with `dotnet new guihua --persistence freesql`, this module is already generated under `src/Modules`, added to the solution, referenced by the API project, and registered from `Program.cs`. The manual steps below are for adding the module to an existing project.

## Explicit integration

From the generated solution directory:

```powershell
dotnet add .\src\Product.Api\Product.Api.csproj reference .\modules\persistence-freesql\Guihua.Persistence.FreeSql.csproj
dotnet add .\src\Product.Core\Product.Core.csproj package FreeSql.Extensions.BaseEntity --version 3.5.310
```

The project containing business entities needs the second package reference so it can inherit `BaseEntity<TEntity, TKey>` directly; the default generated application remains free of this dependency until you opt into the module.

In the host composition root, add the module reference and registration explicitly:

```csharp
using Guihua.Persistence.FreeSql;

builder.Services.AddFreeSqlPersistence(
    builder.Configuration,
    typeof(ProductEntity).Assembly);
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

`ProductEntity` is a business entity that inherits `BaseEntity<TEntity, TKey>`:

```csharp
using FreeSql;
using FreeSql.DataAnnotations;

[Table(Name = "products")]
public sealed class ProductEntity : BaseEntity<ProductEntity, long>
{
    [Column(StringLength = 200, IsNullable = false)]
    public required string Name { get; set; }
}
```

The base class supplies `Id`, `CreateTime`, `UpdateTime`, `IsDeleted`, and `Sort`, along with Active Record CRUD methods. Pass every assembly containing such entities to `AddFreeSqlPersistence`; when `AutoSyncStructure` is `true`, the module initializes FreeSql at host startup and synchronizes only its own sample entity plus those explicitly supplied entity types.

Set `Provider` to `MySql` and use a MySQL connection string when deploying against MySQL. `AutoSyncStructure` is off by default; enabling it logs a warning and should be an intentional development or migration decision. SQLite creates its database file during synchronization; provision a MySQL database separately and let the application synchronize its tables.

The module does not scan assemblies and does not rewrite `Program.cs`. Add the project reference, the single registration call, and configuration explicitly.
