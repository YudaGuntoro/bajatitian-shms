# SHMS-System Backend

Layered .NET 8 backend for the PT. Baja Titian Utama SHMS-System system.

## Projects

- `Web.API`: ASP.NET Core Web API.
- `Web.API.Domain`: authentication, response, and SHMS domain models.
- `Web.API.Persistence`: EF Core context, auth service, and SHMS database mappings.

## Database

Use the on-premise MySQL database:

```text
Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=bajatitian-shms;SslMode=None;AllowPublicKeyRetrieval=True;
```

Apply the SHMS schema from:

```text
Backend/database/bajatitian-shms.sql
```

For an existing database that still has legacy production-control tables, run:

```text
Web.API.Persistence/Migrations/20260730_001_drop_unused_tables.sql
```

## API Modules

- `POST /api/auth/login`
- `GET /api/shms-system/status`
- `GET|PUT /api/shms-system/mqtt-configuration`
- `GET /api/shms-system/log-buffer`
- `GET|PUT /api/shms-system/settings`

## Run

```powershell
$env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=bajatitian-shms;SslMode=None;AllowPublicKeyRetrieval=True;"
dotnet run --project Web.API\Web.API.csproj --urls http://localhost:5241
```
