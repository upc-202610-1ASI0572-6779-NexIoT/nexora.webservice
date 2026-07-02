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
2. Run `dotnet restore` to install dependencies.
3. Run `dotnet build` to compile the solution.
4. Run the API project: `dotnet run --project src/host/Nexora.WebApi/Nexora.WebApi.csproj`

## Stripe Integration Configuration

The subscription management system integrates with Stripe for checkout payment flows and event webhooks.

### 1. API Keys Configuration
Update your configuration files with your Stripe Test Keys:

*   **Backend (`src/host/Nexora.WebApi/appsettings.json`):**
    ```json
    "Stripe": {
      "SecretKey": "sk_test_...",
      "PublishableKey": "pk_test_...",
      "WebhookSecret": "whsec_..."
    }
    ```
*   **Frontend Webapp (`.env` or env files):**
    Ensure `VITE_STRIPE_PUBLISHABLE_KEY` is set to your publishable key:
    ```env
    VITE_STRIPE_PUBLISHABLE_KEY=pk_test_...
    ```

### 2. Local Webhook Tunnel Setup (Stripe CLI)
To test webhooks (like checkout completion, invoices payment, and subscription status updates) locally, you must route webhook events from Stripe to your local server using the Stripe CLI:

1.  **Install the Stripe CLI:**
    *   **macOS (Homebrew):** `brew install stripe/stripe-cli/stripe`
    *   **Windows (Scoop):** `scoop bucket add stripe https://github.com/stripe/stripe-cli.git && scoop install stripe`
2.  **Log in to your Stripe Account:**
    ```bash
    stripe login
    ```
3.  **Start the Local Forwarding Tunnel:**
    Run the following command to listen to events and forward them to your local API webhook controller:
    ```bash
    stripe listen --forward-to http://localhost:5001/api/v1/payments/webhook
    ```
4.  **Configure the Webhook Signing Secret:**
    *   The command above will output your local webhook signing secret in the terminal console (e.g. `whsec_...`).
    *   Copy this string and paste it as the value of `Stripe:WebhookSecret` in your backend's `appsettings.json` file.
    *   Restart your dotnet backend server to apply the new configuration.
