# Tyresoles Back-End

Production-ready multi-tenant SQL data access for Business Central / Dynamics NAV style databases.

## Projects

| Project | Description |
|---------|-------------|
| **Tyresoles.Sql** | Core library for query building, materialization, and tenant-scoped data access |
| **Tyresoles.Api** | ASP.NET Core Web API with health checks and configuration |
| **Tyresoles.Sql.Cli** | CLI and model generator (packable as global tool `tyresoles`) |

## Quick Start

### Run the API

```bash
cd back-end
dotnet run --project Tyresoles.Api
```

- **Health check**: `GET /health`
- **OpenAPI**: `GET /openapi/v1.json` (in Development)

### Configuration

Configure tenants in `appsettings.json` or via environment variables:

```json
{
  "Tyresoles": {
    "DefaultTenantKey": "NavLive",
    "Tenants": {
      "NavLive": {
        "ConnectionString": "Server=.;Database=YourDb;Trusted_Connection=True;TrustServerCertificate=True",
        "DefaultCompany": "CRONUS"
      }
    }
  }
}
```

For production, use environment variables or secrets:
- `Tyresoles__Tenants__NavLive__ConnectionString`

## Usage (Library)

```csharp
// DI registration
services.AddTyresolesSql(configuration);

// Usage
using (var scope = dataverse.ForTenant("NavLive"))
{
    var items = await scope.Query<Item>()
        .Where(x => x.Blocked == false)
        .OrderBy(x => x.No)
        .ToArrayAsync();
}
```

### Model generation (from Tyresoles.config.json)

From the `back-end` folder (where `Tyresoles.config.json` lives):

```bash
# Option A: run without installing
dotnet run --project Tyresoles.Sql.Cli -- generate

# Option B: install as global tool, then run (use tyresoles, not dotnet tyresoles)
dotnet pack Tyresoles.Sql.Cli -c Release -o nupkg
dotnet tool install -g --add-source ./nupkg Tyresoles.Cli
tyresoles generate
```

Generated models are written to `Generated/NavModels.g.cs`.

## Build

```bash
dotnet build
dotnet test
```
