# Complete setup guide

This document describes how to run the **Scalable System Design Implementation** project end to end: prerequisites, Docker stack (PostgreSQL master/slave, two API nodes, Nginx), local development without Docker, configuration, verification, and common issues.

---

## What this project does

- **ASP.NET Core** minimal API (`src/ProductApi`) targeting **.NET 10**.
- **POST /products** → writes to **PostgreSQL master** (`WriteDb`).
- **GET /products** → reads from **PostgreSQL slave** (`ReadDb`) replica.
- **Nginx** load balances HTTP across **two identical API containers**; responses include **`processed_by`** (e.g. `Node_1` / `Node_2`) so you can see which node served the request.

Repository layout (important paths):

| Path | Purpose |
|------|---------|
| `src/ProductApi/` | API: `Program.cs`, `Features/` (minimal API endpoints per slice), `Infrastructure/` (persistence, DI), `Domain/` (entities) |
| `docker-compose.yml` | Master/slave Postgres, 2 API services, Nginx |
| `infra/nginx/default.conf` | Upstream to `api-node-1` and `api-node-2` |
| `.env.example` | Template for database passwords (copy to `.env`; real `.env` is gitignored) |
| `Scalable-System-Design-Implementation.sln` | Solution file (used by Dockerfile restore) |

---

## Environment variables and secrets (Docker)

**Do not commit real passwords** in `docker-compose.yml` if the repo is shared. Recommended approach:

1. Copy `.env.example` to `.env` in the repo root.
2. Set `POSTGRESQL_PASSWORD`, replication passwords, and optionally `POSTGRESQL_DATABASE`.
3. Run `docker compose up -d --build` — Compose reads `.env` automatically.

The compose file uses `${VAR:-default}` so you can still run without a `.env` file (defaults match the old tutorial values). **Production or shared environments** should use strong secrets and a proper secret store (Azure Key Vault, AWS Secrets Manager, Docker/Kubernetes secrets, etc.), not only `.env`.

**Note:** `src/ProductApi/appsettings.json` still contains localhost connection strings for non-Docker runs; for production apps, prefer **User Secrets** / environment variables / Key Vault, not checked-in passwords.

---

## Prerequisites

- **.NET SDK 10** (matches `TargetFramework` in `ProductApi.csproj`).
- **Docker Desktop** (or Docker Engine + Compose) for the full stack.
- Optional: **curl** (Windows 10+ often has `curl.exe`), **Postman**, or **pgAdmin** for tests and DB inspection.

---

## Port map (host machine)

When the full Docker stack is running, these ports are used on **localhost**:

| Port | Service |
|------|---------|
| **8080** | Nginx (public API entry: `http://localhost:8080`) |
| **5432** | PostgreSQL **master** (mapped from `postgres-master`) |
| **5433** | PostgreSQL **slave** (mapped from `postgres-slave`; container still uses 5432 internally) |

API containers **do not** publish host ports; they are reached only via Docker network and Nginx.

---

## Full stack with Docker (recommended)

### 1) Clone and open a terminal in the repo root

The root must contain `docker-compose.yml` and `Scalable-System-Design-Implementation.sln` (required by the API `Dockerfile`).

### 2) Free port 5432 if another PostgreSQL is running

This project maps the **master** to host port **5432**. If you already have a local PostgreSQL (or another app) on 5432, either:

- **Stop the other service** (e.g. Windows “PostgreSQL” service), or  
- **Change** the `postgres-master` port mapping in `docker-compose.yml` (e.g. `"5434:5432"`) and use that port in `appsettings.json` / pgAdmin for the master.

If Docker cannot bind 5432, the master container may not start or you may connect to the **wrong** database in tools like pgAdmin.

### 3) Start everything

```bash
docker compose up -d --build
```

First run builds the API image and pulls **Bitnami PostgreSQL** and **Nginx** images.

### 4) Check services

```bash
docker compose ps
```

You should see healthy **products-postgres-master**, **products-postgres-slave**, two **api-node** services, and **products-nginx**.

### 5) Call the API (through Nginx)

**Health**

```bash
curl http://localhost:8080/health
```

**List products** (reads from slave via app)

```bash
curl http://localhost:8080/products
```

**Create product** (writes to master)

- **bash / Git Bash**

```bash
curl -X POST http://localhost:8080/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Keyboard","price":99.99}'
```

- **Windows CMD** (note escaped JSON)

```cmd
curl -X POST "http://localhost:8080/products" -H "Content-Type: application/json" -d "{\"name\":\"Keyboard\",\"price\":99.99}"
```

- **PowerShell** (example)

```powershell
Invoke-RestMethod -Uri "http://localhost:8080/products" -Method Post -ContentType "application/json" -Body '{"name":"Keyboard","price":99.99}'
```

**Load balancer / `processed_by`**

Send **GET /products** several times. `processed_by` may not alternate every request, but over many requests you should see both `Node_1` and `Node_2`.

```powershell
1..20 | ForEach-Object { (Invoke-RestMethod "http://localhost:8080/products").processed_by }
```

### 6) Stop the stack

