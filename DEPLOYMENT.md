# On-Premise Deployment

This project is intended to run against the on-premise MySQL database.

## Requirements

- Windows Server or Windows workstation
- .NET 8 Runtime or SDK
- Node.js 20+
- MySQL 8 at `127.0.0.1:3306`

## Database

Connection string:

```text
Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=bajatitian_shms;SslMode=None;AllowPublicKeyRetrieval=True;
```

Apply the schema:

```powershell
mysql -h 127.0.0.1 -P 3306 -u root -p -e "source Backend/database/bajatitian-shms.sql"
```

## API

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
$env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=bajatitian_shms;SslMode=None;AllowPublicKeyRetrieval=True;"
dotnet run --project Backend\Web.API\Web.API.csproj --urls http://localhost:5241
```

Swagger:

```text
http://localhost:5241/swagger
```

## Frontend

```powershell
Set-Location Frontend
npm install
$env:NEXT_PUBLIC_API_BASE_URL="http://localhost:5241"
npm run dev
```

Open:

```text
http://localhost:3000
```

## Updating an existing Docker VPS deployment

Updated for the 15 October 2026 code changes. These instructions describe the
repository's Docker Compose deployment. They do not confirm the current state of
your VPS; no SSH connection or remote deployment has been performed.

The confirmed existing VPS project directory is `/home/ubuntu/bajatitian-shms`,
the remote is `https://github.com/YudaGuntoro/bajatitian-shms.git`, and the branch is
`main`. Use your actual installed directory if it differs. A deployment using
PM2, systemd, IIS, or Windows services needs its own restart procedure.

### 1. Publish the local changes first

Changes must be committed and pushed from the development machine before the VPS
can receive them. Review the intended files, commit them, then push to `origin main`.
Keep `.env`, `Settings.ini`, database dumps and credentials outside Git.

A previous push does not include changes made afterward. Check `git status` locally
and the remote commit before expecting `git pull` on the VPS to update the app.

### 2. Pull on the VPS

Connect using your existing SSH credentials, then:

```bash
cd /home/ubuntu/bajatitian-shms
git status --short
git rev-parse HEAD
git pull --ff-only origin main
git log -1 --oneline
```

Record the previous commit for recovery. If tracked files on the VPS have local
changes, resolve them before pulling. Do not use `git reset --hard` or replace the
existing `.env` with the example file.

### 3. Check the existing configuration

Keep the VPS database credentials, domain, ports, JWT key and MQTT configuration.
Edit the existing file:

```bash
nano .env
```

For the current testing setup:

```env
WORKER_LOCAL_ONLY=true
MAIN_SERVER_UPLOAD_URL=
SERVER_API_BASE_URL=http://api:8080
NEXT_PUBLIC_API_BASE_URL=
```

The current database name is `bajatitian_shms`. Verify the database actually
installed on the VPS and use the same name in `MYSQL_CONNECTION_STRING` and the
MySQL commands below. Do not rename or create a replacement database during an
update merely because an older configuration used a different name.

For MySQL running on the VPS host, the container connection usually uses
`Server=host.docker.internal;Port=3306;...`. The MySQL CLI commands below connect
from the VPS host using `127.0.0.1`; adjust the host/user for your installation.

If the Worker settings file is missing, create it without overwriting an existing
file:

```bash
test -f Backend/Worker/Settings.ini || cp Backend/Worker/Settings.example.ini Backend/Worker/Settings.ini
```

The Compose Worker receives its database connection from `MYSQL_CONNECTION_STRING`.
Redis and external uploads remain disabled with `WORKER_LOCAL_ONLY=true`.

### 4. Build the images, then back up during maintenance

Build before stopping the application, so a build failure does not cause downtime:

```bash
docker compose -f docker-compose.vps.yml --env-file .env --profile worker build api web worker
```

After the build succeeds, stop the application while applying the schema changes.
The obsolete-column migration is incompatible with the old API.

