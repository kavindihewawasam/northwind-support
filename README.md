# Northwind Support Desk

A support ticket management system for a small support team: raise tickets, search and filter
them, assign them to agents, and move them through their lifecycle.

Monorepo: **.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, sharing one build,
one test command and one set of conventions.

> **Taking this as an interview assignment?** Your brief is in
> **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**. Read it once you have the app running.

---

## Quick start

### Prerequisites

| Tool | Version |
| --- | --- |
| .NET SDK | 10.0 (pinned in `global.json`) |
| Node.js | 22 LTS recommended (`.nvmrc`); 20.19+ works |
| Docker | any recent version (for SQL Server — or bring your own instance) |
| Git | any recent version |

### Four commands

```bash
cp .env.example .env          # optional — every value has a working default
npm install                   # installs the web workspace (apps/web)
npm run db:up                 # SQL Server 2022 in Docker, on localhost:1433
npm run api                   # terminal 1 — migrates, seeds, then serves the API
npm run web                   # terminal 2 — the React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger UI | https://localhost:7043/swagger |
| SQL Server | `localhost:1433` (container `supportdesk-sql`) |

In Development the API applies migrations and seeds demo data on startup, so there is nothing
else to run. Prefer your own SQL Server? Point `ConnectionStrings__Default` at it in `.env`, or
edit `apps/api/src/SupportDesk.Presentation/appsettings.Development.json`. **Never commit real credentials.**

### Running from Visual Studio

Visual Studio 2026 (the first version that supports .NET 10), with the **ASP.NET and web development**
workload.

1. **Start SQL Server**: `npm run db:up` (Docker must be running).
2. **Open** `SupportDesk.sln` from the repository root. Solution Explorer shows a `src` folder
   with the four layer projects and a `tests` folder.
3. **Set the startup project**: right-click **SupportDesk.Presentation** →
   **Set as Startup Project**.
4. **Run**: pick the **https** profile in the toolbar and press **F5**. The API migrates and
   seeds the database, then opens Swagger at https://localhost:7043/swagger.
5. **Start the web app** from a terminal. It is an npm workspace, not part of the solution:
   `npm install` (first time only), then `npm run web`, and open http://localhost:5173.

Run the backend tests from **Test → Test Explorer → Run All**.

> Opened this solution before the projects were restructured? If Visual Studio still points at an
> old startup project or shows stale projects, close it, delete the hidden `.vs` folder in the
> repository root, and reopen `SupportDesk.sln`.

### Tests

```bash
npm test                      # both suites: dotnet test, then the web tests
```

Both pass on a clean checkout. The backend suites live in `apps/api/tests`: `SupportDesk.UnitTests`
(domain aggregates and application handlers, laid out like `src`) and
`SupportDesk.ArchitectureTests` (layer dependency rules, and a check that the EF model matches
the migrations). Neither needs a database.

---

## Repository layout

An `apps` monorepo: each deployable application lives in `apps/` together with its own code and
tests. The API is a **domain-driven monolith** built in four clean-architecture layers.

```
.
├── apps/
│   ├── api/                                .NET backend
│   │   ├── src/
│   │   │   ├── SupportDesk.Domain/         1. the core: no dependencies at all
│   │   │   │   ├── Aggregates/             Tickets/, Categories/, Customers/, Agents/ — each
│   │   │   │   │                           aggregate root with its child entities and enums
│   │   │   │   ├── Common/                 Entity and AggregateRoot base classes
│   │   │   │   ├── Exceptions/             BusinessRuleViolationException
│   │   │   │   └── Repositories/           repository and unit-of-work interfaces
│   │   │   ├── SupportDesk.Application/    2. use cases; depends only on Domain
│   │   │   │   ├── Abstractions/           IClock and the read-side query interfaces
│   │   │   │   ├── Contracts/              request / response DTOs, per feature
│   │   │   │   ├── Exceptions/             NotFound / Conflict / Validation
│   │   │   │   ├── Features/<Feature>/
│   │   │   │   │   ├── Commands/<UseCase>/ state changes: handler + request validator
│   │   │   │   │   └── Queries/<UseCase>/  reads
│   │   │   │   └── DependencyInjection.cs
│   │   │   ├── SupportDesk.Infrastructure/ 3. EF Core + SQL Server
│   │   │   │   ├── Data/                   SupportDbContext (also the unit of work),
│   │   │   │   │   ├── Configurations/     Fluent API mappings
│   │   │   │   │   └── Migrations/         + the development seed data
│   │   │   │   ├── Repositories/           domain repository implementations
│   │   │   │   ├── Queries/                read-side query implementations
│   │   │   │   └── Services/               SystemClock
│   │   │   └── SupportDesk.Presentation/   4. ASP.NET Core Web API + composition root
│   │   │       ├── Controllers/            thin: one handler call per action
│   │   │       ├── Filters/                request validation filter
│   │   │       ├── Middleware/             central exception handling
│   │   │       └── Program.cs
│   │   └── tests/
│   │       ├── SupportDesk.UnitTests/      mirrors src: Domain/ (aggregates) and Application/ (handlers)
│   │       └── SupportDesk.ArchitectureTests/ layer dependency rules + "model matches migrations"
│   └── web/                                React 19 + TypeScript + Vite (npm workspace @support-desk/web)
│       └── src/
│           ├── api/                        one typed client per resource
│           ├── components/                 shared presentational components
│           ├── features/                   tickets/ and customers/ — pages, components, hooks
│           ├── hooks/                      shared hooks
│           ├── lib/                        formatting helpers
│           └── types/api.ts                the API contract, in TypeScript
├── db/                                     reference schema + seed script
├── spec/                                   feature specifications (empty for now)
├── .github/
│   ├── workflows/ci.yml                    CI: format, build and test the API; lint, typecheck, test and build the web app
│   └── pull_request_template.md
├── .config/dotnet-tools.json               pinned local .NET tools (dotnet-ef)
├── .editorconfig / .gitattributes          formatting and line-ending rules, shared by every editor and OS
├── .nvmrc                                  Node.js version for nvm / fnm / CI
├── Directory.Build.props                   shared C# settings for every project
├── Directory.Packages.props                central NuGet versions — one version per package, repo-wide
├── global.json                             pinned .NET SDK
├── package.json                            npm workspaces + the repo-wide scripts
├── docker-compose.yml                      SQL Server for local development
├── SupportDesk.sln
└── ASSIGNMENT_CANDIDATE.md                 the interview assignment
```

### How the layers depend on each other

Dependencies point inwards, towards the domain:

```
Presentation ──► Application ──► Domain
     │                ▲            ▲
     └──► Infrastructure ──────────┘
