
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

### Triage on create (BR-1..BR-6)
`RaiseTicketCommandHandler` loads the category flags, whether the customer is Premium and every
agent's open-ticket count, asks `TicketTriage` for a decision, then creates the ticket with
`Ticket.Raise` + `ApplyTriage`. The response is the ticket plus a `triage` object: reason for the
priority, SLA window in minutes with its reason, and the reason for the assignment. If nobody is
eligible the ticket is still created (201), unassigned, and the assignment reason says why.

### Escalation endpoints (BR-7)
`EscalateTicketCommandHandler` checks the ticket exists (404), then that it is open and not Critical
(409 with a message naming which), loads the same inputs as create, asks `TicketTriage.ForEscalation`
and calls `Ticket.Escalate`. The reason is validated before the handler runs (400): trimmed, 5-500
characters; `escalatedBy` is required, max 100. Until Task 3, `escalatedBy` comes from the request body.

### `slaStatus` filter in SQL
`slaStatus` is one more predicate in `TicketQueries.Predicates` (the extension point from Task 1): no
change to counting, sorting or paging. `SlaPredicate` is the SQL form of `SlaEvaluator`, built from
the same columns (`DueAtUtc`, `ResolvedAtUtc`, `SlaWindowMinutes`). "At risk" compares
`DATEDIFF(second, now, due)` with a share of the stored window, so it uses SQL Server's `DATEDIFF`.
The list and detail DTOs compute `SlaStatus` with the same `SlaEvaluator` and the same configured
threshold. Because the SQL and the C# rules are two forms of one rule, the boundary tests exist on both
sides (see Tests).

### Design decisions (this part)
- **`ITriageInputs`, a read-only seam for the triage inputs.** It returns the category flags, the
  customer tier and the agent candidates (id, limits, open-ticket count, specialization ids). The rules
  stay free of EF Core and the handlers stay thin. Rejected: loading agents and tickets through the
  repositories and counting in C# (an N+1 on agents' open tickets), and adding methods to the existing
  repository interfaces (it widens aggregates' repositories with query-shaped methods).
- **Same code path for create and escalate.** Both build a `TriageContext` and call `TicketTriage`;
  neither contains a rule. Rejected: re-implementing assignment inside the escalate handler.
- **Escalating excludes the ticket's own load.** `GetAgentCandidatesAsync(excludingTicketId)` leaves
  the ticket being escalated out of its agent's open count, otherwise an agent exactly at their limit
  would count as ineligible for a ticket they already hold.
- **409 for "cannot escalate", checked in the handler with a specific message.** The aggregate also
  enforces it (defence in depth), but the handler checks first so the message can say Critical or
  Resolved/Closed. Rejected: letting the domain exception through (its text is the same, but the
  ordering of 404/409 checks would be less obvious).
- **Manual assignment is unchanged.** `PATCH /assignment` stays the team lead's override and does not
  apply the specialist rule (as it did before Task 2).
- **`SlaPolicy` is bound from configuration once and registered as a singleton**, so a missing priority
  window fails loudly rather than using a hidden default.

### Assumptions
- Escalation always re-evaluates the owner: keep the current agent if still eligible, otherwise choose
  again; if nobody is eligible the ticket becomes unassigned and the history row records that.
- The due date after escalation is *now* + the new window (not creation time + window).
- Seed tickets get `SlaWindowMinutes` from their seeded window, so the demo data shows AtRisk tickets.

### Tests (this part)
- `RaiseTicketTriageTests`: a Premium Security ticket requested as Low becomes Critical with a 2 h
  window and the least-loaded specialist (a full agent and a non-specialist are skipped); with nobody
  eligible the ticket is still created, unassigned, with a reason.
- `EscalateTicketCommandHandlerTests`: unknown ticket (404), Critical (409, message), Resolved and
  Closed (409, message names the status), priority raised, due date from now, history row written,
  agent kept while eligible and changed when not, own load excluded.
- `TicketQueriesSlaFilterTests` (SQLite): NotApplicable, Met and Breached, the filter combined with
  another filter, and the count/pages for the filtered set.
- **Deliberately not automated:** the AtRisk and WithinSla filters, because they use SQL Server's
  `DATEDIFF`, which SQLite does not have. Their boundary logic is covered in `SlaEvaluatorAtRiskTests`;
  the SQL was checked by hand against the SQL Server container (see below).

### Manual checks (SQL Server, Swagger)
- `POST /api/tickets` (customer 1 Contoso/Premium, category 5 Security, requested Low): [201, priority
  Critical, due [..] ≈ 2 h ahead, assigned [Priya Nair], triage reasons present]
- Billing ticket for Fabrikam: [assigned to Sara Lindqvist]
- `POST /api/tickets/{id}/escalate`: [200 Medium→High, due ≈ 8 h ahead]; again → Critical; a third time →
  [409 "already Critical"]; reason `abc` → [400]; ticket 9999 → [404]; ticket 25 (Resolved) → [409].
- `GET /api/tickets/{id}/escalations`: [newest first, before/after values correct]
- `GET /api/tickets?slaStatus=AtRisk` (and the other four): [only matching tickets; the five counts add
  up to totalCount]. The SQL log shows `DATEDIFF(second, ...)` in the `WHERE`.

### Known limitations (this part)
- `escalatedBy` is a plain string from the body until Task 3 replaces it with the signed-in user.
- Two tickets created at the same moment can read the same agent loads and pick the same agent
  (no locking); acceptable for a single-instance demo, a transaction or a queue would fix it.
- `NextReferenceAsync` still uses "highest id + 1" (existing behaviour).