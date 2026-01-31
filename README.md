Concise, production-minded .NET project that ingests a sales CSV, normalizes data into a relational schema, supports scheduled and on-demand refresh, and exposes REST endpoints for revenue analysis (total, by product, category, region).
Highlights
- Normalized schema: `products`, `customers`, `orders` (relational, FK enforced).
- CSV loader: upserts products/customers first (two-phase) then orders; validation and decimal-aggregation fallbacks included.
- Scheduling: background service for daily refresh; on-demand refresh API available.
- Demoability: `docker compose up --build` spins up Postgres + app with sample CSV mounted.
- Focus on code quality: meaningful names, single try/catch per method, clear error handling, debug endpoints, switchable DB providers (SQLite / Postgres).
Quick demo (one-command)
1. Build and start local demo (Docker required):

```bash
# from repository root
docker compose up --build
```
2. Trigger refresh (loads `src/data/sales_sample.csv`):

```bash
curl -X POST "http://localhost:5000/api/ValidateRevenue/refresh?overwrite=true"
```
3. Verify results:

```bash
curl "http://localhost:5000/api/ValidateRevenue/debug/orders/count"
curl "http://localhost:5000/api/ValidateRevenue/total?start=2023-01-01&end=2026-01-31"
```

Run locally (without Docker)
1. Ensure .NET 7 SDK installed.
2. From `src` folder:

```powershell
cd src
dotnet restore
dotnet run
# DataAnalyticsAPI — Sales ETL & Revenue API

A compact, production-minded .NET 7 Web API that:

- Loads and normalizes sales CSV data into a relational schema (`products`, `customers`, `orders`).
- Provides scheduled (daily) and on-demand data refresh.
- Exposes endpoints for revenue analysis: total, by product, category, and region.
- Includes a Docker Compose demo (Postgres + app) for a one-command local demo.

Why this project

- Designed for reliability: two-phase CSV loader inserts principals first then orders inside a transaction.
- Defensive APIs: input validation, single try/catch per endpoint, and clear HTTP status codes.
- Demo-ready: Docker Compose and a simple web UI for quick walkthroughs.

Quickstart — recommended (Docker)

1. From repository root run:

```bash
docker compose up --build
```

2. Trigger a CSV load (uses `src/data/sales_sample.csv`):

```bash
curl -X POST "http://localhost:5000/api/ValidateRevenue/refresh?overwrite=true"
```

3. Verify:

```bash
curl "http://localhost:5000/api/ValidateRevenue/debug/orders/count"
curl "http://localhost:5000/api/ValidateRevenue/total?start=2023-01-01&end=2026-01-31"
```

Quickstart — local (no Docker)

1. Install .NET 7 SDK.
2. From `src`:

```powershell
cd src
dotnet restore
dotnet run
# then trigger refresh
curl -X POST "http://localhost:5000/api/ValidateRevenue/refresh?overwrite=true"
```

Configuration

- Default DB provider is selected by `ConnectionStrings:Default` in `src/appsettings.json` or `ConnectionStrings__Default` env var.
- For Supabase/Postgres include SSL parameters: `SSL Mode=Require;Trust Server Certificate=true`.

API reference (key endpoints)

- POST `/api/ValidateRevenue/refresh?overwrite={true|false}` — trigger CSV load
- GET `/api/ValidateRevenue/total?start=YYYY-MM-DD&end=YYYY-MM-DD` — total revenue
- GET `/api/ValidateRevenue/by_product` — revenue grouped by product
- GET `/api/ValidateRevenue/by_category` — revenue grouped by category
- GET `/api/ValidateRevenue/by_region` — revenue grouped by region
- Debug: `/api/ValidateRevenue/debug/*` — counts & samples

CSV expectations

The loader supports common header variants; sample CSV headers (recommended):

```
Order ID, Product ID, Customer ID, Product Name, Category, Region, Date of Sale, Quantity Sold, Unit Price, Discount, Shipping Cost, Payment Method, Customer Name, Customer Email, Customer Address
```

Validation & robustness

- Two-phase loader: upserts `products` and `customers` first, then `orders`.
- Provider-aware aggregation: falls back to client-side grouping when SQLite cannot translate decimal SUM expressions. Postgres supports server-side decimal aggregates.
- Single try/catch per controller method with clear logging and appropriate HTTP status codes.
