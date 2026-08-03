# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Rubato is a single-user daily time-tracking app: a Blazor Server (.NET 10) web app backed by a local SQLite database. You navigate day-by-day, add entries, and each entry's worked hours are computed from a free-text time field.

## Commands

Run from the repo root (the solution file is `rubato.slnx`).

```bash
dotnet run --project Rubato        # run locally (Development profile, http://localhost:5218)
dotnet build rubato.slnx           # build
dotnet watch --project Rubato      # hot-reload during development
```

Entity Framework migrations (run from the `Rubato/` directory):

```bash
dotnet ef migrations add <Name>    # create a migration after changing Data/Models
dotnet ef database update          # apply migrations manually (normally not needed — see below)
```

There is no test project.

## Architecture

**Rendering.** Blazor Server with the `InteractiveServer` render mode (set in `Pages/App.razor`). There is no WebAssembly/client project and no HTTP API — components call services directly in-process.

**Two-layer model mapping.** EF entities live in `Data/Models/` (`Entry`, `Project`). UI-facing view models live in `Models/` (`EntryModel`, `ProjectModel`). Services return view models, never entities.

**Services** (`Services/`, registered transient in `Program.cs`) are the only code that touches `RubatoDataContext`. Components inject `EntryService`/`ProjectService` and never query the `DbContext` directly. Each service method creates its own short-lived context from `IDbContextFactory<RubatoDataContext>` — never hold a context across calls. A plain scoped `DbContext` would live for the whole Blazor circuit, so every component in the session would share one context and collide on it whenever two operations overlap.

**Database lifecycle.** SQLite, registered via `AddDbContextFactory`. `db.Database.Migrate()` runs at startup in `Program.cs`, so migrations apply automatically on launch — you rarely need `dotnet ef database update`. The DB file path comes from the `DataPath` config value (defaults to `Database/`; the Docker image sets it to `/etc/rubato`, a mounted volume). `RubatoDataContext` also stores ASP.NET Data Protection keys (`IDataProtectionKeyContext`); Data Protection and the startup migration resolve the context from a scope, which is why `Program.cs` keeps a scoped registration alongside the factory.

**Time computation is the domain crux.** An entry's `Time` is a multi-line free-text field (e.g. `9-10:30`, one range per line). `EntryModel.ParseTime()` parses each line with `TimeRegex` and returns both the summed hours and the lines it could not read.

`EntryModel.Duration` is **derived on every read** from that parse — a get-only property, not a stored field. Nothing in the UI reads the `Duration` column; services write it on save purely as a denormalized copy. This is deliberate: when `Duration` was a settable field refreshed only by the Time field's own `@bind:after`, the day total (summed from the stored value) could disagree with the red "could not be read" warning (computed live), so a row could be flagged invalid *and* still contribute hours. Any change to the parser must be paired with `EntryService.ReconcileDurationsAsync()`, which runs at startup and rewrites stored durations that no longer follow from their time text — rows written by an older parser held stale and even negative hours. It is idempotent, so it costs one query per launch once converged.

Rules the parser holds to, all of them load-bearing:

- **The 12-hour assumption.** An end *time* before the start time means the afternoon, so `9-5` is 9am–5pm and `9:30-9` is 9:30am–9pm. Compare whole times, not just hours: comparing hours alone rejected `9:30-9` while accepting the equivalent `9:30-8`, purely because the hours happened to tie. An end hour of 12 or more is already unambiguous and is taken literally, so `14-13` is not "2pm to 1am" — it is backwards, and rejected.
- **An end of just minutes (`7:15-:30`) means the start's own hour**, i.e. 7:15–7:30. The colon is required so that `9-30` stays an out-of-range hour instead of quietly becoming 9:30. The 12-hour assumption does *not* apply to this form — it has named its hour outright — so `7:45-:30` is a typo, not an afternoon. This syntax is in active use in real data; an earlier anchored-regex rewrite dropped it and silently zeroed those rows.
- **Unreadable lines are flagged, never counted as zero.** A line that is not a range (`8am-5pm`, `lunch`), has out-of-range numbers (`9:70-10`, `99-1`), or still runs backwards after the afternoon reading (`14-13`, `12:30-12`, and overnight ranges like `23-1`, which a single day's entry cannot express) is collected in `TimeParseResult.InvalidLines`. `EntryRow` gives the field a red border and a tooltip; `EntryList` warns next to the day total. Silently returning 0 hours here is the bug that made day totals quietly come up short.
- **The cost of the 12-hour assumption is that same-hour typos read as afternoons**: `10:30-10:00` is 11.5 hours, not an error. That follows from the rule rather than being a special case — `10:30-9:45` was already accepted as 11.25 hours — and the inline day total is what surfaces it.
- **Valid lines still count when a sibling line is bad**, so a typo on line 3 does not erase lines 1 and 2.
- **A range with no end yet (`9-`) is in progress**: readable, worth 0 hours, not an error.
- **Hours and minutes are validated before any `TimeSpan`/`DateTime` is built.** Feeding unvalidated regex captures into `new DateTime(...)` is what used to throw `ArgumentOutOfRangeException` from an event handler and kill the circuit.
- **The parse is cached per `Time` value.** The `Time` setter clears it; a single render reads `Duration` and `InvalidTimeLines` several times over.

`Pages/App.razor` carries the standard `#blazor-error-ui` element, so if an unhandled exception does take the circuit down the user sees a message instead of a silently frozen page.

**Component conventions.**

- Components that issue async/DB calls inherit `CancellableComponentBase` (`Components/Shared/`), which exposes a `CancellationToken` tied to the component lifetime and cancels it on dispose. Pass this token through to service calls.
- Saving is inline and immediate: `EntryRow.razor` uses `@bind:after` to persist each field on change, then raises `OnEntryChanged` so the parent (`Pages/Day.razor`) reloads and recomputes the day total.
- **Every `await` in an event handler is wrapped.** An exception escaping a handler takes the whole circuit down, so `EntryRow`/`ProjectRow` catch `OperationCanceledException` (the component is being disposed — return before `StateHasChanged`, and do not notify the parent) and surface anything else as a per-row warning icon, always clearing the busy flag in a `finally`. Leaving `IsSaving` stuck on a throw is what permanently disabled a row's delete button.
- **Update services return `bool`, not throw, when the row is gone.** `UpdateEntryAsync`/`UpdateProjectAsync` use `FirstOrDefaultAsync`; a save racing a delete in another tab is a normal outcome, not an error the user should see.
- **Busy flags guard re-entrancy, not just cosmetics.** A `disabled` attribute is a client-side hint and clicks queued before the flag's first render still arrive, so handlers whose work is not idempotent re-check before touching the database — `Day.CopyEntriesAsync` re-tests `CanCopy` because the entry count it depends on does not change until the copy finishes, and without that a double-click copied the previous day twice.
- `Pages/` holds routable pages; `Components/` holds reusable components (`Components/Shared/` for cross-cutting ones). Root routes (`/`, `/day`, `/day/{date}`) all resolve to `Pages/Day.razor`.

**Styling.** Bootstrap 5 + Bootstrap Icons, served locally from `wwwroot/lib/`. Light/dark theme follows the OS preference via the inline script in `App.razor` (`data-bs-theme`). Project colors are applied as inline CSS custom properties (`--bs-border-color`).

## Deployment

`Dockerfile` produces a self-contained ASP.NET image listening on port 5161, expecting the SQLite DB on the `/etc/rubato` volume.
