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