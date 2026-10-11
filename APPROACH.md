# Approach

How I approached each task, in the order I did it: what I read, what I tried, what I decided and why.
The README has the reference material (rules, endpoints, database, tests, how to run); this file is the
thinking.

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
the code throws a clear error instead of quietly using a hidden number. (In Task 4 I moved the section
into `appsettings.json`, because a container that does not run as Development never loads the
Development file.)

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

**The escalation form.** The limits (5 to 500) appear once as constants and match the API validator. The
form is not rendered when the ticket is already Critical or resolved/closed, because a disabled form
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

### Reading the brief

Nothing existed: no users, no tokens, every endpoint open. The parts I thought were hard were not the
login form but the details around it: not leaking whether the email or the password was wrong, protecting
*every* endpoint in one place, keeping the signing key out of code, and making `escalatedBy` impossible
to fake. On the frontend I had one advantage: every call already goes through a single `request`
function, so attaching a token and reacting to a 401 each need to be written once.

### Where credentials live

I added a nullable `PasswordHash` column to `Agent`. The alternative was a separate `Users` table, but the
agents are the only users, each has exactly one login and there are no roles, so a second table would be
a join with nothing to justify it. The column is nullable so that an agent without a password simply
cannot sign in, which is also what happens to the inactive agent.

### Hashing

I used ASP.NET Identity's `PasswordHasher` (PBKDF2, a new random salt for every password, the iteration
count stored with the hash). I considered BCrypt, which needs an extra package for no real gain, and
writing PBKDF2 myself, which is exactly the "home-made" thing the brief rules out. The hasher sits behind
a small `IPasswordHasher` interface so the login handler can be tested with a mock, and the real hasher has
its own tests (the hash is not the password, two hashes of the same password differ, verify passes and
fails correctly).

### One message for every failure

Login returns the same 401 and the same words for an unknown email, a wrong password and an inactive
agent. That is not enough on its own: an unknown email would still return faster, because there is no
hash to check. So the hasher always does the work, against a dummy hash when there is no real one, and a
test checks that the check really runs for an unknown email. I also capped the password at 128 characters
so nobody can make the server hash an enormous string.

### Where the code lives

The login handler and the interfaces are in the application layer; the hasher is in infrastructure; the
token creation is in the web project, because that is the only project that references the JWT package.
That avoided adding a package to a second project.

### Enforcing it centrally

I used a fallback authorization policy ("must be authenticated") instead of `[Authorize]` on each
controller. With attributes, a new controller that forgets one is silently open; with the fallback policy
the default is closed and the exceptions, login and later `/health`, have to say `AllowAnonymous`.
Swagger is middleware rather than an endpoint, so it stays open without extra work.

### The signing key and the lifetime

The key comes from configuration. Development has a clearly-labelled fixture in
`appsettings.Development.json`, in the same way the database password already was; any other environment
must set `Jwt__SigningKey`, and the API refuses to start without a key of at least 32 bytes, so a missing
key is a loud startup error instead of a weak default. I chose a 60-minute lifetime. There are no refresh
tokens (the brief says not to build them), so the lifetime is also the session length: short enough to
limit what a stolen token can do, long enough not to interrupt normal work, and it is one setting.

### A problem I would have missed

My first idea was to hash the development password in the existing seeder. But that seeder returns early
when tickets already exist, so a database that was seeded before login existed (like mine) would have
agents with no passwords and no way in. I added a separate `AgentCredentialSeeder` that runs on every
start (in Development, and later in the Docker demo) and only fills in agents that have no password.

### Who escalated

Before Task 3, `escalatedBy` was a free-text field. Now it is not in the request at all: the controller
reads the agent's name from the token and passes it to the handler. A client can no longer record someone
else's name; I tested this by sending a different name in the body. Agent names can be 200 characters but
the history column is 100, so the handler cuts it to fit, with a test.

### Frontend

- **Where the token lives.** `sessionStorage`: it survives a reload but not a closed tab. Memory only would
  log you out on every refresh, and `localStorage` keeps the token around longer for no benefit here. The
  honest downside is that a script injected into the page could read it; an httpOnly cookie avoids that
  but brings CSRF handling, which seemed more than the task asked for. I wrote the trade-off in the README.
- **One place for the token and for 401s.** The API client attaches the token and, on any 401 other than
  the login call, clears the session and tells the app. The login call is excluded because a 401 there just
  means "wrong password"; treating it as an expired session would have been a bug.
