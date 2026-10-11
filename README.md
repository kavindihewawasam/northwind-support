# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign and escalate them,
and track them against an SLA. Agents sign in to use it.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.
The assignment is in [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md). My reasoning for each task
is in [`APPROACH.md`](APPROACH.md).

## Status

| # | Task | Status |
| --- | --- | --- |
| 1 | Defect fix and server-side filtering | Done |
| 2 | Ticket escalation and assignment | Done (optional stretch 2.5, the SLA summary report, not built) |
| 3 | Login and authentication | Done |
| 4 | Dockerise the API (optional) | **Not done** (see Known limitations) |

## Development login credentials

| Email | Password |
| --- | --- |
| `alex.turner@northwind-support.example` | `LocalDevOnly!123` |

The same password works for `ben.osei@`, `priya.nair@`, `sara.lindqvist@` and `marcus.doyle@`
(all `@northwind-support.example`). `yuki.tanaka@` is an inactive agent and cannot sign in (on purpose,
useful to test). The password is a **local fixture**: it lives only in
`appsettings.Development.json` (`Seed:AgentPassword`), is hashed before it is stored, and is only
applied when the API runs in Development.

## Run it

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker Desktop.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433 (first run downloads ~1.5 GB)
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app (sign in here) | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger (open; protected endpoints need a token, which Swagger does not send) |

In Development the API applies migrations, seeds demo data (5 categories, 6 customers, 6 agents, 40
tickets) and gives every agent the development password. To start from an empty database:
`npm run db:reset`, then `npm run db:up`, then `npm run api`.

## Tests

```bash
npm test          # dotnet test, then the web tests (Vitest)
npm run lint
npm run typecheck
```

**Stop the API before running the .NET tests.** A running API locks its DLLs on Windows and the build
fails with "file is locked by SupportDesk.Presentation". No test needs SQL Server.
TODO: add the totals from your last `npm test` (backend and web) here.

What is tested:

- **Triage rules:** priority, SLA window per priority and tier including the 1 h floor, forced-Critical
  categories, agent selection (fewest open tickets, specialists, limit, inactive, ties, nobody eligible).
- **Ticket behaviour:** escalation writes the right history row and rejects Critical, Resolved, Closed.
- **SLA status at exact boundaries** (25% left, due exactly now, resolved exactly on time).
- **Query filtering against a real database (SQLite):** exact-match priority, filter combinations, search
  on customer name, count and pages for the filtered set, SLA status filter.
- **Handlers:** create with triage (including nobody eligible), escalate (404, 409 messages, owner kept or
  changed, the signed-in agent recorded), login (below).
- **Authentication:** a wrong password gives a 401-type error and no token; an unknown email gives the
  identical error and still runs a password check; an inactive agent cannot sign in; the password hasher
  salts, verifies and rejects correctly and never stores the password.
- **Web:** every filter change sends a request immediately and search is debounced (DEFECT-117); a short
  escalation reason blocks submission; a 409 is shown and the ticket stays unchanged; the SLA indicator
  renders every state distinguishably; the API client attaches the token and a 401 ends the session
  (but a rejected login does not); unauthenticated visitors are redirected to login and remembered; an
  expired session is ignored; the login form validates and shows one message for rejected credentials.
- **Architecture:** the EF model matches the latest migration.

Not tested automatically: the `AtRisk` and `WithinSla` SQL filters (SQL Server's `DATEDIFF`, which SQLite
does not have; the rules are covered by the evaluator tests and I checked the SQL by hand), and
"a protected endpoint is 401 without a token and 200 with one" end to end (checked by hand; an
integration test with `WebApplicationFactory` would add it).

## API at a glance

