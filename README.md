# SHMS-System

Structural Health Monitoring System for **PT. Baja Titian Utama**.

## Included modules

- JWT login with the existing authentication flow
- SHMS dashboard
- MQTT configuration
- Location monitoring
- Sensor log buffer
- System settings
- MySQL schema and starter data

## Project structure

- `Frontend` - JavaScript/Next.js frontend application
- `Backend` - C#/.NET API, domain/persistence projects, database script, and backend assets

## Default demo access

```text
Username: root
Password: root_native
```

## Database

MySQL 8 is required. From the repository root, run:

```powershell
mysql -u root -p -e "source Backend/database/bajatitian-shms.sql"
```

The script creates `bajatitian_shms`, the login user, SHMS tables, and starter records.

## Run locally

API:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=bajatitian-shms;SslMode=None;AllowPublicKeyRetrieval=True;"
dotnet run --project Backend\Web.API\Web.API.csproj
```

Frontend, in another terminal:

```powershell
Set-Location Frontend
npm install
$env:NEXT_PUBLIC_API_BASE_URL="http://localhost:5241"
npm run dev
```

Open `http://localhost:3000`.

## Core API

Core endpoints:

- `POST /api/auth/login`
- `GET /api/shms-system/status`
- `GET|PUT /api/shms-system/mqtt-configuration`
- `GET /api/shms-system/log-buffer`
- `GET|PUT /api/shms-system/settings`
