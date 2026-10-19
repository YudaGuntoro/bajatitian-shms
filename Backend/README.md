# SHMS-System Backend

Layered .NET 8 backend for the PT. Baja Titian Utama SHMS-System system.

## Local testing without Redis or an upload server

The Worker defaults to **local-only mode**. MQTT reception and normalized MySQL
persistence stay enabled. Redis clients, HTTP uploaders and remote sync writers
are not registered or constructed in this mode, and queue replay is a no-op.
No Redis installation or main/Witon server is required for local testing.

```ini
[Worker]
LocalOnly=true
```

`WORKER_LOCAL_ONLY=true` (or `Worker__LocalOnly=true`) overrides `Settings.ini`.
Missing configuration defaults to local-only; invalid values stop startup instead
of enabling uploads. To restore the existing Redis/upload flow later, configure
the dependencies and set `WORKER_LOCAL_ONLY=false`, then restart the Worker.

Local messages are recorded with raw-log status `ignored` (intentionally excluded
from forwarding) and their sensor readings keep `uploaded_at=NULL`. They are not
marked as uploaded or counted as server failures. Existing FIFO rules protect
these unconfirmed readings and logs from automatic deletion. Local-mode data is
not automatically sent later when remote mode is enabled.

In Docker Compose, Redis is now an optional `redis` profile. Local testing uses
`docker compose -f docker-compose.vps.yml --profile worker up -d` without Redis.
When remote services are ready, set `WORKER_LOCAL_ONLY=false` and also enable
`--profile redis` if Redis should run in Compose. MQTT and MySQL are still required
to receive and store live sensor data. The header's connectivity indicator is
labeled `Local API`, since an API response does not indicate Witon availability.

## Projects

- `Web.API`: ASP.NET Core Web API.
- `Web.API.Domain`: authentication, response, and SHMS domain models.
- `Web.API.Persistence`: EF Core context, auth service, and SHMS database mappings.

## Database

Use the on-premise MySQL database:

```text
Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=bajatitian_shms;SslMode=None;AllowPublicKeyRetrieval=True;
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
$env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=bajatitian_shms;SslMode=None;AllowPublicKeyRetrieval=True;"
dotnet run --project Web.API\Web.API.csproj --urls http://localhost:5241
```

## Worker persistence and indexes

Run SQL commands below from the repository root. New installations use
`Backend/database/bajatitian-shms.sql` for the schema and initial site/asset records.
For existing installations, select the database and apply this repeatable migration:

```powershell
mysql -u root -p bajatitian_shms -e "source Backend/Web.API.Persistence/Migrations/20261015_001_sensor_integrity_indexes.sql"
mysql -u root -p bajatitian_shms -e "source Backend/Web.API.Persistence/Migrations/20261015_002_data_retention.sql"
mysql -u root -p bajatitian_shms -e "source Backend/Web.API.Persistence/Migrations/20261015_003_remove_legacy_system_settings.sql"
```

The migration preserves data, fills in missing foreign keys and indexes, and skips
equivalent indexes even if they have another name. Duplicate identities or orphaned
references cause an error: correct that data and rerun instead of disabling foreign
key checks. Back up the database before deployment; MySQL DDL commits implicitly.

All devices share the same normalized tables:

- `measurement_units`: one row per category/symbol.
- `sensor_types`: one row per sensor family, such as TILT or VW.
- `sensor_devices`: one row per device code, such as tilt01 or tilt02.
- `sensor_channels`: one row per device/channel, with its own unit and measurement.
- `sensor_readings`: timestamped values referencing a channel by foreign key.

No table is created per sensor. The type's default unit is only a default: the unit
for an individual reading is obtained through its channel. Ingestion preserves
existing device names, maintenance states, channel configuration, and default units.
Conflicting sensor types or channel units/measurements/axes are rejected instead of
silently changing the interpretation of historical values. Use a separate channel
for a different measurement definition.
New devices use the existing default site and asset (ID 1); initialize these records
with the bootstrap script before receiving readings.

The reading and raw-log writers validate the schema once per service instance;
the reading writer also requires the normalized foreign keys and unique identity indexes.
They do not create/alter tables or seed site/asset records during ingestion. Schema
changes must be deployed before starting the Worker. All channels in one MQTT
payload commit in one transaction; a failure rolls back both readings and any new
master records. Retried readings reuse their existing IDs.

Relevant indexes:

| Table | Index columns | Purpose |
| --- | --- | --- |
| `sensor_devices` | unique `device_code` | Resolve device identity |
| `sensor_channels` | unique `(sensor_device_id, channel_code)` | Resolve channel identity |
| `sensor_readings` | unique `(sensor_channel_id, measured_at)` | Deduplicate retries and query channel history |
| `sensor_readings` | `measured_at` | Query across sensors by time |
| `log_buffer` | `time_stamp` | Latest activity, with primary key as pagination tie breaker |
| `log_buffer` | `(status, time_stamp)` | Logs by delivery status/time |
| `log_buffer` | `(device_id, time_stamp)` | Logs by external device code/time |
| `log_buffer` | `(sensor_device_id, time_stamp)` | Logs by normalized device ID/time |

