# Project requirements

Specification for the scalable backend infrastructure assignment: load balancing, replication, and a minimal REST API.

---

## 1. Project overview

The objective is to build a **functional, scalable backend infrastructure** from scratch, with emphasis on **how traffic is distributed** and **how data stays consistent across multiple nodes**—not only on application code.

You must set up a **load-balanced environment** with a **replicated database architecture**.

---

## 2. System architecture

The implementation **must** include these main components.

### Load balancer (Nginx or HAProxy)

- Acts as the **single entry point** for HTTP traffic.
- Must distribute incoming requests to backend nodes using **Round Robin** or **Least Connections**.

### Application layer (two API nodes)

- **Two identical instances** of a REST API (technology choice is open: Node.js, Python, Go, .NET, etc.).
- Runs on **separate ports or containers** (isolated processes).

### Database layer (master–slave replication)

| Role | Responsibility |
|------|----------------|
| **Master** | Handles **writes** and may handle reads (`INSERT`, `UPDATE`, `DELETE`; reads as specified by your design). |
| **Slave (read replica)** | **Synchronizes** data from the master and serves **read** operations (e.g. `GET`) per assignment rules. |

---

## 3. Functional requirements (API)

API behavior is intentionally simple so effort focuses on **infrastructure**.

### `POST /products`

- **Action:** Validate and persist product data (**Name**, **Price**) to the **master** database.
- **Response:** Success indication and the **created** resource (or equivalent success payload).

### `GET /products`

- **Action:** Load the list of products from the **slave** database.
- **Response:** Product list plus **server metadata** (e.g. `"processed_by": "Node_A"`) so you can **prove the load balancer** is hitting different backends.

---

## 4. Technical implementation phases

Document and record work for **each** phase below.

### Phase 1 — Database replication

1. Configure the **master** node.
2. Configure the **slave** node.
3. **Verify synchronization:** insert on the master and confirm data appears on the slave.

### Phase 2 — API and read/write split

1. Implement the API logic (`POST` / `GET` as above).
2. **Critical:** Use **two connection strings** — one for **writes** (master address) and one for **reads** (slave address).

### Phase 3 — Infrastructure and load balancing

1. Run **two** API instances.
2. Configure the load balancer to **proxy** to both API nodes.
3. **Optional but recommended:** health checks on backends.

### Phase 4 — Verification and stress test

1. Use **curl**, **Postman**, or similar to send **many** requests.
2. Confirm the **server ID** (or equivalent metadata) **varies** across responses (e.g. Node 1 vs Node 2).
3. Confirm data **written on the master** is **visible when reading via the slave** (replication path).