```bash
docker compose -f docker-compose.vps.yml --env-file .env --profile worker stop web api worker
umask 077
backup_dir="$HOME/shms-backups/$(date +%Y%m%d-%H%M%S)"
mkdir -p "$backup_dir"
cp .env "$backup_dir/deployment.env"
cp Backend/Worker/Settings.ini "$backup_dir/Settings.ini"
git rev-parse HEAD > "$backup_dir/deployed-source-commit.txt"
mysqldump -h 127.0.0.1 -P 3306 -u root -p --single-transaction --no-tablespaces bajatitian_shms > "$backup_dir/database.sql"
```

Make sure `mysqldump` succeeded and the dump is nonempty before continuing. The
backup contains private data and credentials; keep it outside the public site and
Git. Keep the pre-pull commit recorded in step 2 as well.

### 5. Apply the database migrations

Database migrations are manual. Pulling/building the application does not apply
them. Complete this step yourself before restarting the updated API and Worker.

From the repository root, open MySQL against the existing database:

```bash
mysql -h 127.0.0.1 -P 3306 -u root -p bajatitian_shms
```

Then run these in the MySQL client, in order:

```sql
SOURCE Backend/Web.API.Persistence/Migrations/20260909_002_mqtt_broker_configs.sql;
SOURCE Backend/Web.API.Persistence/Migrations/20260909_003_mqtt_sensor_topic_slots.sql;
SOURCE Backend/Web.API.Persistence/Migrations/20261015_001_sensor_integrity_indexes.sql;
SOURCE Backend/Web.API.Persistence/Migrations/20261015_002_data_retention.sql;
SOURCE Backend/Web.API.Persistence/Migrations/20261015_003_remove_legacy_system_settings.sql;

SHOW COLUMNS FROM system_settings;
SELECT enabled, reading_retention_days, uploaded_log_retention_days, cleanup_interval_minutes
FROM data_retention_settings;
quit
```

These migrations can be rerun. They add the required indexes, delivery tracking,
retention settings and remove obsolete PLC/production settings. The remaining
`system_settings` columns should be `id`, `backup_db_location`, `backup_schedule`,
`created_at` and `updated_at`.

The retention migration defaults cleanup to disabled on its first installation
and preserves existing policy settings when rerun. Local-only readings remain
unconfirmed and are protected by the current upload-based cleanup rules.

Do not run `Backend/database/bajatitian-shms.sql` or the dummy-data script on an
existing VPS as an update: they contain starter records and can overwrite
configuration. Do not rerun historical PLC migrations after the cleanup migration.
If any migration fails, investigate the error before starting the new version;
MySQL schema changes are not automatically rolled back.

### 6. Start and verify

```bash
docker compose -f docker-compose.vps.yml --env-file .env --profile worker up -d --no-build api web worker
docker compose -f docker-compose.vps.yml --env-file .env --profile worker ps
docker compose -f docker-compose.vps.yml --env-file .env --profile worker logs --tail=100 api web worker
```

Check the website using your existing domain or configured frontend port. Verify
login, the Settings submenus, storage settings and MQTT readings. The Worker log
should contain:

```text
Local-only mode: MQTT messages go directly to MySQL. Redis and server uploads are disabled.
```

Redis is now optional and is excluded unless its `redis` profile is enabled. If
an older SHMS Redis container is still running and is no longer needed, stop that
project's container without deleting its data volume:

```bash
docker compose -f docker-compose.vps.yml --env-file .env --profile redis stop redis
```

The database backup is also needed for a full rollback after dropping legacy
columns; reverting only the code will not restore those columns.

## First-time Docker VPS installation

Only use this section for a new installation, not an existing deployment.

```bash
sudo mkdir -p /home/ubuntu/bajatitian-shms
sudo chown "$USER":"$USER" /home/ubuntu/bajatitian-shms
git clone https://github.com/YudaGuntoro/bajatitian-shms.git /home/ubuntu/bajatitian-shms
cd /home/ubuntu/bajatitian-shms
cp .env.example .env
nano .env
```

Set your real database connection, JWT key, frontend origin/domain and ports.
For MySQL on the VPS host, use `host.docker.internal` in the container connection.
The default frontend host port is `8091`; the API host port is `5274`.
Initialize the new database using the bootstrap instructions in `Backend/README.md`,
then create the Worker settings file and start the selected Compose services.
