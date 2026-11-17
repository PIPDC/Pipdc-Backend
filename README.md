# PIPDC Real Estate Platform — Backend API

ASP.NET Core 9 Web API for a full-stack real-estate platform: property listings, agents, enquiries, real-time messaging, development-project tracking, a blog, and an AI property concierge.

> **Portfolio demo project.** This is a personal portfolio project built by Simon Agboji with sample data. It is **not affiliated with, endorsed by, or built for the real PIPDC** or any government body.

**Live demo:** [pipdc-frontend.onrender.com](https://pipdc-frontend.onrender.com) · **Frontend repo:** [PIPDC/Pipdc-Frontend](https://github.com/PIPDC/Pipdc-Frontend)

---

## Contents

- [Features](#features)
- [Tech stack](#tech-stack)
- [Architecture](#architecture)
- [API overview](#api-overview)
- [Getting started](#getting-started)
- [Configuration](#configuration)
- [Testing](#testing)
- [Deployment](#deployment)
- [Further documentation](#further-documentation)

## Features

- **Listings** — properties for sale and rent with images, locations, filtering, saved properties, and nearby search.
- **Roles** — `User`, `Agent`, and `Admin`, enforced with ASP.NET Core Identity and JWT bearer authentication (access + refresh tokens).
- **Agents** — agent applications with admin review, appeals, public agent profiles, reviews, and user reports.
- **Enquiries & messaging** — enquiries on listings, plus real-time one-to-one conversations over **SignalR**, with escalation to admins.
- **AI property concierge** — chat assistant that searches the live catalogue and hands off to the team when it cannot find an exact match. Uses a pluggable provider (**OpenRouter** or **Google Gemini**).
- **Development projects** — public project pages with units, progress updates, and user tracking/notifications.
- **Transactions** — sale and lease records, including automated rent-expiry reminder emails.
- **Blog** — posts, categories, and tags managed by admins.
- **Email** — queued transactional email through the Gmail API, sent by a background worker.
- **Hardening** — per-policy rate limiting, idempotency keys on writes, Cloudflare Turnstile support, security headers (HSTS, CSP), global exception handling, and health checks.

## Tech stack

| Area | Technology |
|---|---|
| Runtime | .NET 9, ASP.NET Core Web API, C# |
| Data | PostgreSQL, Entity Framework Core 9 (Npgsql) |
| Auth | ASP.NET Core Identity, JWT bearer, refresh tokens |
| Real-time | SignalR |
| AI | OpenRouter or Google Gemini (configurable) |
| Media | Cloudinary |
| Email | Gmail API (MimeKit) |
| API docs | OpenAPI + Scalar, URL-segment API versioning (`/api/v1`) |
| Testing | xUnit, NetArchTest.Rules |
| Hosting | Docker (multi-stage image), Render |

## Architecture

Clean-architecture layering inside **one application project** (`src/PIPDC.csproj`). The layers are folders with their own namespaces, and the dependency direction is enforced by architecture tests rather than project references.

```
src/
├── Domain/          Entities, enums, Result/Error — references nothing
├── Application/     Services, interfaces, DTOs, business rules
├── Infrastructure/  EF Core, auth, email, AI clients, rate limiting, health checks
├── API/             Controllers, SignalR hub, exception handling
└── Program.cs       Composition root
tests/
└── PIPDC.ArchitectureTests/   Layer-dependency rules + unit tests
```

```
API ──► Application ──► Domain
 └────► Infrastructure ──┘
```

See [ARCHITECTURE.md](ARCHITECTURE.md) and [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) for the full walkthrough.

## API overview

All routes are versioned under `/api/v1`.

| Area | Base route |
|---|---|
| Auth | `/api/v1/auth` |
| Properties / saved | `/api/v1/properties`, `/api/v1/saved-properties` |
| Locations | `/api/v1/locations` |
| Agents | `/api/v1/agents`, `/agent-applications`, `/agent-reviews`, `/agent-reports` |
| Enquiries | `/api/v1/enquiries` |
| Conversations & messages | `/api/v1/conversations`, `/conversations/{id}/messages` |
| AI concierge | `/api/v1/ai-chat`, `/api/v1/concierge-escalations` |
| Development projects | `/api/v1/development-projects`, `/development-tracking` |
| Transactions | `/api/v1/transactions` |
| Blog | `/api/v1/blog`, `/blog/categories`, `/blog/tags` |
| Dashboard / users | `/api/v1/dashboard`, `/api/v1/users` |
| Contact / images | `/api/v1/contact`, `/api/v1/images` |

Other endpoints:

- **SignalR hub:** `/hubs/messaging`
- **Health checks:** `/healthz/live` and `/healthz/ready`
- **Interactive API reference (Development only):** `/scalar/v1`
- **OpenAPI document (Development only):** `/openapi/v1.json`

## Getting started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- PostgreSQL 14+ (local install or Docker)

### 1. Clone and restore

```bash
git clone https://github.com/PIPDC/Pipdc-Backend.git
cd Pipdc-Backend
dotnet restore
dotnet tool restore        # installs the pinned dotnet-ef
```

### 2. Start PostgreSQL (Docker example)

```bash
docker run -d --name pipdc-db -p 5432:5432 \
  -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=pipdc \
  postgres:16
```

### 3. Set the required secrets

Secrets are kept out of source control with .NET user secrets:

```bash
cd src
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=pipdc;Username=postgres;Password=postgres"
dotnet user-secrets set "JwtSettings:Key" "<a random string of at least 32 characters>"
dotnet user-secrets set "SeedAdminPassword" "<password for the seeded admin account>"
```

### 4. Run

```bash
dotnet run --project src --launch-profile https
```

| Profile | URLs |
|---|---|
| `https` | `https://localhost:7123` and `http://localhost:5123` |
| `http` | `http://localhost:5123` |

In Development the API automatically applies migrations, seeds roles, and seeds sample data. Open `https://localhost:7123/scalar/v1` to explore the endpoints.

## Configuration

Settings live in `src/appsettings.json` and can be overridden with user secrets or environment variables (for example, `Cors__AllowedOrigins__0`).

| Key | Purpose | Needed for |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection | **Startup** |
| `JwtSettings:Key` / `Issuer` / `Audience` | Token signing and validation | **Startup** |
| `Cors:AllowedOrigins` | Allowed frontend origins (Development: `http://localhost:5173`) | **Startup** |
| `SeedAdminPassword` | Password for the seeded admin account | Development seeding |
| `Frontend:BaseUrl` | Absolute frontend URL used in email links | Required in Production |
| `GmailApiSettings:*` | Gmail API credentials for outgoing email | Email delivery |
| `Cloudinary:*` | Image uploads | Uploads |
| `AiChat:Provider` | `OpenRouter` (default) or `Gemini` | AI concierge |
| `OpenRouter:*` / `Gemini:*` | AI provider API key and model | AI concierge |
| `Turnstile:*` | Cloudflare Turnstile bot protection (disabled by default) | Optional |

> Never commit real keys. Use `dotnet user-secrets` locally and environment variables in production.

## Testing

```bash
dotnet test
```

The `PIPDC.ArchitectureTests` project uses **NetArchTest** to check that Domain, Application, Infrastructure, and API only depend in the allowed direction. It also includes unit tests for email link generation.

## Deployment

The included multi-stage `Dockerfile` builds a self-contained runtime image. It listens on `$PORT` (defaulting to `8080`), which suits Render and similar hosts.

```bash
docker build -t pipdc-api .
docker run -p 8080:8080 --env-file .env pipdc-api
```

In **Production**, migrations are **not** applied on startup. Apply them deliberately:

```bash
dotnet ef database update --project src
```

## Further documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) — how the layers, DbContext, and services fit together
- [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) — folder-by-folder audit
- [PIPDC.http](PIPDC.http) — sample requests

## Author

**Simon Agboji** — Software Engineer (C# · ASP.NET Core)
[Portfolio](https://simon-a.netlify.app) · [GitHub](https://github.com/agbojisimon) · [LinkedIn](https://www.linkedin.com/in/agboji-simon)
