# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. **Filters are accepted but not applied yet** (Task 1). |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |


## Task 1.1: DEFECT-117 (filters sometimes do not take effect)

**Root cause.** The effect in `useTicketList` that loads tickets only depended on
`search`, `page` and `pageSize`. Changing status, priority, category, customer, agent,
unassigned-only or sort updated state but never triggered a request; the next search
keystroke re-ran the effect and sent whatever the filters were by then. An
`eslint-disable` comment ("filters object identity changes every render") hid the
missing-dependency warning, and its reasoning was wrong: `filters` comes from `useState`.

**Fix.** The request is built from a `query` state that holds the *whole* filters object.
Dropdowns, checkbox, sort and paging update `query` immediately; only search-text changes
are debounced (300 ms). Stale responses are ignored. No forced reload, remount key or
in-memory filtering.

**Why the SLA filter needs no special wiring.** `query` is the full filters object, so a
new filter (e.g. `slaStatus`) is added to the `TicketFilters` type and the filter bar and is
sent automatically. The hook does not change.

**How to verify.** `npm test` (the `useTicketList` tests fail on the original code: 7 of 10
failed with "expected 2 calls, got 1", and pass on the fix). In the browser, open
DevTools > Network, change Status: a request with `status=InProgress` appears immediately;
type a word quickly: one request about 300 ms after the last key.

**Decisions.** Debounce only the text input (debouncing dropdowns would feel laggy).
Rejected: forced reload / remount key / in-memory filtering (hide the effect, not the cause);
listing every filter by hand in the effect dependencies (easy to forget for the next filter).

**AI usage (Task 1.1).** Used an AI assistant to find the root cause and draft the fix and
tests. I verified the diagnosis myself by reproducing the bug in the Network tab and by
seeing the new tests fail on the original code. The first fix it proposed was wrong: it sent
a stale request on every keystroke (my tests caught it: 2 failed, expecting 1 call but getting
2 and 4). The second version failed the `react-hooks/set-state-in-effect` lint rule, so
`isLoading` is now derived instead of set inside the effect. Generated code was verified
with the tests, lint, typecheck and manual checks in the browser.

## Task 1.2: Server-side filtering

**Where it is applied.** `TicketQueries.GetPagedAsync` (Infrastructure layer). The query is
built in this order: join labels → apply filters → count → sort → skip/take → project.
Because the count and the page both come from the *filtered* query, `totalCount`,
`totalPages` and the pager describe the filtered set (FL-5). Nothing is filtered in C#.

**How it works.** `Predicates(query)` returns one small condition per filter the caller
actually supplied (status, priority, categoryId, customerId, assignedAgentId,
unassignedOnly, search). `ApplyFilters` chains them with `.Where(...)`, which EF Core
translates to a single SQL `WHERE ... AND ...` (FL-4). A filter that is not supplied adds
nothing.

**Rules as understood.**
- `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`: exact match (FL-1).
- `unassignedOnly=true`: only tickets with no assigned agent (FL-2).
- `search`: trimmed; blank is ignored; matches ticket title, reference (e.g. `TCK-0042`) or
  customer name using "contains" (FL-3).

**How to add the next filter (FL-6).** Add one field to `TicketQuery` and one
`if (...) { yield return x => ...; }` block in `Predicates`. The count, sort and paging
code does not change. The Task 2 `slaStatus` filter will be added this way.

**How the SQL was checked.** I called the API and read the SQL that EF Core logs in the API
terminal. For `GET /api/tickets?status=InProgress&pageSize=3` the count query and the page
query both contain the `WHERE` on `Status`, so the database filters before counting and paging.
Results: `totalCount: 7`, `totalPages: 3`, 3 items, all `InProgress`. A blank search
(`?search=%20%20`) returned all 40 tickets.

> TODO before submitting: paste the real `SELECT COUNT(*) ... WHERE ...` text from the API
> terminal here (it shows as `Executed DbCommand`).

**Design decisions.**
- *Chosen:* a list of predicates chained with `Where`. Rejected: one big `Where` with
  `(status == null || x.Status == status) && ...` (grows with every filter and is harder to
  read), and filtering in C# after loading (breaks paging and counts, and was ruled out).
