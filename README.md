# Scalable System Design Implementation

This repository contains a .NET REST API backed by PostgreSQL with master-slave replication, running behind an Nginx load balancer with two API nodes.

## What is included

- `POST /products` writes a product to PostgreSQL master using the write connection string.
- `GET /products` reads products from PostgreSQL slave (read replica) using the read connection string.
- `processed_by` is returned in responses so later API-node and load-balancer testing can prove which instance handled the request.
- `docker-compose.yml` starts PostgreSQL master + slave, two API containers, and an Nginx reverse proxy.

## Project layout

- `src/ProductApi` - ASP.NET Core minimal API (`Features/`, `Infrastructure/`, `Domain/`)
- `docker-compose.yml` - Full local infrastructure (master/replica DB, two API nodes, Nginx)

## Run locally

Optional: copy `.env.example` to `.env` and set database passwords (see `docs/setup.md`). Compose uses safe defaults if `.env` is missing.

1. Start the full stack:
	 ```bash
	 docker compose up -d
	 ```
2. Open the app through Nginx at `http://localhost:8080`.
3. If you want to run the API locally without Docker, use:
	```bash
	dotnet run --project src/ProductApi
	```
	The local profile runs on `http://localhost:5080`.

## API contract

- `POST /products`
	- Body:
		```json
		{
			"name": "Keyboard",
			"price": 99.99
		}
		```
	- Response includes the created product and `processed_by`.

- `GET /products`
	- Response includes the list of products and `processed_by`.

## Load balancer test

Send repeated `GET /products` requests to `http://localhost:8080/products` and watch the `processed_by` value alternate between `Node_1` and `Node_2`.

## PostgreSQL configuration

The app reads two connection strings:

- `ConnectionStrings:WriteDb`
- `ConnectionStrings:ReadDb`

In Docker Compose:
- `WriteDb` points to `postgres-master:5432`
- `ReadDb` points to `postgres-slave:5432`

## Replication verification

1. Start the stack:
	```bash
	docker compose up -d --build
	```

2. Confirm slave is in recovery mode (`true` means replica):
	```bash
	docker compose exec postgres-slave psql -U postgres -d postgres -c "SELECT pg_is_in_recovery();"
	```

3. Insert data through API (writes to master):
	```bash
	curl -X POST http://localhost:8080/products \
	  -H "Content-Type: application/json" \
	  -d '{"name":"Mouse","price":25.50}'
	```

4. Verify data appears on slave:
	```bash
	docker compose exec postgres-slave psql -U postgres -d products_db -c "SELECT id, name, price FROM products ORDER BY id DESC LIMIT 5;"
	```

5. Verify API read path and load balancing:
	```bash
	curl http://localhost:8080/products
	```
	Response includes product list and `processed_by` metadata.