| Endpoint | Description |
| --- | --- |
| `POST /api/auth/login` | **Open.** Body `email`, `password`. Returns `accessToken`, `expiresAtUtc` and the `user`. 401 with the same message for an unknown email, a wrong password or an inactive agent. |
| `GET /api/auth/me` | The signed-in agent (from the token), or 401. |
| `GET /api/tickets` | Paged list. Filters: `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus` (`NotApplicable`, `WithinSla`, `AtRisk`, `Breached`, `Met`); plus `sortBy`, `sortDirection`, `page`, `pageSize`. All applied in SQL before counting and paging. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create. Priority, due date and owner are decided automatically; the response has a `triage` object with the reasons. 201 also when nobody is eligible (unassigned, with the reason). |
| `POST /api/tickets/{id}/escalate` | Escalate one level. Body: `reason` (5 to 500 characters). Who escalated comes from the token. 200 with the ticket and the new history row; 400 invalid reason; 404 unknown ticket; 409 Critical or Resolved/Closed (message says which). |
| `GET /api/tickets/{id}/escalations` | History, newest first. |
| `PATCH /api/tickets/{id}/status` / `/assignment` | Change status / assignee (assignment is a manual override). |
| `GET /api/customers` / `/api/agents` / `/api/categories` | Reference data |

Every endpoint except `POST /api/auth/login` requires a valid token (`Authorization: Bearer ...`).
Errors are `ProblemDetails`: 400 validation, 401 not signed in, 404 not found, 409 conflict or business rule.

---

## Task 1: DEFECT-117 and server-side filtering

### The defect: root cause, fix, how to verify

**Root cause.** The effect in `useTicketList` that loads tickets only depended on `search`, `page` and
`pageSize`. Changing status, priority, category, customer, agent, unassigned-only or sort updated the
state but never triggered a request. The next keystroke in the search box re-ran the effect and sent
whatever the filters were by then, which is the "stale status" in the report. An `eslint-disable`
comment ("filters object identity changes every render") hid the missing-dependency warning, and its
reasoning was wrong: `filters` comes from `useState`, so it only changes when it is set.