The Worker accepts `ConnectionStrings__DefaultConnection`, with `Settings.ini` as
fallback. To run it from the repository root:

```powershell
$env:ConnectionStrings__DefaultConnection="Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=bajatitian_shms;SslMode=None;AllowPublicKeyRetrieval=True;"
dotnet run --project Backend/Worker/Worker.csproj
```

MySQL integration checks create a temporary database with a generated name and
drop it after execution. They cover the legacy-schema upgrade, migration repeatability,
normalized identities, retry deduplication, master-data preservation, atomic rollback,
foreign-key enforcement, log/device identity, and index use through `EXPLAIN`:

```powershell
$env:SHMS_TEST_CONNECTION_STRING=$env:ConnectionStrings__DefaultConnection
dotnet run --project Backend/Worker.IntegrationTests/Worker.IntegrationTests.csproj
```

## FIFO retention settings

Setting now has separate pages for display, server/upload, storage, backup, alerts,
and system information. `/settings` redirects to `/settings/display`.
`/settings/storage` reads and writes the database policy through authenticated
`/api/shms-system/storage/settings`, `/status`, and `/preview` endpoints.
Preview is read-only; saving a policy never directly deletes data.

The new `data_retention_settings` singleton stores enabled state, reading retention
(1–3650 days), successfully uploaded log retention (1–3650 days), cleanup interval
(1–1440 minutes), and an optional reading count cap (100–100,000,000).
`data_retention_status` records the most recent attempt/result and whether more
cleanup work remains. No tables are created per sensor. The migration defaults
cleanup to **disabled**; users enable it after reviewing the estimated impact.

The Worker checks for changed policies every 30 seconds. Due cleanup deletes at
most 1000 readings and 1000 uploaded logs per transaction/batch, with at most 10
batches per pass. Large backlogs continue on the next Worker tick without waiting
another full interval. A database-scoped advisory lock prevents simultaneous runs
by multiple Worker instances. The storage page shows counts, pending logs, oldest
data, the last cleanup result, and the next planned check. The Worker must be running
for automatic cleanup to execute; saving in the browser alone does not run cleanup.

Reading retention uses immutable first `ingested_at`, ordered by `(ingested_at,id)`.
Retried readings keep their original ingestion time. The router passes an upload
acknowledgement only after the main server reports success; the reading writer
persists `uploaded_at` in the same transaction as the values. Failed/unconfirmed
corrections revoke the old acknowledgement when their contents change. Legacy
readings without an acknowledgement remain protected; their delivery status is
never inferred or backfilled from log text.

Cleanup only removes readings with confirmed delivery and no referencing alarm
events. Pending/failed raw logs and all Redis queue entries are excluded. Uploaded
log expiry uses `uploaded_at`, so a recently uploaded old message gets its full
log retention period. Device/channel/unit master data are never deleted.
The optional count cap can evict confirmed data earlier than its retention period;
it never overrides delivery or alarm protection, so protected data can exceed the
configured cap. The UI reports this condition.

The FIFO scan uses `sensor_readings(ingested_at,id,uploaded_at)`; uploaded-log
cleanup uses `log_buffer(status,uploaded_at)`. Existing foreign keys and history
indexes remain in place. Existing data is preserved by the additive migration.
Integration checks also cover policy validation, migration repeatability, expiry,
FIFO capacity, protected data, immutable ingestion time, scheduling, corrections,
multi-Worker locking, and large-backlog continuation.

InnoDB can reuse space freed by deletion; database files do not necessarily shrink
on disk immediately. The count cap limits rows, not physical bytes.

## Last sensor readings

The authenticated `GET /api/shms-system/sensors/latest` endpoint returns the newest
stored measurement for each device/channel, including value, unit, measurement
time and last received time. It reads normalized sensor history directly, using
the existing `(sensor_channel_id, measured_at)` unique index and the
`log_buffer(device_id,time_stamp)` index. No schema migration is required.

The dashboard displays these values even when the sensor is offline, independently
of its 50 most recent activity rows. Later arrival of an older historical sample
does not replace a newer measurement. Values survive browser reloads and raw log
cleanup while their sensor history is retained. The existing FIFO policy still
controls history retention; no additional permanent copy is created. A sensor with
no retained measurements shows `No data`; an initial API failure shows an
unavailable message and refresh failures preserve values already displayed.

## Legacy system settings cleanup

Migration `20261015_003_remove_legacy_system_settings.sql` removes the obsolete
`plc_ip_address`, `pressure_unit_id`, and `cycle_time_unit_id` columns and their
foreign keys from `system_settings`. The migration can be rerun and discovers
foreign key names from database metadata. Back up before applying it; dropped
column values cannot be recovered without a backup.

The remaining columns are `id`, `backup_db_location`, `backup_schedule`,
`created_at`, and `updated_at`. Backup values/timestamps, measurement-unit master
records, normalized sensor relationships, and sensor readings are preserved.
The API and browser no longer read, submit, or seed the obsolete fields. Browser
settings from older versions are filtered to current keys when loaded.
Historical migrations are retained for upgrade history; run migrations in order
and do not rerun old production migrations against the current schema alone.
