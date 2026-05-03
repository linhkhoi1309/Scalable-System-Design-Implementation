"1. Project Overview
The objective of this project is to build a functional, scalable backend infrastructure from scratch. You will move beyond simple application development to focus on how traffic is distributed and how data remains consistent across multiple nodes. You are required to set up a load-balanced environment with a replicated database architecture.

2. System Architecture Requirements
Your implementation must consist of the following main components:

Load Balancer (Nginx or HAProxy): Acts as the single entry point. It must distribute incoming traffic to the backend nodes using a Round Robin or Least Connections algorithm.

Application Layer (2x API Nodes): Two identical instances of a REST API (Node.js, Python, Go, etc.) running on separate ports or containers.

Database Layer (Master-Slave Replication):

Master Node: Handles all Write / Read operations (INSERT, UPDATE, DELETE, GET).

Slave Node (Read Replica): Synchronizes data from the Master and handles Read operations (GET).

3. Functional Requirements (The API)
The API logic is intentionally kept simple to focus on the infrastructure setup. It must include:

POST /products:

Action: Validates and saves product data (Name, Price) into the Master Database.

Response: Success message and the data created.

GET /products:

Action: Fetches the list of products from the Slave Database.

Response: List of products + Server Metadata (e.g., "processed_by": "Node_A"). This is crucial to prove the Load Balancer is working.

4. Technical Implementation Steps (Step-by-Step)
You must document and record each of the following phases:

Phase 1: Database Replication Setup
Configure the Master node.

Configure the Slave node.

Verify synchronization by inserting data into Master and checking the Slave.

Phase 2: API Development & Read/Write Splitting
Implement the API logic.

Critical: Configure the app to use two different connection strings: one for Writes (Master IP) and one for Reads (Slave IP).

Phase 3: Infrastructure & Load Balancing
Deploy two instances of the API.

Configure the Load Balancer to proxy requests to both API nodes.

Ensure the Load Balancer performs health checks (optional but recommended).

Phase 4: Verification & Stress Test
Use curl or Postman to send multiple requests.

Verify that the "Server ID" in the response toggles between Node 1 and Node 2.

Verify that data written to the Master is visible when querying the Slave.