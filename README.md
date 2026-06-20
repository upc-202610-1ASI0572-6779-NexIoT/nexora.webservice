# Nexora Web API

This is the core WebService backend for the Nexora IoT platform. It is built using **ASP.NET Core** and structured following **Domain-Driven Design (DDD)** principles to ensure scalability, maintainability, and clean separation of concerns.

## Tech Stack
* **Framework:** .NET 8.0 / ASP.NET Core
* **Database:** PostgreSQL (via Entity Framework Core)
* **Documentation:** Swagger UI (OpenAPI)

## Architecture Overview
The solution is divided into four main layers:
* `Domain`: Core business logic, aggregates, value objects, and repository interfaces. Agnostic to external frameworks.
* `Application`: Use cases, application services, and orchestration logic.
* `Infrastructure`: Data persistence (PostgreSQL), external clients, and technical implementations.
* `Presentation`: REST API Controllers, Middlewares, and web endpoints.

## Getting Started
1. Ensure you have the latest .NET SDK installed.
2. Clone the repository and navigate to the `backend/` directory.
3. Run `dotnet restore` to install dependencies.
4. Run `dotnet build` to compile the solution.
