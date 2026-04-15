# ShopGlobal

> **⚠️ WARNING: This application is intentionally poorly designed. It is NOT meant for production use.**

This is a deliberately flawed .NET 8 web API backed by Azure Cosmos DB. It exists as a learning tool to be improved using the [Azure Cosmos DB Agent Kit](https://github.com/AzureCosmosDB/cosmosdb-agent-kit). The app is packed with real-world anti-patterns across Cosmos DB usage, data modeling, security, and frontend design — your goal is to find and fix them.

**Do not use this code as a reference for building real applications.**

## What's in here

- ASP.NET Core Web API with controllers for carts, customers, inventory, and products
- Azure Cosmos DB (NoSQL) data layer
- Static frontend (HTML/CSS/JS)

---

## Intentional Anti-Patterns (58 total)

### Cosmos DB Connection & Client Management

| # | File | Issue |
|---|------|-------|
| 1 | `CosmosService.cs` | **New `CosmosClient` created on every call** — `CosmosClient` is designed to be a singleton. Creating one per request wastes TCP connections, skips connection pooling, and adds hundreds of ms of latency. |
| 2 | `Program.cs` | **`CosmosService` registered as `Transient`** — combined with #1, every injected service creates a new client. A single HTTP request can spin up 3-4 clients. |
| 3 | `Program.cs` | **All services registered as `Transient`** — `CartService`, `ProductService`, etc. should be `Scoped` or `Singleton`. |
| 4 | `CosmosService.cs` | **`ConnectionMode.Gateway` hardcoded** — adds latency vs. `Direct` mode (the default and recommended mode). |
| 5 | `CosmosService.cs` | **`DangerousAcceptAnyServerCertificateValidator` not gated to dev** — disables TLS cert validation and would ship to production. |
| 6 | `CosmosService.cs` | **`GetContainer()` creates a client but never disposes it** — leaks a `CosmosClient` on every call. |

### Partition Key Design & Data Modeling

| # | File | Issue |
|---|------|-------|
| 7 | `CosmosService.cs` | **Single container with partition key `/type`** — all products share one partition, all orders another. Creates massive hot partitions with no horizontal scaling within an entity type. |
| 8 | `CosmosService.cs` | **Manual throughput at 400 RU/s** — the absolute minimum. Any real workload will throttle immediately. No autoscale. |
| 9 | All Models | **Single container for all entity types** — products, orders, customers, carts, inventory, and recommendations in one container with `/type` partition key makes cross-entity queries into cross-partition scans. |

### Query Anti-Patterns

| # | File | Issue |
|---|------|-------|
| 10 | `ProductService.cs` | **Query instead of point read for `GetProductAsync`** — `id` + partition key are known; should use `ReadItemAsync` (1 RU) instead of a query (3-10x more). |
| 11 | `CustomerService.cs` | **Query instead of point read for `GetCustomerAsync`** — same problem. |
| 12 | `InventoryService.cs` | **Query instead of point read for `GetInventoryAsync`** — same problem. |
| 13 | `ProductService.cs` | **`CONTAINS(LOWER(...))` for search** — forces a full partition scan on every search. Cannot use indexes. |
| 14 | `ProductService.cs` | **`SELECT *` everywhere** — returns entire documents including embedded reviews, inventory, and recommendation links. Wastes RUs and bandwidth. |
| 15 | `AdminController.cs` | **Client-side aggregation for analytics** — loads ALL orders into memory then does `GroupBy` in C#. Should use server-side aggregation. |
| 16 | `AdminController.cs` | **Revenue analytics: same `SELECT *` + client-side aggregation** |
| 17 | `AdminController.cs` | **Active customers: loads ALL orders with no time filter** — unbounded full-partition scan. |
| 18 | `RecommendationService.cs` | **Loads ALL recommendation documents, filters in memory** — should filter by `sourceProductId` in the query. |
| 19 | `CartService.cs` | **Cart lookup by query instead of point read** |
| 20 | All Services | **No pagination on any query** — every query drains all pages. No `MaxItemCount`, no continuation tokens. |

### Document Size & Unbounded Growth

| # | File | Issue |
|---|------|-------|
| 21 | `Customer.cs` | **Full `Order` list embedded in `Customer` document** — grows without bound. Cosmos DB has a 2 MB document limit. |
| 22 | `CustomerService.cs` | **`GetCustomerAsync` loads all orders, embeds them, then `ReplaceItemAsync`** — every *read* triggers a *write*. A GET that mutates data. |
| 23 | `Order.cs` | **Full `Customer` snapshot (including payment methods, addresses) embedded in every order** — bloats orders, data becomes stale immediately. |
| 24 | `Order.cs` / `Cart.cs` | **Full `Product` snapshot with all reviews embedded in each line item** — a product with 50 reviews in 3 line items = massive document. |
| 25 | `Inventory.cs` | **Unbounded `AuditLog` list in inventory document** — every stock change appends an entry, never truncated. |
| 26 | `Recommendation.cs` | **Full `PurchaseHistory` list embedded in recommendation documents** — grows without bound. |
| 27 | `Product.cs` | **All reviews embedded in product document** — popular products will hit the 2 MB limit. |
| 28 | `Product.cs` | **Per-region inventory counts embedded in product** — duplicates data from Inventory, immediately stale. |

### Concurrency & Data Integrity

| # | File | Issue |
|---|------|-------|
| 29 | `CartService.cs` | **Read-modify-write on inventory without ETags** — concurrent checkouts cause race conditions and overselling. |
| 30 | `InventoryService.cs` | **`AdjustStockAsync` — same read-modify-write without ETags** — manual adjustments race with checkouts. |
| 31 | `CartService.cs` | **Order creation and inventory decrement are not transactional** — if inventory update fails, the order still exists. |
| 32 | `CartService.cs` | **Exceptions swallowed during inventory updates** — `catch { /* swallow */ }`. Stock could fail to decrement silently. |

### Retry Logic & Error Handling

| # | File | Issue |
|---|------|-------|
| 33 | `CosmosService.cs` | **Retries 10 times with zero delay on ANY exception** — no backoff, no `RetryAfter` header respect, retries non-transient errors (400, 404). Amplifies load during throttling. |
| 34 | `CosmosService.cs` | **Empty catch block — no logging** — failures are completely invisible. |
| 35 | `ProductController.cs` | **`RebuildRecommendationsAsync` failure silently swallowed** |
| 36 | All Controllers | **No global exception handling** — unhandled exceptions return 500 with potentially sensitive stack traces. |

### Performance & Scalability

| # | File | Issue |
|---|------|-------|
| 37 | `ProductController.cs` | **`RebuildRecommendationsAsync()` triggered on EVERY product detail view** — an O(n²) operation on every page view. |
| 38 | `RecommendationService.cs` | **Full recommendation rebuild: loads all orders, all products, deletes and recreates all recs** — unbounded memory usage, hundreds of individual writes. |
| 39 | `RecommendationService.cs` | **Deletes old recommendations one at a time** — each delete is a separate round-trip. No batch. |
| 40 | `RecommendationService.cs` | **Creates new recommendations one at a time** — no bulk operations. |
| 41 | `AdminController.cs` | **Seed inserts every document one at a time** — 121 sequential individual writes. Should use bulk or transactional batch. |
| 42 | `CartService.cs` | **AddItem fetches entire product (with all reviews) just for a price** — should project only `price` and `name`. |
| 43 | `CartService.cs` | **Checkout appends full order (with product snapshots) to customer document** — replaces the entire bloated customer document. |

### Security Issues

| # | File | Issue |
|---|------|-------|
| 44 | `appsettings.json` | **Cosmos DB key stored in plain text in config** — should use Key Vault, managed identity, or user-secrets. |
| 45 | `CosmosService.cs` | **TLS certificate validation disabled unconditionally** — not gated by environment. |
| 46 | `AdminController.cs` | **No authentication/authorization on admin endpoints** — anyone can hit `POST /api/admin/seed` and wipe the database. |
| 47 | All Controllers | **No input validation** — no `[Required]` attributes, no model validation, no sanitization. |
| 48 | `CustomerController.cs` | **Email search with no rate limiting** — could enumerate all customer emails. |
| 49 | `Customer.cs` | **Payment method details stored alongside general customer data** — PCI-sensitive data with no access controls. |

### Frontend Issues (`app.js`)

| # | Issue |
|---|-------|
| 50 | **Home page fetches 5 categories SEQUENTIALLY** — each `await` blocks the next. Should use `Promise.all()`. |
| 51 | **For each product on home page, fetches inventory individually and sequentially** — up to 40 additional sequential API calls. |
| 52 | **Category page fetches inventory for every product sequentially** |
| 53 | **Search fires on every keystroke with no debounce** — typing "headphones" sends 10 queries, each also fetching inventory per result. |
| 54 | **Product detail page fetches each recommended product individually** — each GET triggers a full recommendation rebuild on the server. |
| 55 | **Cart view re-fetches product AND inventory for each cart item** — redundant since the cart already has product snapshots. |
| 56 | **Cart polling every 2 seconds** — indefinite polling wastes RUs and bandwidth. |
| 57 | **Account page makes 3 redundant API calls for the same customer** |
| 58 | **Admin "Rebuild Recommendations" works by visiting a product page** — relies on the side effect that viewing a product triggers a full rebuild. |

### Architectural Issues

| # | File | Issue |
|---|------|-------|
| — | All Services | **No interface abstractions** — concrete classes injected directly, no testability. |
| — | `CosmosService.cs` | **`DefaultTimeToLive = -1`** — carts, stale recs, and abandoned data accumulate forever. |
| — | `CustomerService.cs` | **Read operation has write side effect** — `GetCustomerAsync` mutates the customer document. |
| — | All Models | **Using `Newtonsoft.Json` instead of `System.Text.Json`** — the modern Cosmos SDK uses STJ by default. |
| — | `CosmosService.cs` | **`CosmosService` is not `IDisposable`** — clients leak since there's no disposal mechanism. |
| — | All Services | **No caching at any layer** — every request hits Cosmos DB. |
| — | `Program.cs` | **`UseAuthorization()` without authentication middleware** — meaningless. |
