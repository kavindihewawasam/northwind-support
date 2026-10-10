# Approach

How I approached each task, in the order I did it: what I read, what I tried, what I decided and why.
The README has the reference material (rules, endpoints, database, tests); this file is the thinking.

## How I worked

For every task I did the same four things: read the brief for the hard parts (not just the list of
features), looked at how the existing code is organised so mine would fit, wrote the tests that pin the
behaviour down, and only then wired things together. I used an AI assistant throughout (see the README),
but I ran everything myself and kept only code I could explain.

---

## Task 1: Defect fix and server-side filtering

### 1.1 DEFECT-117

**Reproducing.** I opened the ticket list with the browser's Network tab open. Changing the Status
dropdown sent no request. Typing in the search box then sent one that included the status. That pattern
told me the filters were stored correctly and something was not reacting to them.

**Finding the cause.** In `useTicketList.ts` the effect that loads tickets only depended on `search`,
`page` and `pageSize`. Every other filter was missing from the dependency list, and an `eslint-disable`
comment had silenced the warning with a wrong explanation (it said the `filters` object changes identity
every render, but it comes from `useState`).

**Proving it first.** Before changing the hook I wrote tests for each filter. On the original code 7 of
them failed with "expected 2 calls, got 1", which confirmed the diagnosis and gave me a regression test
that fails on the original code and passes on the fix.

**The fix, and a wrong turn.** My first fix put the whole filters object in the request but debounced it
together with the search text, which sent a stale request on every keystroke; the tests caught it. The
second version split the state in two: `filters` (what the inputs show) and `query` (what requests are
built from). Only search-text changes are delayed by 300 ms; everything else updates `query` immediately.
That version failed the `set-state-in-effect` lint rule, so `isLoading` is now derived (loading means the
current query has no answer yet) and stale responses are ignored with a cleanup flag.

**What I rejected.** Forcing a reload, a remount key or filtering in memory: they hide the effect instead
of fixing the cause, and the brief says so. Listing every filter by hand in the dependency array: it is
easy to forget one for the next filter. Because requests are built from the whole filters object, the
Task 2 SLA filter needed no change in the hook.

### 1.2 Server-side filtering

**Approach.** The existing query already joined tickets with customer, category and agent, so I filtered
that query instead of adding a second one. Each supplied filter becomes one predicate, the predicates are
chained with `Where` (AND), and the count and the page both read from the filtered query, so the pager is
correct.

