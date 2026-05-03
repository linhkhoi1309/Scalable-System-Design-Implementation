# Key configuration snippets

Reference for **Nginx**, **PostgreSQL (Docker / Bitnami)**, and the **API’s database connection and read/write split**. Values match the current repo; adjust via `.env` or `appsettings` as needed.

---

## Nginx (load balancer)

**File:** `infra/nginx/default.conf`

Upstream pools **two** API containers on the Docker network (`api-node-1`, `api-node-2`). Client traffic hits **Nginx** on port **80** inside the container; Compose maps **8080 → 80** on the host.

See `infra/nginx/default.conf` for the full file. Summary:

| Setting | Purpose |
|---------|---------|
| `upstream` | Multiple `server` entries → default **round-robin** across backends. |
| `max_fails` / `fail_timeout` | Passive health: after failures, peer is skipped briefly so traffic converges on the live node faster during chaos tests. |
| `proxy_connect_timeout` | Short connect timeout (e.g. **2s**) so dead backends fail fast instead of stalling. |
| `proxy_next_upstream` | On **error**, **timeout**, **502/503/504**, try another peer in the same request where applicable. **GET/HEAD** retry by default; **POST** does not (avoids duplicate writes) unless `proxy_next_upstream non_idempotent on;` is added. |
| `proxy_next_upstream_tries` | Cap attempts (e.g. **2**) so each client request does not fan out forever. |
| `proxy_pass http://product_api_backend` | Forwards all paths (`/health`, `/products`, …) to the pool. |
| `X-Forwarded-*` | Preserves original host/client info for the API (logging, future features). |

Compose mounts this file read-only:

```yaml
# docker-compose.yml (nginx service excerpt)
volumes:
  - ./infra/nginx/default.conf:/etc/nginx/conf.d/default.conf:ro
ports:
  - "8080:80"
```

---

## Database (PostgreSQL master / slave)

### Docker Compose — Bitnami replication

**File:** `docker-compose.yml`

Master exposes **5432** on the host; slave exposes **5433 → 5432**. Credentials and DB name can be overridden via **`.env`** (see `.env.example`).

**Master (primary)**

```yaml
postgres-master:
  environment:
    POSTGRESQL_PASSWORD: ${POSTGRESQL_PASSWORD:-postgres}
    POSTGRESQL_DATABASE: ${POSTGRESQL_DATABASE:-products_db}
    POSTGRESQL_REPLICATION_MODE: master
    POSTGRESQL_REPLICATION_USER: ${POSTGRESQL_REPLICATION_USER:-replicator}
    POSTGRESQL_REPLICATION_PASSWORD: ${POSTGRESQL_REPLICATION_PASSWORD:-replicator_pass}
  ports:
    - "5432:5432"
```

**Slave (standby)**

```yaml
postgres-slave:
  environment:
    POSTGRESQL_PASSWORD: ${POSTGRESQL_PASSWORD:-postgres}
    POSTGRESQL_REPLICATION_MODE: slave
    POSTGRESQL_MASTER_HOST: postgres-master
    POSTGRESQL_MASTER_PORT_NUMBER: 5432
    POSTGRESQL_REPLICATION_USER: ${POSTGRESQL_REPLICATION_USER:-replicator}
    POSTGRESQL_REPLICATION_PASSWORD: ${POSTGRESQL_REPLICATION_PASSWORD:-replicator_pass}
  ports:
    - "5433:5432"
```

| Variable | Role |
|----------|------|
| `POSTGRESQL_PASSWORD` | Password for user **`postgres`** (matches API connection strings). |
| `POSTGRESQL_DATABASE` | Application database name (`products_db` by default). |
| `POSTGRESQL_REPLICATION_*` | Replication user/password used between master and slave. |

### Optional `.env` (repo root, gitignored)

**File:** `.env.example` → copy to `.env`

```dotenv
POSTGRESQL_PASSWORD=postgres
POSTGRESQL_REPLICATION_USER=replicator
POSTGRESQL_REPLICATION_PASSWORD=replicator_pass
POSTGRESQL_DATABASE=products_db
```