```bash
docker compose down
```

Remove database volumes as well (wipes data):

```bash
docker compose down -v
```

---

## Configuration reference

### Connection strings (app)

The API reads:

- `ConnectionStrings:WriteDb` — **master** (writes).
- `ConnectionStrings:ReadDb` — **slave** (reads). If `ReadDb` is missing, the app falls back to `WriteDb` (see `Program.cs`).

**Inside Docker** (set in `docker-compose.yml` for `api-node-1` and `api-node-2`):

- Write: `Host=postgres-master;Port=5432;Database=products_db;Username=postgres;Password=postgres`
- Read: `Host=postgres-slave;Port=5432;Database=products_db;Username=postgres;Password=postgres`  
  (hostnames are Docker service names; port **5432** is correct **inside** the network.)

**Local `appsettings.json`** (for `dotnet run` on the host) defaults both to `localhost:5432`. For a true read/write split while DBs run in Docker, point **ReadDb** to the slave port on the host, e.g. `Host=localhost;Port=5433;...`.

### Server identity (`processed_by`)

Resolved in order: config key `ServerId`, then environment variables `SERVER_ID` or `NODE_ID`, else default `Node_A`. Docker sets `ServerId` to `Node_1` / `Node_2` per service.

---

## Run the API on the host (without Docker for the API)

Use this for local development and faster edit-run cycles. Test with **curl**, **Postman**, or the `src/ProductApi/ProductApi.http` file in your IDE.

1) Start only the databases (or full stack) so PostgreSQL is available on the host:

```bash
docker compose up -d postgres-master postgres-slave
```

2) Adjust `src/ProductApi/appsettings.json` if needed:

- **WriteDb**: `Host=localhost;Port=5432;...` (master)
- **ReadDb**: `Host=localhost;Port=5433;...` (slave) for real split, or same as write for a single-DB local test

3) From repo root:

```bash
dotnet restore
dotnet build
dotnet run --project src/ProductApi
```

Profile **ProductApi** listens on **http://localhost:5080** (see `Properties/launchSettings.json`).

---

## Verify PostgreSQL replication

These commands assume the stack from this repo’s `docker-compose.yml` is running.

### 1) Slave is a replica

Password may be required interactively; on Windows you can pass it:

```bash
docker compose exec -e PGPASSWORD=postgres postgres-slave psql -U postgres -d postgres -c "SELECT pg_is_in_recovery();"
```

Expected: `t` (true).

### 2) Write via API, read from slave

Insert a product with **POST /products** through `http://localhost:8080`, then:

```bash
docker compose exec -e PGPASSWORD=postgres postgres-slave psql -U postgres -d products_db -c "SELECT * FROM products;"
```

EF Core created **PascalCase** column names in PostgreSQL (`"Id"`, `"Name"`, `"Price"`, `"CreatedAtUtc"`). Use `SELECT *` or quote identifiers if you filter by column name.

### 3) Optional: insert SQL on master only

Connect to **master** (port **5432**). Example:

```sql
INSERT INTO products ("Name", "Price", "CreatedAtUtc")
VALUES ('Mouse', 25.50, NOW() AT TIME ZONE 'UTC');
```

Do **not** expect arbitrary INSERTs on the **slave**; it is read-only in replication mode.

---

## pgAdmin (or DBeaver) connection

Register **two** servers using **localhost** (pgAdmin running **on Windows**, not inside a container):

| Server | Host | Port | Database (initial) | Username | Password |
|--------|------|------|--------------------|----------|----------|
| Master | `127.0.0.1` or `localhost` | **5432** | `postgres` | `postgres` | `postgres` |
| Slave | `127.0.0.1` or `localhost` | **5433** | `postgres` | `postgres` | `postgres` |

- SSL mode: **Disable** (typical for local Docker).
- Application data is in database **`products_db`**.

If pgAdmin runs **inside Docker**, use **`host.docker.internal`** instead of `localhost` for the host, with the same ports.

**“Password authentication failed” on 5432** usually means another PostgreSQL instance is bound to 5432 (wrong server) or an old saved password. Confirm with `docker compose ps` and stop the conflicting local service or remap the master port.

---

## Troubleshooting

| Symptom | What to check |
|---------|----------------|
| Cannot bind port 5432 | Another Postgres or process using 5432; stop it or change compose mapping. |
| API returns 500 / DB errors | `docker compose logs api-node-1 api-node-2 postgres-master postgres-slave` |
| Only one `processed_by` value | Normal for few requests; send more GETs; ensure both API containers are **Up**. |
| Replica lag | Under load, slave may be slightly behind; retry SELECT after a short wait. |

---

## Quick reference: environment variables (Docker API)

| Variable | Purpose |
|----------|---------|
| `ConnectionStrings__WriteDb` | Npgsql connection to **master** |
| `ConnectionStrings__ReadDb` | Npgsql connection to **slave** |
| `ServerId` | Value returned as `processed_by` metadata |
| `ASPNETCORE_ENVIRONMENT` | Standard ASP.NET Core environment (e.g. `Development` for local `dotnet run`) |

---
