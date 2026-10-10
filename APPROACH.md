# Approach

## Task 1: Defect fix and server-side filtering

### 1.1 DEFECT-117

**Reproducing.** I opened the ticket list with the browser's Network tab open. Changing the
Status dropdown sent no request. Typing in search then sent a request that included the
status. That pattern told me the filters were stored correctly but something was not
reacting to them.

**Finding the cause.** In `useTicketList.ts` the effect that loads tickets only depended on
`search`, `page` and `pageSize`. Every other filter was missing from the dependency list, and
an `eslint-disable` comment had silenced the warning with a wrong explanation (it claimed the
`filters` object changes identity every render, but it comes from `useState`).

**Proving it first.** Before fixing anything I wrote regression tests for each filter. On the
original code 7 of them failed with "expected 2 calls, got 1", which confirmed the diagnosis.

**The fix and a wrong turn.** My first fix put the whole filters object in the request but
debounced it with the search text, which sent a stale request on every keystroke. The tests
caught it. The second version split the state in two: `filters` (what the inputs show) and
`query` (what requests are built from). Only search-text changes are delayed by 300 ms;
everything else updates `query` immediately. It then failed the `set-state-in-effect` lint
rule, so `isLoading` is now derived (loading = the current query has no answer yet) and stale
responses are ignored with a cleanup flag.

**What I rejected.** Forcing a reload, a remount key or filtering in memory: those hide the
effect instead of fixing the cause. Listing every filter by hand in the dependency array:
easy to forget for the next filter. The SLA filter in Task 2 needs no special wiring because
requests are built from the whole filters object.

### 1.2 Server-side filtering

**Approach.** The existing query already joined tickets with customer, category and agent, so
I filtered that query rather than adding a second one. Each supplied filter becomes one
predicate, the predicates are chained with `Where` (AND), and the count and the page both
read from the filtered query, so the pager is correct.