- Search uses `Contains` on title, reference and customer name. Case sensitivity follows the
  database collation (SQL Server's default is case-insensitive). I did not add full-text search
  because it is out of scope.

**Assumptions.**
- Combining `assignedAgentId` with `unassignedOnly=true` is contradictory, so AND returns an
  empty result.
- Search terms are not split into words: the whole trimmed text is matched as one phrase.

**Tests.** Backend tests for exact-match priority, a combination of filters, and a search on
customer name. `TicketQueriesFilterTests` run the real `TicketQueries` against in-memory SQLite
(7 tests): exact-match priority, a combination of filters, search on customer name, search on
reference and title, trimmed/blank search, and count/pages for the filtered set. I checked the
tests are meaningful by temporarily disabling `ApplyFilters`: 6 of 7 failed. Run with
`dotnet test apps/api/tests/SupportDesk.UnitTests --filter TicketQueriesFilterTests` or `npm test`.
SQLite's text matching is case-sensitive, unlike SQL Server's default collation, so the search
tests use matching case.

**AI usage (Task 1.2).** Used an AI assistant to design the predicate approach and write the
code. Verification: I called the API with each filter, checked `totalCount` and `totalPages`
against the filtered results, and read the generated SQL. Issues along the way: its first
edit instructions led me to a duplicated `GetPagedAsync` method that did not compile, which
I fixed by replacing the whole file; and `dotnet build` failed with "file is locked" errors
because the API was still running, so I stopped the API and restarted it with `npm run api`
(which compiles the code).

## Task 2: Ticket escalation and assignment (in progress)

### Where the rules live
All business rules are plain classes in `SupportDesk.Domain/Triage`. They have no database, HTTP or
clock dependency, so they are unit-tested directly and are shared by creating and escalating a
ticket (one place, no duplicated rules).

| Class | Rules | Responsibility |
| --- | --- | --- |
| `SlaPolicy` | BR-2, BR-3, BR-6 | Window for a priority and customer tier: base hours, premium multiplier, 1 h floor. Bound from the `Sla` configuration section. |
| `PriorityRules` | BR-1, BR-7 | Initial priority (requested, default Medium, forced Critical by category flag) and one-level escalation. |
| `AgentAssignment` | BR-4, BR-5, BR-7 | Eligibility (active, specialised where the category requires it, strictly below `MaxOpenTickets`), fewest open tickets, lowest id as the tie-break, keep-the-current-agent on escalation. |
| `TicketTriage` | all of the above | Combines them into one `TriageDecision` (priority, due date, owner, and a human-readable reason for each). |

### Rules as understood
- Priority: the requested one, Medium by default; a category with `ForcesCriticalPriority` is always Critical.
- Due date = time of triage + window. Premium customers get `PremiumCustomerMultiplier` (0.5) of the
  window, never below `MinimumWindowHours` (1 h).
- Only a category with `RequiresSpecialist` restricts the owner to agents with that specialization.
  No category names are hard-coded: behaviour comes from the category flags.
- Nobody eligible is a normal outcome: the ticket is created unassigned and the reason says why.
- Escalation raises priority one level and recomputes the due date from the moment of escalation. The
  current agent is kept while still eligible, otherwise the ticket is reassigned as above. Critical
  tickets cannot be escalated.

### Design decisions
- **Chosen: pure rule classes plus a thin handler.** The handlers load data and save; the decisions are
  made in `TicketTriage`. Rejected: putting the rules in the controllers or the handlers (they would be
  duplicated between create and escalate and could only be tested with mocks and a database).
- **Chosen: configuration in the existing `Sla` section** (`appsettings`), bound to `SlaPolicy`, with
  no defaults in code, so the configuration is the single source of truth and a missing priority fails
  loudly. Rejected: constants in code (BR-6) and a settings table (more than the task needs).
- **Chosen: agents are passed in as plain `AgentCandidate` values** (id, limits, open-ticket count,
  specialization ids) so the assignment rule needs no repository and tests are simple.
- **Assumption (escalation):** when a ticket is escalated, its own open-ticket count is excluded from
  the current agent's load. Otherwise an agent at their limit would look ineligible for a ticket they
  already hold.
- **Assumption (ties):** the lowest agent id wins a tie, which makes the choice deterministic.

### Tests
`TicketTriageTests` (xUnit, `SupportDesk.UnitTests/Domain/Triage`) cover: requested/default/forced-Critical
priority, windows for every priority and tier, the 1 h premium floor, a missing configured window, fewest
open tickets, inactive agents, the limit boundary, specialists, ties, nobody eligible (still triaged),
one-step escalation, due date from now, Critical rejected, and keeping or changing the agent on escalation.

*Still to add here: SLA status (BR-8), the API endpoints, the migration and its index, and the UI.*
### Escalation and SLA status (domain and database)

**Escalation on the aggregate.** `Ticket.Escalate(...)` is the only way to escalate. It refuses a
resolved/closed or Critical ticket, checks the new priority is exactly one level up, then updates
priority, due date, SLA window and owner and adds one `TicketEscalation` history row in the same
operation. The *values* (new priority, due date, owner) come from `TicketTriage`, so create and
escalate share one set of rules; the aggregate only guards that the change is allowed.
`TicketEscalation` has an internal constructor and private setters, so a history row can be
created but never edited.

**SLA status (BR-8) is derived, never stored.** `SlaEvaluator.Evaluate(due, resolved, now,
windowMinutes, atRiskPercent)` returns NotApplicable, Met, Breached, AtRisk or WithinSla. "Now" is a
parameter, so tests pass a fixed time. Boundaries: exactly 25% of the window left is AtRisk; due
exactly now is AtRisk (not yet breached); resolved exactly at the due date is Met.

**Why `SlaWindowMinutes` is stored.** "At risk" means 25% or less of the *window* remains. The window
is not always due minus created, because escalation restarts it from the moment of escalation. So the
window length is stored next to the due date, set whenever the due date is set (triage and
escalation). Existing rows are backfilled by the migration (`DATEDIFF(minute, CreatedAtUtc, DueAtUtc)`)
and the seeder sets it for demo tickets. The status itself is still computed from these columns.

### Database
Migration `AddTicketEscalations` adds:
- `Tickets.SlaWindowMinutes` (int, nullable: null when the ticket has no due date).
- Table `TicketEscalations`: `Id`, `TicketId` (FK), `FromPriority`/`ToPriority`, `FromAgentId`/`ToAgentId`
  (nullable FKs to Agents), `FromDueAtUtc` (nullable)/`ToDueAtUtc`, `Reason` (500), `EscalatedBy` (100),
  `EscalatedAtUtc`.
- **Index `IX_TicketEscalations_TicketId_EscalatedAtUtc`** (my choice). The history is always read as
  "one ticket's rows, newest first", so one composite index serves the foreign key and the sort and
  avoids a separate sort step. A plain `TicketId` index would only cover the lookup.
- EF Core also generated plain indexes on `FromAgentId` and `ToAgentId` (it indexes foreign keys by
  default). I kept them; they are small and I did not choose them deliberately.
- **Delete behaviour: Restrict** on every foreign key of the history table. It is an audit record, so
  deleting a ticket or an agent must not silently delete or null out its history. Agent ids are
  nullable because a ticket can have no owner before or after an escalation.
- Reads are projected with `AsNoTracking` and no navigation loading, so there is no N+1.

### Tests (this part)
- `TicketEscalationTests`: one-step priority increase, window restarted, due date from now, owner
  changed or removed, correct history row (before/after values, trimmed reason and actor), repeated
  escalation recorded step by step, Critical rejected, Resolved/Closed rejected, more than one level
  rejected, reason and actor required.
- `SlaEvaluatorAtRiskTests`: every BR-8 outcome at its exact boundary (25% left, a second more, due
  exactly now, one tick late, resolved exactly on time, resolved late, no window).
- The existing `SlaEvaluatorTests` still pass unchanged: the two new parameters are optional.
- `TheModel_MatchesTheLatestMigration` (architecture test) confirms the model and the migration agree.

*Still to add here: the API endpoints, the `slaStatus` filter, the UI, and the manual checks.*