- **Redirects without effects.** The provider holds the session in state; `RequireAuth` redirects when it is
  empty and passes the page the visitor wanted. So an expired token, a manual logout and a first visit all
  go through the same path, and signing in returns you where you were headed.
- **Expired sessions.** When the session is read, one that has already expired is ignored and removed, so a
  stale tab does not look signed in.

### Testing

I tested the login handler with mocks (wrong password gives the error and no token, unknown email gives
the identical error, inactive agent, trimmed email), the real hasher on its own, and on the web the client
(token attached, 401 ends the session, rejected login does not), the session expiry, the route guard and
the login form. The one thing I did not automate is "401 without a token, 200 with one" through the real
pipeline; I checked it by hand. An integration test with `WebApplicationFactory` would be my next step.

### What I would do with more time

- Keep the token in an httpOnly cookie, or at least check on each request that the agent is still active
  (today a deactivated agent's token keeps working until it expires).
- Rate-limit and lock out repeated failed logins.
- Add an Authorize button to Swagger and an integration test for the 401/200 behaviour.
- Never seed a known password outside a local run, and keep the signing key in a secret store.

---

## Task 4: Dockerise the API

### Reading the brief

The Dockerfile itself is short. The parts that need thought are what must *not* be in the image, and what
has to change in the app so it can run as something other than Development. Locally the API migrates,
seeds and finds its settings because it runs as Development with `appsettings.Development.json`. A
container should not depend on that file at all.

### What I had to change in the app

Three small things:

- The `Sla` section only existed in the Development file. It is a business setting needed in every
  environment, so I moved it to `appsettings.json` (still overridable by environment variables).
- Migrating and seeding on start was tied to Development. I added a `Database:MigrateAndSeedOnStartup`
  switch, so the container can run as Production (Swagger off) and still do it as a demo. Development
  behaves as before.
- `/health` did not exist. It tests the database connection and is mapped with `AllowAnonymous()` in
  `Program.cs`, so it is configured centrally with the rest of the pipeline and the fallback policy still
  protects everything else. It returns only status words, so an unauthenticated caller can never read a
  connection string or an exception message.

### Keeping secrets out

I excluded `appsettings.Development.json` from the *published output* in the project file. That means it
cannot end up in an image however the image is built, which is stronger than relying on `.dockerignore`
alone (the `.dockerignore` still excludes `.env` and the rest). I checked it by listing the files inside
the running container. The connection string, signing key and environment name come from environment
variables in the compose file. Compose does have throwaway local defaults for the key and the demo
password, the same way the database password already had one; I wrote that down in the README as a
demo-only convenience, overridable in `.env`.

### The restore layer

The restore step copies only `global.json`, the two shared props files and the four project files, and
restores before the source is copied. I considered copying everything and restoring once, which is simpler
but repeats the restore on every code change. I checked that it works by editing a `.cs` file and
rebuilding: the restore step came from cache and only publish ran again.

### Non-root, port and certificates

The runtime image already has a non-root user, so I switched to it with `USER` and kept plain HTTP on
8080: HTTPS in the container is out of scope and the brief says not to ship certificates. I published it
on host port 5080 so it does not clash with the local API on 5043. `whoami` inside the container prints
`app`.

### Compose

The `api` service is built from the Dockerfile, reaches SQL Server by its service name (not `localhost`,
which inside a container would be the API itself), and starts only when SQL Server reports healthy, using
the health check that was already on the `sqlserver` service. I left `npm run db:up` alone: it starts only
`sqlserver`, so local development is unchanged.

### Migrations in real life

The container migrates itself only because the brief wants one command to produce a working system. In the
README I wrote down why I would not do that in production (several instances racing to migrate, an
over-privileged database login at run time, a failed migration stopping every instance, and demo data and
a known password in production) and what I would do instead: an idempotent script or a migration bundle,
run as a separate pipeline step with a privileged account, while the API runs with a least-privilege
login.

### What I would do with more time

- A container `HEALTHCHECK` (the runtime image has no `curl`, so it needs a tiny probe or a different
  image) and a split between "alive" and "ready" endpoints.
- A smoke test in CI that builds the image and calls `/health`.
- Take the demo password and demo signing key out of the compose defaults, so the demo has to be given
  them explicitly.