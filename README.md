# ShopGlobal

> **⚠️ WARNING: This application is intentionally poorly designed. It is NOT meant for production use.**

This is a deliberately flawed .NET 8 web API backed by Azure Cosmos DB. It exists as a learning tool and demonstration of common anti-patterns, bad practices, and performance pitfalls in cloud application development.

**Do not use this code as a reference for building real applications.**

## What's in here

- ASP.NET Core Web API with controllers for carts, customers, inventory, and products
- Azure Cosmos DB (NoSQL) data layer
- Static frontend (HTML/CSS/JS)

## Anti-patterns included (by design)

- New `CosmosClient` created on every request (transient registration)
- Naive retry logic with no backoff
- Poor partition key strategy
- Gateway connection mode
- No dependency injection best practices
- No error handling or logging strategy
- Hardcoded configuration values