Compose substitutes `${POSTGRESQL_*:-…}` when resolving `docker-compose.yml`; API connection strings use the same variables so passwords stay aligned.

---

## API — database connection logic

### 1) Configuration keys

ASP.NET Core reads:

| Key | Usage |
|-----|--------|
| `ConnectionStrings:WriteDb` | **Npgsql** → **master** (writes). |
| `ConnectionStrings:ReadDb` | **Npgsql** → **slave** (reads). If missing, falls back to **WriteDb**. |

**Local defaults** (`src/ProductApi/appsettings.json`):

```json
{
  "ConnectionStrings": {
    "WriteDb": "Host=localhost;Port=5432;Database=products_db;Username=postgres;Password=postgres",
    "ReadDb": "Host=localhost;Port=5432;Database=products_db;Username=postgres;Password=postgres"
  },
  "ServerId": "Node_A"
}
```

**Docker** overrides via environment (same keys, colon → double underscore):

```yaml
# docker-compose.yml — api-node-1 / api-node-2
ConnectionStrings__WriteDb: Host=postgres-master;Port=5432;Database=${POSTGRESQL_DATABASE:-products_db};Username=postgres;Password=${POSTGRESQL_PASSWORD:-postgres}
ConnectionStrings__ReadDb: Host=postgres-slave;Port=5432;Database=${POSTGRESQL_DATABASE:-products_db};Username=postgres;Password=${POSTGRESQL_PASSWORD:-postgres}
```

Inside Docker, **always use port `5432`** in the connection string: that is the **container** port; service names (`postgres-master`, `postgres-slave`) resolve on the Compose network.

**Host-only `dotnet run`** with databases in Docker: point **WriteDb** at `localhost:5432` (master) and **ReadDb** at `localhost:5433` (slave) so reads hit the replica.

### 2) Registration — two DbContexts, one pair of strings

**File:** `Infrastructure/ServiceCollectionExtensions.cs`

```csharp
var writeConnectionString = configuration.GetConnectionString("WriteDb")
    ?? throw new InvalidOperationException("Connection string 'WriteDb' was not found.");
var readConnectionString = configuration.GetConnectionString("ReadDb")
    ?? writeConnectionString;

services.AddDbContext<WriteProductDbContext>(options => options.UseNpgsql(writeConnectionString));
services.AddDbContext<ReadProductDbContext>(options => options.UseNpgsql(readConnectionString));
```

- **`WriteProductDbContext`** and **`ReadProductDbContext`** share the same EF model; only the **connection string** differs.
- Fallback to **`writeConnectionString`** for reads keeps single-database local setups working.

### 3) Startup — schema creation

**File:** `Infrastructure/WebApplicationExtensions.cs`

- **`EnsureCreatedAsync`** runs on the **write** context first.
- If read and write connection strings **differ**, **`EnsureCreatedAsync`** also runs on the **read** context (for local/dev scenarios; on a true replica, schema normally comes from replication, not a second `EnsureCreated` on the standby).

### 4) Endpoints — inject write vs read

**File:** `Features/Products/ProductEndpoints.cs`

- **`POST /products`** → method parameter **`WriteProductDbContext`** → writes go to **master**.
- **`GET /products`** → **`ReadProductDbContext`** → reads go to **slave** (when configured).

Server metadata for load-balancer demos uses **`IConfiguration`** (`ServerId`, or env `SERVER_ID` / `NODE_ID`), not the DB layer.

---

## Quick matrix

| Layer | Config surface | Effect |
|-------|----------------|--------|
| Nginx | `infra/nginx/default.conf` | Balance HTTP across `api-node-1` and `api-node-2`. |
| Postgres | `docker-compose.yml` + `.env` | Master/slave roles, ports **5432** / **5433** on host. |
| API | `appsettings.json` or env vars | **WriteDb** / **ReadDb** Npgsql URLs; **ServerId** for `processed_by`. |

See also `docs/setup.md` for runbooks and pgAdmin ports.