**Why predicates.** One big `Where` with `(x == null || ...)` clauses grows with every filter.
A list of predicates means the next filter (Task 2's SLA status) is one added block.

**Checking.** I called the API with each filter, compared `totalCount`/`totalPages` with the
visible items, tried a blank search (ignored) and read the SQL EF Core logs to confirm the
`WHERE` runs in the database before counting and paging.

## Task 2: Ticket escalation and assignment

**Reading the brief.** The task says the rules are simple and "the challenge is where they are placed".
That told me to design for one thing first: creating and escalating a ticket must share the same
decisions, otherwise the two paths drift apart.

**Where the rules go.** I made a small `Triage` folder in the domain with four classes (SLA window,
priority, agent assignment, and a `TicketTriage` that combines them). Nothing in it knows about the
database, HTTP or the clock: the handler hands it plain values (the category's flags, whether the
customer is Premium, "now", and the agents with their open-ticket counts) and gets a decision back,
including a reason in words. That keeps each rule testable on its own and means "no eligible agent"
is just a decision with no owner, not an error.

**Configuration.** The `Sla` section was already in `appsettings.Development.json`, so I bound it to
`SlaPolicy` instead of adding constants. I left out default values on purpose: if a priority is missing
from the configuration the code throws a clear error rather than quietly using a hidden number.

**Edge cases I thought about.** A premium window must not drop below 1 hour (a 1.5 h critical window
halved would be 45 minutes). The limit is strict ("strictly below"), so an agent holding exactly their
maximum is not eligible. On escalation the ticket already counts towards its own agent's load, so it
has to be excluded or an agent at their limit could never keep their own ticket. A forced-Critical
category cannot be escalated further, so escalation of those tickets is rejected.

**Tests first.** The rules are covered by tests before anything is wired into the API, so when I
connect them to create and escalate I am only testing the plumbing.

*Still to write: SLA status and the at-risk threshold, the endpoints, the migration, the UI.*

### Escalation, history and SLA status

**What the brief forced me to decide.** BR-8 says a ticket is at risk when 25% or less of "the window"
remains. But BR-7 restarts the window on escalation, so I cannot get the window from due date minus
creation time. I considered three options: store a "window start" timestamp, store the window length,
or recompute the window from priority and customer tier each time. Recomputing would break as soon as
the configuration changed (old tickets would be judged by new numbers), and a start timestamp works
but needs a subtraction everywhere. I stored the window length, set at the same moment as the due date.
The SLA *status* is still never stored, as BR-8 requires.

**Where the escalation lives.** I put `Escalate` on the `Ticket` aggregate so the rules "Critical and
Resolved/Closed cannot be escalated" and "exactly one level up" cannot be bypassed by any caller.
It does not decide the new owner or due date itself: that needs the list of agents, which an aggregate
should not load, so the triage rules decide and the aggregate checks and records. The history row is
created inside `Escalate`, so a ticket can never change priority without leaving a record.

**History as an audit trail.** Rows are immutable (no setters, internal constructor) and the
foreign keys use Restrict, because deleting history when a ticket or an agent is removed would defeat
the point of having it. I added one composite index (`TicketId`, `EscalatedAtUtc`) because the only
question asked of this table is "this ticket's history, newest first".

**Edge cases.** The reason and actor are trimmed before they are stored. A ticket can be escalated more
than once, up to Critical. An escalation can end with no owner when nobody is eligible; the history row
records that with a null `ToAgentId`. A ticket with no due date has no window, so it can never be
AtRisk.

**How I checked it.** I wrote the evaluator tests around the exact boundaries first (25% left, one
second more, due exactly now, resolved exactly on the due date) because those are where an off-by-one
would hide. For the migration I read the generated `Up()` before running it, and added one SQL
statement to backfill the window for tickets that already exist, otherwise they could never show as
AtRisk. The architecture test that compares the model with the latest migration confirmed the two
agree.

*Still to write: the endpoints, the SQL `slaStatus` filter, the UI.*

### Wiring the rules into create and escalate

**Plan.** The rules were already tested, so this step was plumbing: get the inputs the rules need,
call them, store the result. I wanted the handlers to contain no business rule at all, so that
reading `RaiseTicketCommandHandler` and `EscalateTicketCommandHandler` side by side shows they are the
same few steps with different decisions behind them.

**The one new abstraction.** The rules need the category flags, the customer tier and each agent's
workload, and none of the existing repository interfaces return those. I added one small read-only
interface (`ITriageInputs`) rather than stretching the repositories, because it is exactly a seam
(database on one side, rules on the other) and the tests replace it with a mock. The agent workload is
one grouped subquery, not a count per agent, so it does not scale with the number of agents.

**The escalation catch.** When a ticket is escalated, its own agent already "holds" it. If that agent is
exactly at their limit, counting this ticket would make them ineligible for their own ticket and the
ticket would be reassigned for no reason. So the inputs query can leave one ticket out of the count.
I found this by writing the "keep the current agent" test with an agent at the limit before writing the
query.

**Ordering the checks.** Escalate returns 404 for an unknown ticket first, then 409 for "cannot
escalate" with a message naming why (Critical, or Resolved/Closed), then 400 for a bad reason before the
handler even runs (the existing validation filter). That order means a caller always learns the most
fundamental problem first.

**The SQL filter.** The SLA filter had to run in the database, including sorting and paging, so I could
not reuse the C# `SlaEvaluator`. I wrote `SlaPredicate` as the SQL form of the same rule, built from the
same columns, and put it behind the `Predicates` extension point I made in Task 1: adding it changed
nothing else in the query, which is what FL-6 asked for. The price is two forms of one rule, so I
tested the boundaries on the C# side and checked the SQL on a real SQL Server by calling each status
and confirming the five counts add up to the total. SQLite has no `DATEDIFF`, which is why AtRisk and
WithinSla are not covered by the automated database tests; I have said so in the README rather than
hide it.

**What I would do with more time.** Move the SQL/C# duplication to a single definition (for example a
database view or a computed column) and add a database-level check that the five statuses partition the
table.