```

- **Domain** depends on nothing. Aggregates have private setters and change only through their
  methods (`Ticket.Raise`, `ChangeStatus`, `AssignTo`, `Agent.AddSpecialization`, …), so their
  rules cannot be bypassed; a broken rule throws `BusinessRuleViolationException`. Aggregates
  refer to each other **by id only** — no navigation properties between aggregates. Domain
  methods take "now" as a parameter rather than reading a clock.
- **Application** depends only on Domain. Each use case is one handler class, under
  `Features/<Feature>/Commands` (changes state, through a repository and `IUnitOfWork`) or
  `Features/<Feature>/Queries` (reads, through an `I…Queries` interface that returns DTOs). It
  knows nothing about EF Core, HTTP or SQL Server.
- **Infrastructure** implements the domain's repositories and the application's query
  interfaces with EF Core. Write-side repositories load whole, tracked aggregates; read-side
  queries use `AsNoTracking()` and join across aggregates to project straight to DTOs.
- **Presentation** maps HTTP to handlers and back, and wires everything together in
  `Program.cs`. No business rules live here.

`apps/api/tests/SupportDesk.ArchitectureTests` checks the dependency rule against the compiled
assemblies, and fails when an EF Core mapping changes without a migration — neither needs a
database.

### Conventions already in the codebase

- **DTOs at every boundary.** Entities are never returned from a controller. Mapping happens in
  the query projections — there is no AutoMapper, and you do not need one.
- **Validation** uses FluentValidation: one validator per request DTO, next to the command that
  takes it, run by `apps/api/src/SupportDesk.Presentation/Filters/ValidationFilter.cs`,
  surfacing as `400` with a problem document.
- **Errors**: `NotFoundException` → `404`, `ConflictException` and
  `BusinessRuleViolationException` → `409`, `ValidationException` → `400`, anything else →
  `500`. All handled centrally in
  `apps/api/src/SupportDesk.Presentation/Middleware/ExceptionHandlingMiddleware.cs`;
  controllers never catch.
- **Repositories and queries** expose intention-revealing async methods, not `IQueryable`.
- **Time** comes from `IClock`.
  `apps/api/src/SupportDesk.Infrastructure/Services/SystemClock.cs` is the only code that reads
  the machine clock, so behaviour that depends on "now" stays testable.
- **Enums** are stored as `int` and travel as strings over the wire.
- **Cancellation tokens** are threaded through every async call.
- **NuGet versions** live in `Directory.Packages.props`, never in a `.csproj`.
- **Web**: components never call `fetch` — they use a hook, which uses a client in
  `apps/web/src/api`. Feature folders own their pages, components and hooks.

---

## API reference

Base URL `https://localhost:7043`. Timestamps are UTC ISO-8601. Enums travel as strings.

