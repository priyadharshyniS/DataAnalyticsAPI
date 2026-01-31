# DataAnalyticsApi (C#)

This project is a minimal .NET 7 Web API that loads sales CSV data into SQLite, supports a daily refresh background job, and exposes revenue analysis endpoints.

Quick start

1. Install .NET 7 SDK: https://dotnet.microsoft.com/download
2. From the `src` folder run:

```bash
dotnet restore
dotnet run
```

3. Open Swagger UI at `http://localhost:5000/swagger` (port may vary).

API endpoints

- `POST /api/revenue/refresh?overwrite=false` — trigger CSV load on-demand.
- `GET /api/revenue/total?start=2024-01-01&end=2024-03-01` — total revenue for date range.
- `GET /api/revenue/by_product` — revenue grouped by product id.
- `GET /api/revenue/by_category` — revenue grouped by product category.
- `GET /api/revenue/by_region` — revenue grouped by region.

Revenue calculation assumption

- Net revenue per order is calculated as: `(Quantity Sold * Unit Price * (1 - Discount)) + Shipping Cost`.
- Discount is expected as a fraction (e.g., `0.1` for 10%).

If you prefer a different formula (exclude shipping, or treat discount as absolute), tell me and I will update the logic.

Web UI

- A small static UI is available at the server root (e.g., `http://localhost:5000/`). It is served from `wwwroot/index.html` and can trigger the refresh and run queries without using curl or Swagger.

Docker demo (local, reproducible)

Run the full demo locally with Docker Compose (Postgres + app):

```bash
# from repository root
docker compose up --build
```

This starts Postgres (user `postgres` / password `devpassword`) and the API at `http://localhost:5000`.

Then trigger load and verify:

```bash
curl -X POST "http://localhost:5000/api/revenue/refresh?overwrite=true"
curl "http://localhost:5000/api/revenue/debug/orders/count"
curl "http://localhost:5000/api/revenue/total?start=2023-01-01&end=2026-01-31"
```

The compose file mounts `src/data` into the container so the sample CSV is available to the loader.

Using a hosted Postgres (optional)

You can switch from SQLite to a hosted Postgres (recommended for production or to avoid SQLite aggregation limitations).

1. Create a hosted Postgres (e.g., Supabase, ElephantSQL, Neon) and get the Postgres connection string.
2. Update `appsettings.json` `ConnectionStrings:Default` with the Postgres connection string (format: `Host=...;Database=...;Username=...;Password=...;`).
3. The app now auto-detects Postgres if the connection string contains `Host=`. It uses the Npgsql EF Core provider.
4. Install EF tools and run migrations locally if you want controlled schema changes:

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add Init --project src --startup-project src
dotnet ef database update --project src --startup-project src
```

If you prefer not to run migrations, the app will call `EnsureCreated()` on startup which can create the schema automatically (suitable for development only).

Configuration

Edit `appsettings.json` to change `ConnectionStrings:Default` and `Csv:Path`.
