# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
escalate them, and track them against an SLA.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.
The assignment is in [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md). My reasoning for each task
is in [`APPROACH.md`](APPROACH.md).

## Status

| # | Task | Status |
| --- | --- | --- |
| 1 | Defect fix and server-side filtering | Done |
| 2 | Ticket escalation and assignment | Done (optional stretch 2.5, the SLA summary report, not built) |
| 3 | Login and authentication | TODO: update when finished (or write "not finished") |
| 4 | Dockerise the API (optional) | TODO: update when finished (or write "not finished") |

## Development login credentials

TODO (Task 3): none yet, the app is still open. Add the email and the dev password here, and say that
the password is a local fixture only.

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
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. The seed has 5 categories,
6 customers (3 Premium), 6 agents (one inactive, one at their limit) and 40 tickets.

## Tests

```bash
npm test          # dotnet test, then the web tests (Vitest)
npm run lint
npm run typecheck
```

**Stop the API before running the .NET tests.** A running API locks its DLLs on Windows and the build
fails with "file is locked by SupportDesk.Presentation".

As of Task 2: 100 backend tests (xUnit + Moq, SQLite for query tests, plus architecture tests) and
20 web tests (Vitest + Testing Library), all passing, with clean lint and typecheck. No test needs
SQL Server.

What is tested:

- **Triage rules:** priority, SLA window per priority and tier including the 1 h floor, forced-Critical
  categories, agent selection (fewest open tickets, specialists, limit, inactive, ties, nobody
  eligible), escalation rules.
- **Ticket behaviour:** escalation writes the right history row and rejects Critical, Resolved, Closed.
- **SLA status at exact boundaries** (25% left, due exactly now, resolved exactly on time).
- **Query filtering against a real database (SQLite):** exact-match priority, filter combinations, search
  on customer name, count and pages for the filtered set, SLA status filter.
- **Handlers:** create with triage (including nobody eligible), escalate (404, 409 messages, owner kept
  or changed).
- **Web:** every filter change sends a request immediately and search is debounced (DEFECT-117), a short
  escalation reason blocks submission, a 409 is shown and the ticket stays unchanged, the SLA indicator
  renders every state distinguishably.
- **Architecture test:** the EF model matches the latest migration.

What is deliberately not tested automatically: the `AtRisk` and `WithinSla` SQL filters (they use SQL
Server's `DATEDIFF`, which SQLite does not have; their rules are covered by the evaluator tests and I
checked the SQL by hand), and the browser end to end (no Playwright; checked manually).

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list. Filters: `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus` (`NotApplicable`, `WithinSla`, `AtRisk`, `Breached`, `Met`); plus `sortBy`, `sortDirection`, `page`, `pageSize`. Everything is applied in SQL before counting and paging. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create. Priority, due date and owner are decided automatically; the response has a `triage` object with the reasons. 201 also when nobody is eligible (unassigned, with the reason). |
| `POST /api/tickets/{id}/escalate` | Escalate one level. Body: `reason` (5 to 500 characters), `escalatedBy` (until Task 3). 200 with the ticket and the new history row; 400 invalid input; 404 unknown ticket; 409 Critical or Resolved/Closed (message says which). |
| `GET /api/tickets/{id}/escalations` | History, newest first. |
| `PATCH /api/tickets/{id}/status` / `/assignment` | Change status / assignee (assignment is a manual override). |
| `GET /api/customers` / `/api/agents` / `/api/categories` | Reference data |

Errors are `ProblemDetails`: 400 validation, 404 not found, 409 conflict or business rule.

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
"expected 2 calls, got 1") and pass on the fix. By hand: DevTools, Network tab. Changing Status sends
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
all `InProgress`. A blank search returned all 40 tickets.

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
- `EscalationPanel` validates the reason (5 to 500 characters after trimming) and "escalated by" (required,
  max 100) with the same limits as the API, so invalid input never reaches it. It shows a pending state
  and server errors, including a 409 message. When a ticket is known to be non-escalatable (Critical,
  Resolved, Closed) it shows why instead of the form, and a server rejection is still handled.