`CustomerTier`: `Standard` | `Premium` · `TicketPriority`: `Low` | `Medium` | `High` | `Critical`
`TicketStatus`: `New` | `Open` | `InProgress` | `Resolved` | `Closed`
`SlaStatus` (derived): `WithinSla` | `AtRisk` | `Breached` | `Met` | `NotApplicable`

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list. Query: `page`, `pageSize` (max 100), `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy` (`createdAtUtc`\|`updatedAtUtc`\|`dueAtUtc`\|`priority`\|`status`), `sortDirection` (`asc`\|`desc`). **The filter parameters (`search` to `unassignedOnly`) are accepted but not applied yet** — only sorting and paging are implemented server-side. |
| `GET /api/tickets/{id}` | One ticket with description and customer contact details. `404` if absent. |
| `POST /api/tickets` | `{ title, description, customerId, categoryId, requestedPriority? }` → `201` + `Location`. Title 5–200, description 10–4000. |
| `PATCH /api/tickets/{id}/status` | `{ status }`. Stamps or clears `resolvedAtUtc`. `409` when reopening a closed ticket. |
| `PATCH /api/tickets/{id}/assignment` | `{ agentId }` — `null` unassigns. `404` unknown agent, `409` inactive agent. This is the manual override, so it does not apply the specialist rule. |
| `GET /api/customers` · `GET /api/customers/{id}` | Customers with open-ticket counts; detail includes their tickets. |
| `GET /api/agents` | Agents with `openTicketCount`, `maxOpenTickets`, `isActive` and their specializations. |
| `GET /api/categories` | Categories with `requiresSpecialist` and `forcesCriticalPriority`. |

A list row:

```json
{
  "id": 13, "reference": "TCK-0013", "title": "Invoice total does not match the order",
  "status": "InProgress", "priority": "High",
  "customer": { "id": 2, "name": "Fabrikam Inc", "tier": "Standard" },
  "category": { "id": 2, "name": "Billing" },
  "assignedAgent": { "id": 4, "fullName": "Sara Lindqvist" },
  "createdAtUtc": "2026-09-26T21:01:28.378Z", "updatedAtUtc": "2026-09-26T22:41:28.378Z",
  "dueAtUtc": "2026-09-27T05:01:28.378Z", "resolvedAtUtc": null,
  "slaStatus": "WithinSla"
}
```

Errors use `ProblemDetails`:

```json
{ "title": "Conflict", "status": 409,
  "detail": "Ticket TCK-0026 is closed and cannot be moved to Open.",
  "traceId": "0HNORVTEJS051:00000001" }
```

Validation failures add `"errors": { "Title": ["..."] }`.

---

## Data model

