# Video demonstration checklist

**Target duration:** 5–10 minutes

Use this list while recording. Check items as you go.

---

## Before you start

- [ ] Stack is up: `docker compose up -d --build` (or already running and healthy).
- [ ] `docker compose ps` — all services **Up** (nginx, `api-node-1`, `api-node-2`, `postgres-master`, `postgres-slave`).
- [ ] Know your **service names** for chaos: `scalable-system-design-implementation-api-node-1-1` (or `api-node-1` via compose) — use `docker compose ps` to copy exact names.

---

## Part 1 — Walkthrough (code & configuration) — ~2–3 min

Briefly show **where** each concern lives. Do not read every line; point at files and state the one-line purpose.

- [ ] **`docker-compose.yml`** — Postgres master + slave, two API services, Nginx; mention **`.env`** / **`.env.example`** for secrets.
- [ ] **`infra/nginx/default.conf`** — single entry, **upstream** to both API containers, load balancing + **failover** (timeouts / `proxy_next_upstream`).
- [ ] **`src/ProductApi/Program.cs`** (or `Features/…`) — app startup, **two connection strings** registered.
- [ ] **`src/ProductApi/Features/Products/ProductEndpoints.cs`** — `POST` uses **write** context → master; `GET` uses **read** context → slave; **`processed_by`** from `ServerId`.
- [ ] **`src/ProductApi/Infrastructure/Persistence/ProductDbContext.cs`** (or path you use) — `WriteProductDbContext` / `ReadProductDbContext`.
- [ ] (Optional) **`docs/design.md`** — one architecture diagram if the rubric wants a **big picture**.

**One sentence to say:** *“HTTP hits Nginx once; the API writes to the primary DB and reads from the replica; Nginx spreads traffic across two identical API containers.”*

---

## Part 2 — Live demo

### 2.1 `POST` saves to the **master** — ~1–2 min

- [ ] State goal: *new row is created on the **primary** (master).*
- [ ] Send **`POST /products`** through Nginx (so path matches production setup):

  ```bash
  curl -X POST "http://localhost:8080/products" -H "Content-Type: application/json" -d "{\"name\":\"Demo Product\",\"price\":99.99}"
  ```

  (PowerShell: `Invoke-RestMethod -Uri "http://localhost:8080/products" -Method Post -ContentType "application/json" -Body '{"name":"Demo Product","price":99.99}'`.)

- [ ] Show response: success + **`processed_by`** (e.g. `Node_1` or `Node_2`).
- [ ] On **master** DB, show the row (pgAdmin on `localhost:5432`, database `products_db`, or `SELECT` in tool of choice).

---

### 2.2 `GET` requests **balanced** across two nodes — ~1–2 min

- [ ] State goal: *repeated **GET**s return different **`processed_by`** values over time (load balancer).*
- [ ] Run **many** `GET`s through **port 8080** (Nginx), not the API port directly:

  ```bash
  curl -s http://localhost:8080/products
  ```

  Repeat 15–30 times, or in PowerShell:

  ```powershell
  1..25 | ForEach-Object { (Invoke-RestMethod "http://localhost:8080/products").processed_by }
  ```

- [ ] Point out **`Node_1`** / **`Node_2`** (or your configured IDs) appearing in the output.

---

## Part 3 — “Chaos” test (one API node stopped) — ~2–3 min

- [ ] State goal: *with **one** replica down, **GET** still succeeds via Nginx using the **surviving** node.*
- [ ] Confirm both nodes healthy **before** chaos (`docker compose ps`).
- [ ] Stop **one** API container only (example — adjust name from `docker compose ps`):

  ```bash
  docker compose stop api-node-1
  ```

- [ ] Immediately send several **`GET`**s (and optionally **`GET /health`**) to **`http://localhost:8080`**:

  ```bash
  curl -s http://localhost:8080/health
  curl -s http://localhost:8080/products
  ```

- [ ] Show **200 OK** and JSON still returning (may briefly glitch right when the container dies — acceptable to narrate).
- [ ] Note: **`processed_by`** may show only **one** node ID while one replica is down — that **proves** only the surviving node answers (still valid “still operational”).
- [ ] Bring the node back:

  ```bash
  docker compose start api-node-1
  ```

- [ ] Optional: repeat **`GET`**s and show **`processed_by`** varying again after both are up.

---

## Closing (~30 s)

- [ ] Summarize: **replicated DB**, **read/write split**, **two API replicas**, **Nginx**, **chaos resilience**.
- [ ] Point to **`docs/setup.md`** / **`docs/design.md`** for reviewers.

---

## Quick reference (URLs & ports)

| Item | Value |
|------|--------|
| Public API (via LB) | `http://localhost:8080` |
| Health | `GET http://localhost:8080/health` |
| Products | `POST` / `GET http://localhost:8080/products` |
| Postgres master (host) | `localhost:5432` |
| Postgres slave (host) | `localhost:5433` |

---

## Timing sanity check

| Section | Rough budget |
|---------|----------------|
| Walkthrough | 2–3 min |
| POST + GET balance | 2–4 min |
| Chaos + recovery | 2–3 min |
| Closing | < 1 min |

**Total:** ~6–10 minutes — adjust depth to fit 5–10 min cap.
