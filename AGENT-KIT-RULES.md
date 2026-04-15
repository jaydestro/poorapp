# Cosmos DB Agent Kit Rules → ShopGlobal Anti-Patterns

This document maps each intentional anti-pattern in ShopGlobal to the [Azure Cosmos DB Agent Kit](https://github.com/AzureCosmosDB/cosmosdb-agent-kit) best-practice rule that would fix it.

**Agent Kit Rule Reference:** [cosmosdb-best-practices SKILL.md](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/SKILL.md)

---

## SDK Best Practices (`sdk-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 1 | New `CosmosClient` on every request (`CosmosService.cs`) | [`sdk-singleton-client`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/sdk-singleton-client.md) | Register `CosmosClient` as a singleton. One client per app lifetime, reuse TCP connections. |
| 2 | `ConnectionMode.Gateway` hardcoded | [`sdk-connection-mode`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/sdk-connection-mode.md) | Use `Direct` mode for production — lower latency, better throughput. |
| 3 | `DangerousAcceptAnyServerCertificateValidator` not gated to dev | [`sdk-emulator-ssl`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/sdk-emulator-ssl.md) | Only disable cert validation when talking to the local emulator, gated by environment. |
| 4 | Retry 10x with no delay on any exception | [`sdk-retry-429`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/sdk-retry-429.md) | Respect `RetryAfter` headers on 429s, use exponential backoff, don't retry non-transient errors. |
| 5 | No diagnostics or logging on failures | [`sdk-diagnostics`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/sdk-diagnostics.md) | Log `CosmosDiagnostics` on failures and high-latency requests for troubleshooting. |
| 6 | Read-modify-write without ETags (cart checkout, inventory) | [`sdk-etag-concurrency`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/sdk-etag-concurrency.md) | Use ETags for optimistic concurrency to prevent race conditions and lost updates. |
| 7 | `CosmosService` not `IDisposable`, clients leak | [`sdk-singleton-client`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/sdk-singleton-client.md) | Singleton client eliminates the leak; DI container handles disposal. |
| 8 | Using `Newtonsoft.Json` instead of `System.Text.Json` | [`sdk-newtonsoft-dependency`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/sdk-newtonsoft-dependency.md) | If using Newtonsoft, explicitly reference it and configure the serializer; prefer STJ for modern SDK. |

---

## Data Modeling (`model-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 9 | Full `Order` list embedded in `Customer` (unbounded growth) | [`model-reference-large`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/model-reference-large.md) | Reference orders by ID instead of embedding — use separate documents for 1:many relationships that grow unboundedly. |
| 10 | Full `Customer` snapshot embedded in every `Order` | [`model-reference-large`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/model-reference-large.md) | Store a customer ID reference, not a full snapshot. Hydrate at read time if needed. |
| 11 | Full `Product` (with reviews!) embedded in cart/order line items | [`model-reference-large`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/model-reference-large.md) | Reference product by ID + store only the fields needed (name, price). |
| 12 | Unbounded `AuditLog` in inventory documents | [`model-avoid-2mb-limit`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/model-avoid-2mb-limit.md) | Bound embedded arrays or move audit logs to separate documents. |
| 13 | Unbounded `PurchaseHistory` in recommendation documents | [`model-avoid-2mb-limit`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/model-avoid-2mb-limit.md) | Cap embedded arrays; store purchase history separately. |
| 14 | All reviews embedded in product document | [`model-avoid-2mb-limit`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/model-avoid-2mb-limit.md) | Move reviews to separate documents or cap embedded count. |
| 15 | Per-region inventory duplicated inside product | [`model-denormalize-reads`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/model-denormalize-reads.md) | If denormalizing for reads, use Change Feed to keep copies in sync — don't embed stale data. |
| 16 | `GetCustomerAsync` mutates the document (read has write side effect) | [`model-relationship-references`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/model-relationship-references.md) | Use ID references with transient hydration — assemble the view at read time without writing back. |
| 17 | No type discriminator strategy | [`model-type-discriminator`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/model-type-discriminator.md) | Use a `type` field to distinguish document types in a shared container. |

---

## Partition Key Design (`partition-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 18 | Partition key `/type` creates hot partitions | [`partition-high-cardinality`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/partition-high-cardinality.md) | Choose a key with high cardinality (e.g., `/id`, `/customerId`) so data distributes evenly. |
| 19 | All products in one partition, all orders in another | [`partition-avoid-hotspots`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/partition-avoid-hotspots.md) | Distribute writes evenly across many logical partitions. |
| 20 | Cross-entity queries require cross-partition scans | [`partition-query-patterns`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/partition-query-patterns.md) | Align partition key with query access patterns. |
| 21 | No hierarchical partition key | [`partition-hierarchical`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/partition-hierarchical.md) | Use hierarchical keys (e.g., `/type` → `/categoryId` → `/id`) for multi-tenant or polymorphic containers. |

---

## Query Optimization (`query-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 22 | Query instead of point read for `GetProductAsync` | [`query-point-reads`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-point-reads.md) | Use `ReadItemAsync` when `id` + partition key are known — 1 RU vs. 3-10x for a query. |
| 23 | Query instead of point read for `GetCustomerAsync` | [`query-point-reads`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-point-reads.md) | Same fix. |
| 24 | Query instead of point read for `GetInventoryAsync` | [`query-point-reads`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-point-reads.md) | Same fix. |
| 25 | Query instead of point read for cart lookup | [`query-point-reads`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-point-reads.md) | Same fix. |
| 26 | `CONTAINS(LOWER(...))` for product search | [`query-avoid-scans`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-avoid-scans.md) | Avoid full scans. Use Full-Text Search (`FullTextContains`) or indexed fields. |
| 27 | `CONTAINS(LOWER(...))` for product search (FTS fix) | [`fts-contains-query`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/AGENTS.md#124-use-fulltextcontains-for-keyword-matching) | Replace `CONTAINS(LOWER(...))` with `FullTextContains` for indexed keyword matching. |
| 28 | `SELECT *` everywhere | [`query-use-projections`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-use-projections.md) | Project only needed fields to reduce RU cost and bandwidth. |
| 29 | Client-side aggregation (loads all orders, GroupBy in C#) | [`query-olap-detection`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-olap-detection.md) | Detect analytical queries and use server-side `GROUP BY`, `SUM`, `COUNT`, or redirect to an analytical store. |
| 30 | No pagination — drains all query pages | [`query-pagination`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-pagination.md) | Use continuation tokens and `MaxItemCount` for pagination. |
| 31 | Loads ALL recommendations, filters in memory | [`query-avoid-cross-partition`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-avoid-cross-partition.md) | Add `WHERE` clauses to filter server-side; avoid cross-partition scans. |
| 32 | No parameterized queries | [`query-parameterize`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/query-parameterize.md) | Use `QueryDefinition` with `.WithParameter()` for safety and plan caching. |

---

## Throughput & Scaling (`throughput-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 33 | Manual throughput at 400 RU/s | [`throughput-autoscale`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/throughput-autoscale.md) | Use autoscale for variable workloads — scales between 10% and 100% of max. |
| 34 | No consideration for serverless | [`throughput-serverless`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/throughput-serverless.md) | Consider serverless capacity mode for dev/test and low-traffic workloads. |
| 35 | Not right-sized for workload | [`throughput-right-size`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/throughput-right-size.md) | Analyze actual RU consumption to right-size provisioned throughput. |

---

## Indexing (`index-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 36 | Default indexing policy (indexes everything) | [`index-exclude-unused`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/index-exclude-unused.md) | Exclude paths never queried (e.g., embedded reviews, audit logs) to reduce write RU cost. |

---

## Full-Text Search (`fts-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 37 | `CONTAINS(LOWER(...))` forces full scan | [`fts-enable-capability`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/AGENTS.md#121-enable-full-text-search-capability-on-account) | Enable `EnableNoSQLFullTextSearch` on the account. |
| 38 | No full-text policy on container | [`fts-full-text-policy`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/AGENTS.md#122-define-full-text-policy-on-the-container) | Define a `fullTextPolicy` with language code on the container. |
| 39 | No full-text index | [`fts-index-policy`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/AGENTS.md#123-add-full-text-index-in-the-indexing-policy) | Add `fullTextIndexes` to the indexing policy. |
| 40 | Using `CONTAINS` instead of `FullTextContains` | [`fts-contains-query`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/AGENTS.md#124-use-fulltextcontains-for-keyword-matching) | Use `FullTextContains` / `FullTextContainsAll` for keyword matching. |

---

## Design Patterns (`pattern-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 41 | Stale denormalized data (inventory in product, customer in order) | [`pattern-change-feed-materialized-views`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/pattern-change-feed-materialized-views.md) | Use Change Feed to keep materialized views and denormalized copies in sync. |
| 42 | `GetCustomerAsync` manually hydrates orders into customer doc | [`pattern-service-layer-relationships`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/pattern-service-layer-relationships.md) | Use a service layer to hydrate references at read time without mutating stored documents. |
| 43 | Full recommendation rebuild on every product view (O(n²)) | [`pattern-change-feed-materialized-views`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/pattern-change-feed-materialized-views.md) | Pre-compute recommendations via Change Feed when orders are created, not on every read. |
| 44 | Client-side analytics (loads all orders, aggregates in C#) | [`pattern-efficient-ranking`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/pattern-efficient-ranking.md) | Use pre-computed aggregates or server-side queries for analytics. |

---

## Monitoring & Diagnostics (`monitoring-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 45 | No RU tracking anywhere | [`monitoring-ru-consumption`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/monitoring-ru-consumption.md) | Track `RequestCharge` from every response to understand cost. |
| 46 | No latency monitoring | [`monitoring-latency`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/monitoring-latency.md) | Monitor P99 latency to catch performance regressions. |
| 47 | Would throttle at 400 RU/s with no alerts | [`monitoring-throttling`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/monitoring-throttling.md) | Alert on 429 throttling to catch capacity issues. |
| 48 | No diagnostic logging | [`monitoring-diagnostic-logs`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/monitoring-diagnostic-logs.md) | Enable diagnostic logging for operational visibility. |

---

## Developer Tooling (`tooling-*`)

| # | App Problem | Rule | What It Fixes |
|---|-------------|------|---------------|
| 49 | No local dev/emulator configuration strategy | [`tooling-emulator-setup`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/tooling-emulator-setup.md) | Use the Cosmos DB Emulator properly for local development. |
| 50 | No VS Code tooling for data inspection | [`tooling-vscode-extension`](https://github.com/AzureCosmosDB/cosmosdb-agent-kit/blob/main/skills/cosmosdb-best-practices/rules/tooling-vscode-extension.md) | Use the VS Code extension for routine data inspection and management. |

---

## Summary by Impact

| Priority | Category | Rules Applied | Problems Fixed |
|----------|----------|--------------|----------------|
| CRITICAL | Data Modeling | 9 rules | Unbounded documents, 2MB limit risks, stale snapshots, mutating reads |
| CRITICAL | Partition Key Design | 4 rules | Hot partitions, cross-partition scans, poor write distribution |
| HIGH | Query Optimization | 11 rules | Point reads, full scans, client-side aggregation, no pagination |
| HIGH | SDK Best Practices | 8 rules | Singleton client, connection mode, retries, ETags, diagnostics |
| HIGH | Full-Text Search | 4 rules | `CONTAINS(LOWER(...))` replaced with indexed FTS |
| HIGH | Design Patterns | 4 rules | Change Feed for materialized views, service-layer hydration |
| MEDIUM-HIGH | Indexing | 1 rule | Exclude unused paths from index |
| MEDIUM | Throughput | 3 rules | Autoscale, serverless, right-sizing |
| LOW-MEDIUM | Monitoring | 4 rules | RU tracking, latency, throttling alerts, diagnostic logs |
| MEDIUM | Developer Tooling | 2 rules | Emulator setup, VS Code extension |