```
Customers ──< Tickets >── Categories          Agents >──< Categories
                 │                              (AgentSpecializations)
                 └── AssignedAgentId ──► Agents
```

- `Customers.Tier` drives the response window a customer gets.
- `Categories.RequiresSpecialist` / `Categories.ForcesCriticalPriority` carry handling rules as
  **data**, so they can change without a code change.
- `Agents.MaxOpenTickets` caps how much work an agent takes.
- Ticket indexes cover the list screen's filters and sorts — see
  `apps/api/src/SupportDesk.Infrastructure/Data/Configurations/TicketConfiguration.cs`.

`db/schema.sql` is the same schema as raw SQL (reference only — the EF migrations are
the source of truth), and `db/seed.sql` is the demo data plus some handy verification
queries. The API seeds the same data automatically in Development.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their open
ticket limit) and 40 tickets spread across every status, priority and SLA state.

To reset the data without dropping the database:

```bash
docker exec -i supportdesk-sql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "Local_Dev_Password1" -C -i /dev/stdin < db/seed.sql
```

---

## Configuration

| Where | What |
| --- | --- |
| `.env.example` → `.env` | `MSSQL_SA_PASSWORD` for the SQL Server container, plus `ConnectionStrings__Default` and `ASPNETCORE_ENVIRONMENT` for the API. |
| `apps/web/.env.example` → `apps/web/.env.local` | `VITE_API_PROXY_TARGET` (dev-server proxy) and `VITE_API_BASE_URL` (what the app calls). |
| `apps/api/src/SupportDesk.Presentation/appsettings.Development.json` | Local defaults, including EF command logging so you can see the SQL that runs. |

`.env` files are git-ignored, and only `VITE_`-prefixed variables reach the browser — never put
a secret in one. Nothing here is required to run the project: every value has a working default.

---

## Commands

Run these from the repository root.

| Command | What it does |
| --- | --- |
| `npm install` | Installs the web workspace |
| `npm run db:up` / `npm run db:down` | Start / stop SQL Server |
| `npm run db:reset` | Throw the database away and start it again, empty |
| `npm run api` | Run the API (migrates and seeds in Development) |
| `npm run web` | Run the React dev server |
| `npm run build` | Build both halves |
| `npm test` | Run both test suites |
| `npm run lint` / `npm run typecheck` | ESLint / TypeScript on the web app |
| `npm run format` / `npm run format:check` | Apply / verify `dotnet format` on the .NET code (CI runs the check) |
| `npm run db:migrate` | Apply EF Core migrations |
| `npm run ef -- migrations add <Name>` | Add a migration (any `dotnet ef` command works this way; restores the pinned `dotnet-ef` tool first) |

The underlying .NET commands still work if you prefer them:

```bash
dotnet build
dotnet test
dotnet run --project apps/api/src/SupportDesk.Presentation
dotnet tool restore            # once, for dotnet ef
dotnet ef migrations add <Name> --project apps/api/src/SupportDesk.Infrastructure --startup-project apps/api/src/SupportDesk.Presentation
```

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, then `docker compose ps` — is the container healthy? It takes ~20s on first start. |
| `A network-related or instance-specific error` | Port 1433 already in use by another SQL Server; change the port in `docker-compose.yml` and `.env`. |
| Browser warns about the API certificate | The backend uses the ASP.NET development certificate. `dotnet dev-certs https --trust`, or just use the web app, which proxies. |
| `npm run ef` fails with "came from another computer and might be blocked" | Windows has marked the tool manifest as downloaded. Unblock it once: `powershell Unblock-File .config/dotnet-tools.json`. |
| Visual Studio starts the wrong project, or lists projects that no longer exist | Close Visual Studio, delete the hidden `.vs` folder in the repository root, reopen `SupportDesk.sln`, and set **SupportDesk.Presentation** as the startup project. |
| Web app shows "Unable to load tickets" | The API is not running, or is on a different port than `VITE_API_PROXY_TARGET`. |
| SQL Server container is slow on Apple Silicon | The image is x86 and runs under emulation. Enable Rosetta in Docker Desktop → Settings → General. |