**Fix.** The hook keeps two states: `filters` (what the inputs show, updated on every change) and
`query` (what requests are built from). A change to anything except the search text sets `query`
immediately, so one request goes out. A change to the search text alone sets it after 300 ms of
silence, so typing does not send a request per keystroke. The request is built from the whole filters
object, so a new filter needs no wiring in the hook (the Task 2 SLA filter did not touch it). Stale
responses are ignored with a cleanup flag, and `isLoading` is derived ("the current query has not been
answered yet"), which also satisfies the `react-hooks/set-state-in-effect` lint rule.

**Not done, on purpose:** forced reloads, remount keys or filtering in memory. Those hide the effect
instead of fixing the cause.

**How to verify.** `npm test`: the `useTicketList` tests failed on the original code (7 of 10 with
"expected 2 calls, got 1") and pass on the fix. By hand, in DevTools' Network tab: changing Status sends
a request with `status=InProgress` at once; typing a word quickly sends one request after you stop;
clearing the search keeps the status.

### Filtering

**Where it is applied.** `TicketQueries.GetPagedAsync` (Infrastructure). The query runs in this order:
join labels, apply filters, count, sort, skip/take, project. Count and page come from the *filtered*
query, so `totalCount`, `totalPages` and the pager describe the filtered set. Nothing is filtered in C#.

**How.** `Predicates(query)` yields one small condition per filter the caller supplied; `ApplyFilters`
chains them with `Where`, which EF Core turns into a single SQL `WHERE ... AND ...`.

**Rules as understood.** `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId` are exact
matches. `unassignedOnly=true` returns tickets with no agent. `search` is trimmed, a blank search is
ignored, and it matches title, reference (`TCK-0042`) or customer name with "contains". Case sensitivity
follows the database collation (SQL Server's default is case-insensitive).

**How the SQL was checked.** I called the API with each filter and read the SQL EF Core logs in the
API terminal: the `WHERE` is in both the count query and the page query. Example:
`GET /api/tickets?status=InProgress&pageSize=3` returned `totalCount: 7`, `totalPages: 3`, three items,
all `InProgress`. A blank search returned every ticket.

**How to add the next filter.** Add a field to `TicketQuery` and one `if (...) { yield return x => ...; }`
block in `Predicates`. Counting, sorting and paging do not change. The `slaStatus` filter was added
exactly this way.

---

## Task 2: Escalation, assignment and SLA

### Rules as understood

| Rule | What the code does |
| --- | --- |
| BR-1 priority | The requested priority, Medium by default. A category with `ForcesCriticalPriority` is always Critical. |
| BR-2 / BR-3 window | Due = time of triage + base window (Critical 4 h, High 8 h, Medium 24 h, Low 72 h). Premium customers get 0.5 of it, never under 1 h. |
| BR-4 / BR-5 assignment | Eligible = active, specialised if the category has `RequiresSpecialist`, strictly below `MaxOpenTickets`. Choose the fewest open tickets (not Resolved/Closed); the lowest agent id wins a tie. Nobody eligible: the ticket is created unassigned with a reason (201, not an error). |
| BR-6 configurable | Windows, premium multiplier, floor and at-risk threshold come from the `Sla` section of `appsettings` (bound to `SlaPolicy`, no defaults in code). Behaviour comes from the category flags; no category name appears in code. |
| BR-7 escalation | One priority level up; due date recomputed from now; keep the current agent while still eligible, otherwise choose again; an immutable history row. Critical and Resolved/Closed tickets cannot be escalated. |
| BR-8 SLA status | Derived, never stored: NotApplicable (no due date), Met, Breached, AtRisk (25% or less of the window left), WithinSla. "Now" is a parameter. |

### Where things live

| Piece | Responsibility |
| --- | --- |
| `Domain/Triage/SlaPolicy` | Window for a priority and tier (BR-2, BR-3, BR-6) |
| `Domain/Triage/PriorityRules` | Initial priority and one-level escalation (BR-1, BR-7) |
| `Domain/Triage/AgentAssignment` | Eligibility, choice, keep-or-choose (BR-4, BR-5, BR-7) |
| `Domain/Triage/TicketTriage` | Combines the three into one decision with a reason for each. Used by create and escalate. |
| `Ticket.Escalate` / `TicketEscalation` | Guards the escalation and writes the history row. Rows are immutable. |
| `SlaEvaluator` | Derived SLA status (BR-8) |
| `ITriageInputs` (`TriageInputs`) | Read-only: category flags, customer tier, agents with their open-ticket counts |
| `RaiseTicketCommandHandler`, `EscalateTicketCommandHandler` | Load inputs, call `TicketTriage`, save. No business rules inside. |
| `TicketQueries` | Lists with filters, including the `slaStatus` predicate in SQL |

### Database

Migration `AddTicketEscalations`:

- `Tickets.SlaWindowMinutes` (int, nullable). Existing rows are backfilled in the migration
  (`DATEDIFF(minute, CreatedAtUtc, DueAtUtc)`) and the seeder sets it for demo tickets.
- Table `TicketEscalations`: `Id`, `TicketId` (FK), `FromPriority`/`ToPriority`, `FromAgentId`/`ToAgentId`
  (nullable FKs), `FromDueAtUtc` (nullable)/`ToDueAtUtc`, `Reason` (500), `EscalatedBy` (100),
  `EscalatedAtUtc`.
- **Index `IX_TicketEscalations_TicketId_EscalatedAtUtc` (chosen).** The history is always read as "one
  ticket's rows, newest first", so one composite index serves the foreign key and the sort. EF Core also
  generated plain indexes on `FromAgentId` and `ToAgentId` (it indexes foreign keys by default); I kept
  them but did not choose them.
- **Delete behaviour: Restrict** on every foreign key of the history table. It is an audit record, so
  removing a ticket or an agent must not silently delete or detach it. Agent ids are nullable because a
  ticket can have no owner.
- Reads use `AsNoTracking`, narrow projections and no per-row queries (agent workload is one subquery,
  not one query per agent).

### Frontend

- `SlaIndicator` is the single SLA component, used in the list and the detail page. Each state has a word
  and a symbol (`▲ At risk`, `✕ Breached`, `● Within SLA`, `✓ Met`, `– No SLA`), so colour is never the
  only signal. The states live in one table (`slaPresentation`) that the filter bar also uses.
- The SLA filter is an ordinary filter in the filter bar; the list hook did not change.
- `EscalationPanel` validates the reason (5 to 500 characters after trimming) with the same limits as the
  API, so invalid input never reaches it. It shows a pending state and server errors, including a 409
  message. When a ticket is known to be non-escalatable (Critical, Resolved, Closed) it shows why instead
  of the form, and a server rejection is still handled.
- The escalate response contains the updated ticket, so the page uses it directly and only re-fetches the
  history. No full reload.
- After creating a ticket the page shows the applied priority, due date, assigned agent (or "Not assigned"
  with the reason).

---

## Task 3: Login and authentication

### How it works

- **Credential storage (AU-1).** A nullable `Agents.PasswordHash` column (migration `AddAgentPasswordHash`).
  An agent without a hash cannot sign in.
- **Hashing (AU-2).** ASP.NET Identity's `PasswordHasher` (PBKDF2 with a random salt per password and a
  stored iteration count) behind an `IPasswordHasher` interface. Nothing is hand-written.
- **Development credentials (AU-3).** `AgentCredentialSeeder` runs at startup in Development and hashes
  `Seed:AgentPassword` for every agent that has no hash. It runs on every start, so a database seeded
  before login existed also gets credentials.
- **Login (AU-4).** `POST /api/auth/login` returns an HS256 signed JWT (claims `sub`, `name`, `email`),
  its expiry and the user. An unknown email, a wrong password and an inactive agent all return the *same*
  401 and the same message. A password check always runs, against a dummy hash when the email is unknown,
  so the response time does not reveal which accounts exist. The password is limited to 128 characters so
  nobody can make the server hash a huge string.
- **Current user (AU-5).** `GET /api/auth/me` reads the signed-in agent from the token's claims.
- **Central enforcement (AU-6).** A fallback authorization policy requires an authenticated user for every
  endpoint; only login is `[AllowAnonymous]`. A new controller is protected without anyone remembering an
  attribute. Swagger stays open.
- **Signing key (AU-7).** `Jwt:SigningKey` comes from configuration, never from code. Development has a
  clearly-labelled local fixture in `appsettings.Development.json`; any other environment must set the
  environment variable `Jwt__SigningKey` (listed in `.env.example`), and the API refuses to start without
  a key of at least 32 bytes. Issuer, audience and lifetime are in `appsettings.json`.
  **Token lifetime: 60 minutes.** There are no refresh tokens (not wanted), so this is also the session
  length; after it the next request gets a 401 and the agent signs in again.
- **Actor from the token (AU-8).** `escalatedBy` was removed from the request body. The controller reads
  the agent's name from the token and passes it to the handler, so it cannot be spoofed. It is cut to 100
  characters, the history column's size (agent names can be 200).

### Frontend

- **Token storage (AU-10).** In `sessionStorage` (`auth/session.ts`): it survives a reload and disappears
  when the tab closes. **Trade-off:** a script injected into the page (XSS) could read it. An httpOnly
  cookie would not be readable by scripts, but needs CSRF protection and cookie settings, which is more
  than this app needs. `api/client.ts` attaches `Authorization: Bearer ...` to every call.
- **Login page (AU-9).** Labelled fields, client validation, a pending state and one clear message when
  credentials are rejected.
- **Protected routes (AU-11).** `RequireAuth` sends visitors who are not signed in to `/login` and
  remembers the page they wanted, so they return there after signing in (or to the ticket list).
- **Name and logout (AU-12).** The header shows the signed-in agent and a Log out button.
- **401 handling (AU-13).** Any 401 clears the session and the protected routes redirect to login. A 401
  from the login call itself is not treated as an expired session; it just means wrong credentials.

---

## Design decisions (chosen, rejected, why)

- **Rules as small classes in the domain, handlers kept thin.** Create and escalate call the same
  `TicketTriage`, so the two cannot drift apart. Rejected: rules in controllers or handlers (duplicated,
  and only testable with a database or heavy mocks).
- **A read-only `ITriageInputs` seam.** The rules receive plain values (category flags, tier, agent
  candidates). Rejected: loading agents and tickets through the repositories and counting in C# (one
  count per agent), and widening the repository interfaces.