- The escalate response contains the updated ticket, so the page uses it directly and only re-fetches the
  history. No full reload.
- After creating a ticket the page shows the applied priority, due date, assigned agent (or "Not assigned"
  with the reason).
- Everything is typed; no `any`, no `@ts-ignore`.

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
- **Manual assignment stays an override** that does not apply the specialist rule, as before Task 2.

## Assumptions (where the spec was open)

- The due date after an escalation is *now* + the new window, not creation time + window.
- Escalation always re-evaluates the owner; if nobody is eligible the ticket becomes unassigned and the
  history row records a null `ToAgentId`.
- A ticket with no due date has no window and can never be AtRisk.
- Combining `assignedAgentId` with `unassignedOnly=true` is contradictory, so it returns nothing.
- The search text is matched as one phrase, not split into words.
- Until Task 3, `escalatedBy` is a free-text field.

---

## AI usage

**Tool and purpose.** I used Claude (chat) throughout: to explain the codebase, find the root cause of
DEFECT-117, design the rules and the persistence, write code and tests, and draft documentation.
TODO: rewrite this section in your own words and keep it honest; the points below are what happened.

- **A workflow that worked:** splitting Task 2 into rules, then database, then wiring, then UI, with tests
  at each step before moving on. I ran every test and check myself.
- **Accepted as is:** TODO (for example: the predicate-list structure for filtering, after reading it and
  testing each filter).
- **Changed or rejected:** the first plan derived the SLA window from due date minus created date; that
  breaks after an escalation, so the window length is stored. The assistant's first escalation design put
  the owner choice inside the aggregate, which would have needed a repository; the decision moved to
  `TicketTriage`.
- **Bugs the AI introduced, and how they were caught:** (1) The first fix for DEFECT-117 sent a stale
  request on every keystroke; my regression tests caught it (2 failures, 1 call expected, 2 and 4
  received). (2) The second version failed the `react-hooks/set-state-in-effect` lint rule, so `isLoading`
  became derived. (3) Edit instructions for `TicketQueries.cs` led me to a duplicated method that did not
  compile; I replaced the whole file. TODO: add anything else you hit.
- **How generated code was verified:** unit and query tests, the architecture test that compares the model
  with the migration, `typecheck` and `lint`, reading the SQL EF Core logs, checking the tests fail when
  the filter is disabled (6 of 7 failed), and manual calls in Swagger and the browser. TODO: fill in the
  manual-check results below.

### Manual checks I ran (SQL Server, Swagger and browser)

- TODO: `POST /api/tickets` for a Premium customer with a Security category requested as Low: result
  (expected 201, Critical, due about 2 h ahead, an assigned specialist, triage reasons).
- TODO: escalate returned 200, 400, 404 and 409 as expected; history newest first.
- TODO: each `slaStatus` value returned only matching tickets and the five totals added up to 40.
- TODO: browser: an invalid reason sent no request; a valid reason updated the page without a reload.

## Known limitations

- `escalatedBy` is typed by the user until Task 3 uses the signed-in agent.
- The SQL SLA filter and `SlaEvaluator` are two forms of one rule. `DATEDIFF` counts whole seconds, so a
  ticket within one second of the at-risk threshold could be labelled differently by the list and the
  filter.
- SQLite has no `DATEDIFF`, so AtRisk and WithinSla filters have no automated database test.
- Two tickets created at the same instant could read the same agent loads and pick the same agent (no
  locking). `NextReferenceAsync` still uses "highest id + 1" (existing behaviour).
- The `Sla` section currently lives only in `appsettings.Development.json`.
- Optional stretch 2.5 (SLA summary report) was not built.
- TODO: add Task 3 and Task 4 limitations (registration, refresh tokens, HTTPS, and so on).

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait about 20 s, check `docker compose ps` |
| `docker` commands fail with a 500 error | Restart Docker Desktop (`wsl --shutdown`, then reopen it) |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
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