**Why predicates.** One large `Where` full of `(x == null || ...)` clauses grows with every filter and is
hard to read. A list of predicates means the next filter (Task 2's SLA status) is one added block, which
is what requirement FL-6 asks for.

**Checking.** I called the API with each filter, compared `totalCount` and `totalPages` with the visible
items, tried a blank search (ignored), and read the SQL EF Core logs to confirm the `WHERE` runs in the
database before counting and paging. The tests run the real query against in-memory SQLite. To make sure
they mean something, I temporarily disabled the filtering: 6 of the 7 tests failed.

---

## Task 2: Ticket escalation and assignment

### Reading the brief

The brief says the rules are simple "and the challenge is where they are placed". That told me to design
for one thing first: creating and escalating a ticket must share the same decisions, otherwise the two
paths drift apart.

### Where the rules go

I made a small `Triage` folder in the domain with four classes: SLA window, priority, agent assignment,
and a `TicketTriage` that combines them. Nothing in it knows about the database, HTTP or the clock. The
handler gives it plain values (the category's flags, whether the customer is Premium, "now", and the
agents with their open-ticket counts) and gets a decision back, including a reason in words. That keeps
each rule testable on its own, and "no eligible agent" is just a decision with no owner, not an error.

### Configuration

The `Sla` section was already in `appsettings.Development.json`, so I bound it to `SlaPolicy` instead of
adding constants. I left out default values on purpose: if a priority is missing from the configuration
the code throws a clear error instead of quietly using a hidden number.

### Edge cases I thought about

- A premium window must not drop below 1 hour (a 1.5 h critical window halved would be 45 minutes).
- The limit is strict ("strictly below"), so an agent holding exactly their maximum is not eligible.
- On escalation the ticket already counts towards its own agent's load. If that agent is exactly at their
  limit, counting it would make them ineligible for their own ticket and it would be reassigned for no
  reason. So the inputs query can leave one ticket out of the count. I found this by writing the "keep
  the current agent" test with an agent at the limit before writing the query.
- A Critical ticket cannot be escalated further, and Resolved/Closed tickets cannot be escalated at all.
- A ticket with no due date has no window, so it can never be AtRisk.

### The SLA window problem (BR-7 and BR-8)

BR-8 says a ticket is at risk when 25% or less of "the window" remains. BR-7 restarts the window on
escalation, so I cannot get the window from due date minus creation time. I considered three options:
store a "window start" timestamp, store the window length, or recompute it from priority and tier each
time. Recomputing breaks as soon as the configuration changes (old tickets would be judged by new
numbers), and a start timestamp needs a subtraction everywhere. I stored the window length next to the
due date, set at the same moment. The SLA status itself is still never stored, as BR-8 requires.

### Escalation and history

I put `Escalate` on the `Ticket` aggregate so the rules "Critical and Resolved/Closed cannot be
escalated" and "exactly one level up" cannot be bypassed by any caller. It does not decide the new owner
or due date: that needs the list of agents, which an aggregate should not load, so the triage rules
decide and the aggregate checks and records. The history row is created inside `Escalate`, so a ticket
can never change priority without leaving a record.

The history rows are immutable (no setters, internal constructor) and every foreign key uses Restrict,
because deleting history when a ticket or agent is removed would defeat its purpose. I added one
composite index (`TicketId`, `EscalatedAtUtc`) because the only question asked of this table is "this
ticket's history, newest first".

### Wiring into create and escalate

The rules needed the category flags, the customer tier and each agent's workload, and none of the
existing repository interfaces returned those. I added one small read-only interface (`ITriageInputs`)
instead of stretching the repositories: it is a real seam (the database on one side, the rules on the
other) and the tests replace it with a mock. The agent workload is one subquery, not a count per agent.

Escalate returns 404 for an unknown ticket first, then 409 for "cannot escalate" with a message that
names the reason, then 400 for a bad reason (the validation filter runs before the handler). A caller
always learns the most fundamental problem first.

### The `slaStatus` filter

It had to run in SQL (filtering, sorting, paging and the count), so I could not reuse the C#
`SlaEvaluator`. I wrote `SlaPredicate` as the SQL form of the same rule, built from the same columns,
and put it behind the `Predicates` extension point from Task 1. Adding it changed nothing else in the
query. The price is two forms of one rule, so I tested the boundaries on the C# side and checked the SQL
on a real SQL Server by calling each status and confirming the five counts add up to the total. SQLite
has no `DATEDIFF`, so AtRisk and WithinSla are not covered by the automated database tests; I say so in
the README instead of hiding it.

### Frontend

**One indicator, one table.** The brief says state must not be conveyed by colour alone and that one
indicator should serve list and detail. `slaPresentation` (label, symbol, colour per state) is the only
place the states are named, used by the badge and by the filter options. A test checks that every label
and symbol is distinct.

**The escalation form.** The limits (5 to 500, 100) appear once as constants and match the API validator.
The form is not rendered when the ticket is already Critical or resolved/closed, because a disabled form
invites "why?"; the message says why. I kept the server-rejection path anyway, since the page can be
stale, and tested it with a mocked 409.

**Refreshing.** The escalate endpoint returns the updated ticket, so I reuse it instead of re-fetching;
only the history needs another request.

### What I would do with more time

- Define the SLA rule once (for example in a database view or computed column) instead of in C# and SQL.
- Add a database-level check that the five SLA statuses partition the tickets.
- Lock or use a transaction around agent selection so two simultaneous tickets cannot pick the same agent.
- Show a short confirmation of the new priority before an escalation is submitted.

---

## Task 3: Login and authentication

TODO: write this after Task 3 (or write "not finished" and what is left). Cover: where credentials are
stored and why (column on Agent or a Users table), the hashing choice, token lifetime, where the token is
kept in the browser and the trade-off, how `escalatedBy` moved to the token, and how the 401 handling works.

## Task 4: Dockerise the API

TODO: write this after Task 4 (or write "not finished"). Cover: the multi-stage build and the restore
cache, running as a non-root user, where configuration and secrets come from, how migrations run here and
how they would run in a real deployment, and the image size.