- **Configuration in the existing `Sla` section, no defaults in code.** A missing priority fails loudly.
  Rejected: constants in code and a settings table.
- **The SLA window is stored next to the due date (`SlaWindowMinutes`).** "At risk" needs the length of the
  window, and escalation restarts it, so it is not always due minus created. Rejected: recomputing it from
  priority and tier (breaks when configuration changes) and a "window start" column. The status itself is
  never stored.
- **`Ticket.Escalate` guards and records; `TicketTriage` decides.** The aggregate cannot load agents, so
  the new values are passed in, and the aggregate checks the escalation is allowed and exactly one level.
- **Escalation leaves the ticket's own load out of its agent's count.** Otherwise an agent exactly at their
  limit would be "ineligible" for the ticket they already hold.
- **The `slaStatus` filter has a SQL form (`SlaPredicate`) next to the C# evaluator.** It has to run before
  paging, so it cannot reuse the C# method. The cost is two forms of one rule, covered by tests on the C#
  side and a manual check on SQL Server.
- **Debounce only the search text** (DEFECT-117). Debouncing dropdowns would make them feel laggy.
- **Credentials as a column on `Agent`, not a `Users` table.** Agents are the only users, each has one
  login and there are no roles, so a second table would add a join and nothing else. Rejected: a Users
  table; BCrypt (an extra package for no gain over Identity's hasher); writing PBKDF2 by hand.
- **Password hashing behind `IPasswordHasher`, token creation behind `ITokenIssuer`.** The login handler
  is tested with mocks, the real hasher is tested separately. The JWT code lives in the web project because
  the JWT package is only referenced there.
- **A fallback policy instead of `[Authorize]` on controllers.** One place decides; forgetting an attribute
  cannot open an endpoint. Rejected: attributes per controller.
- **`escalatedBy` from the token, not the body.** Removed from the contract and the UI.
- **Manual assignment stays an override** that does not apply the specialist rule, as before Task 2.

## Assumptions (where the spec was open)

- The due date after an escalation is *now* + the new window, not creation time + window.
- Escalation always re-evaluates the owner; if nobody is eligible the ticket becomes unassigned and the
  history row records a null `ToAgentId`.
- A ticket with no due date has no window and can never be AtRisk.
- Combining `assignedAgentId` with `unassignedOnly=true` is contradictory, so it returns nothing.
- The search text is matched as one phrase, not split into words.
- All agents share one development password; it is a fixture, not a policy.
- Email lookup is exact after trimming; case sensitivity follows the database collation.

---

## AI usage

**Tool and purpose.** I used Claude (chat) throughout: to explain the codebase, find the root cause of
DEFECT-117, design the rules, the persistence and the authentication, write code and tests, and draft
documentation. TODO: rewrite this section in your own words and keep it honest; the points below are what
happened.

- **A workflow that worked:** splitting each task into small steps (rules, then database, then wiring,
  then UI) with tests before moving on. I ran every test and check myself.
- **Accepted as is:** TODO (for example: the predicate-list structure for filtering, after reading it and
  testing each filter).
- **Changed or rejected:** the first plan derived the SLA window from due date minus created date; that
  breaks after an escalation, so the window length is stored. The first escalation design put the owner
  choice inside the aggregate, which would have needed a repository, so the decision moved to
  `TicketTriage`.
- **Bugs the AI introduced, and how they were caught:** (1) The first fix for DEFECT-117 sent a stale
  request on every keystroke; my regression tests caught it (2 failures, 1 call expected, 2 and 4
  received). (2) The second version failed the `react-hooks/set-state-in-effect` lint rule, so `isLoading`
  became derived. (3) Edit instructions for `TicketQueries.cs` led me to a duplicated method that did not
  compile; I replaced the whole file. TODO: add anything you hit in Task 3.
- **How generated code was verified:** unit and query tests, the architecture test that compares the model
  with the migration, `typecheck` and `lint`, reading the SQL EF Core logs, checking that the tests fail
  when the filter is disabled (6 of 7 failed), and manual checks in Swagger and the browser (below).

### Manual checks I ran

Task 2, in Swagger against SQL Server (before authentication was added):

- Created tickets in the Security and Billing categories: the priority was chosen automatically (Security
  forced Critical, a request without a priority defaulted to Medium), the SLA deadline matched the window
  rules, and an eligible agent was assigned.
- Escalated a ticket Medium to High, then High to Critical: the due date was recalculated each time and
  the history recorded each step.
- A Critical ticket was refused further escalation (409), a reason shorter than 5 characters gave 400, and
  an unknown ticket gave 404.
- TODO (optional): `slaStatus` filter, SLA indicator and escalation panel in the browser.

Task 3, in the browser and Swagger:

- Opening a page signed out redirects to the login page, and signing in returns to the page asked for.
- A wrong password, an unknown email and the inactive agent all show the same message.
- After signing in the name and Log out button appear; logging out returns to the login page.
- The token is sent on every request; a request without it gets 401; an expired or invalid session sends
  the visitor back to the login page.
- Escalating a ticket records the signed-in agent's name.
- TODO: delete any line above that you did not actually run.

## Known limitations

- **Authentication, the weakest part:** a token stays valid for its 60 minutes even if the agent is
  deactivated meanwhile (it is checked at sign-in, not on every request); there is no revocation and no
  refresh token; the token is in `sessionStorage`, readable by an injected script; there is no lockout or
  rate limiting on login; there is no registration, password reset or roles (all out of scope). In
  production I would use a short-lived token in an httpOnly cookie, check the agent on each request or keep
  a revocation list, rate-limit login, keep the key in a secret store and never seed a known password.
- The development signing key and password are fixtures in `appsettings.Development.json`.
- HTTPS is only the ASP.NET development certificate; no certificate work was done.
- Swagger cannot call protected endpoints (there is no Authorize button); use the web app.
- The SQL SLA filter and `SlaEvaluator` are two forms of one rule. `DATEDIFF` counts whole seconds, so a
  ticket within one second of the at-risk threshold could be labelled differently by the list and the
  filter.
- SQLite has no `DATEDIFF`, so AtRisk and WithinSla filters have no automated database test.
- Two tickets created at the same instant could read the same agent loads and pick the same agent (no
  locking). `NextReferenceAsync` still uses "highest id + 1" (existing behaviour).
- **Task 4 (Docker) was not done.** It would need: a multi-stage Dockerfile, a non-root user, `/health`,
  and moving the `Sla` section from `appsettings.Development.json` to `appsettings.json` (it is only
  loaded in Development today) with `Jwt__SigningKey` and the connection string from environment variables.
- Optional stretch 2.5 (SLA summary report) was not built.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait about 20 s, check `docker compose ps` |
| `docker` commands fail with a 500 error | Restart Docker Desktop (`wsl --shutdown`, then reopen it) |
| API stops at startup with "Jwt:SigningKey must be set" | Run in Development (it has a fixture) or set `Jwt__SigningKey` to 32+ characters |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| Sent back to the login page after about an hour | The 60-minute token expired; sign in again |
| Cannot sign in as a seeded agent | Check the password in `appsettings.Development.json`; restart the API so it gives agents their password |
| `dotnet test` / `dotnet build` fails with "file is locked" | Stop the running API first |
| Test DLL blocked by "Application Control policy" (Windows) | Smart App Control blocks unsigned local builds; turn it off or run the tests elsewhere |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop, Settings, General |

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |