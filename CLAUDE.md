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

**Two-layer model mapping.** EF entities live in `Data/Models/` (`Entry`, `Project`). UI-facing view models live in `Models/` (`EntryModel`, `ProjectModel`). Services return view models, never entities. Each view model owns its own mapping — `EntryModel.FromData`/`ToData`, `ProjectModel.FromData` — so services project through those rather than listing fields inline. `ToData` is what makes a copied entry pick up a freshly parsed `Duration` instead of inheriting the old row's stored one.

**Services** (`Services/`, registered transient in `Program.cs`) are the only code that touches `RubatoDataContext`. Components inject `EntryService`/`ProjectService` and never query the `DbContext` directly. Each service method creates its own short-lived context from `IDbContextFactory<RubatoDataContext>` — never hold a context across calls. A plain scoped `DbContext` would live for the whole Blazor circuit, so every component in the session would share one context and collide on it whenever two operations overlap.

**Database lifecycle.** SQLite, registered via `AddDbContextFactory`. `db.Database.Migrate()` runs at startup in `Program.cs`, so migrations apply automatically on launch — you rarely need `dotnet ef database update`. The DB file path comes from the `DataPath` config value (defaults to `Database/`; the Docker image sets it to `/etc/rubato`, a mounted volume). `RubatoDataContext` also stores ASP.NET Data Protection keys (`IDataProtectionKeyContext`); Data Protection and the startup migration resolve the context from a scope, which is why `Program.cs` keeps a scoped registration alongside the factory.

**Time computation is the domain crux.** An entry's `Time` is a multi-line free-text field (e.g. `9-10:30`, one range per line). `EntryModel.ParseTime()` parses each line with `TimeRegex` and returns both the summed hours and the lines it could not read.

`EntryModel.Duration` is **derived on every read** from that parse — a get-only property, not a stored field. Nothing in the UI reads the `Duration` column; services write it on save purely as a denormalized copy. `EntryService.GetWeekTotalAsync` sums its Monday-start week the same way, through the parser rather than the stored column, so the week total cannot disagree with the day totals it is made of. This is deliberate: when `Duration` was a settable field refreshed only by the Time field's own `@bind:after`, the day total (summed from the stored value) could disagree with the red "could not be read" warning (computed live), so a row could be flagged invalid *and* still contribute hours. Any change to the parser must be paired with `EntryService.ReconcileDurationsAsync()`, which runs at startup and rewrites stored durations that no longer follow from their time text — rows written by an older parser held stale and even negative hours. It is idempotent, so it costs one query per launch once converged.

Rules the parser holds to, all of them load-bearing:

- **The 12-hour assumption.** An end *time* before the start time means the afternoon, so `9-5` is 9am–5pm and `9:30-9` is 9:30am–9pm. Compare whole times, not just hours: comparing hours alone rejected `9:30-9` while accepting the equivalent `9:30-8`, purely because the hours happened to tie. An end hour of 12 or more is already unambiguous and is taken literally, so `14-13` is not "2pm to 1am" — it is backwards, and rejected.
- **An end of just minutes (`7:15-:30`) means the start's own hour**, i.e. 7:15–7:30. The colon is required so that `9-30` stays an out-of-range hour instead of quietly becoming 9:30. The 12-hour assumption does *not* apply to this form — it has named its hour outright — so `7:45-:30` is a typo, not an afternoon. This syntax is in active use in real data; an earlier anchored-regex rewrite dropped it and silently zeroed those rows.
- **Unreadable lines are flagged, never counted as zero.** A line that is not a range (`8am-5pm`, `lunch`), has out-of-range numbers (`9:70-10`, `99-1`), or still runs backwards after the afternoon reading (`14-13`, `12:30-12`, and overnight ranges like `23-1`, which a single day's entry cannot express) is collected in `TimeParseResult.InvalidLines`. `EntryRow` gives the field a red border and a tooltip; `Day` warns next to the day total. Silently returning 0 hours here is the bug that made day totals quietly come up short.
- **The cost of the 12-hour assumption is that same-hour typos read as afternoons**: `10:30-10:00` is 11.5 hours, not an error. That follows from the rule rather than being a special case — `10:30-9:45` was already accepted as 11.25 hours — and the inline day total is what surfaces it.
- **Valid lines still count when a sibling line is bad**, so a typo on line 3 does not erase lines 1 and 2.
- **A range with no end yet (`9-`) is in progress**: readable, worth 0 hours, not an error.
- **Hours and minutes are validated before any `TimeSpan`/`DateTime` is built.** Feeding unvalidated regex captures into `new DateTime(...)` is what used to throw `ArgumentOutOfRangeException` from an event handler and kill the circuit.
- **The parse is cached per `Time` value.** The `Time` setter clears it; a single render reads `Duration` and `InvalidTimeLines` several times over.

`Pages/App.razor` carries the standard `#blazor-error-ui` element, so if an unhandled exception does take the circuit down the user sees a message instead of a silently frozen page.

**Component conventions.**

- **Code-behind.** A component with any logic keeps it in a `X.razor.cs` partial class next to `X.razor`, so the `.razor` file is markup only. Injection moves with it: `@inject` becomes an `[Inject] private T Name { get; set; } = default!;` property. `@page` and `@inherits` stay in the `.razor` file — the razor-generated partial always emits a base class, so declaring one in the `.cs` partial too is a compile error, and the `.cs` partial is just `public partial class X`. Components that are markup plus parameters and no logic (`Loader`, `PageTitleWithSuffix`, `PageShell`) stay single-file, with the parameters in an `@code` block.
- Components that issue async/DB calls inherit `CancellableComponentBase` (`Components/Shared/`), which exposes a `CancellationToken` tied to the component lifetime and cancels it on dispose. Pass this token through to service calls.
- **Every `await` in a handler or lifecycle load goes through `RunGuardedAsync`.** An exception escaping one takes the whole circuit down, so the base class owns the pattern in one place: it holds `IsBusy` for the duration, clears it in a `finally`, turns a failure into `Error` (optionally prefixed, e.g. `"Not saved"`), and invokes the success callback only when the work actually succeeded. `OperationCanceledException` means the component is being disposed, so it returns silently — no `StateHasChanged`, no notifying the parent. Leaving the busy flag stuck on a throw is what permanently disabled a row's delete button, and hand-rolling the block per handler is why three of them once had no guard at all. Re-entrancy is deliberately *not* blocked: two field edits in quick succession are two real saves. `CopyButton` is the one deliberate exception to the rule — it hand-rolls its own try/catch, because a clipboard write has no busy state worth holding and its failure has a message of its own rather than an exception to relay.
- **`IsLoading` is for the fetch the page cannot render without**, and `RunInitialLoadAsync` is the only thing that sets it — it wraps `RunGuardedAsync` and clears the flag in a `finally`, because a failed load is still a finished load and leaving the loader up gives the error it produced nowhere to show. A refresh of a page already on screen goes through plain `RunGuardedAsync` instead: `Day.OnParametersSetAsync` loads through the former so paging to another day shows the loader, while `ReloadDayAsync` (raised by a row that just saved) uses the latter so the table does not flicker on every field edit. Keep the flag's setter private to the base — a page that could set it directly is a page that can strand its own loader.
- **`Error` is the one channel for "this didn't work."** `EntryRow` renders it as a small inline alert under the description, `ProjectRow` as a warning icon in a cell of its own with the message as its tooltip, and pages hand it to `PageShell` as an alert. `ProjectRow`'s cell is always in the markup so the icon appearing does not shift the fields beside it. A component that fails without throwing sets it directly, which is why the `"Not saved: "` prefix belongs to the save handlers and not to the markup — when the markup owned it, a clipboard failure read "Not saved: Could not copy to the clipboard." `Error` is per-component, so a self-contained child owns its own: `CopyButton` shows a failed clipboard write on itself rather than borrowing the row's slot.
- Saving is inline and immediate: both row components use `@bind:after` to persist each field on change. `EntryRow` then raises `OnEntryChanged` so the parent (`Pages/Day.razor`) reloads and recomputes the day total; `ProjectRow` has no such callback, because nothing on the projects page is derived from a project's own fields.
- **A save racing a delete is a normal outcome, not an error the user should see.** `UpdateEntryAsync`/`UpdateProjectAsync` both look the row up with `FirstOrDefaultAsync` and return quietly when it is gone — another tab may have deleted it while this one still had the row on screen. `UpdateEntryAsync` says which happened with a `bool`; `UpdateProjectAsync` returns plain `Task`, since no caller acts on the difference.
- **Busy flags guard re-entrancy, not just cosmetics.** A `disabled` attribute is a client-side hint and clicks queued before the flag's first render still arrive, so handlers whose work is not idempotent re-check before touching the database — `Day.CopyEntriesAsync` re-tests `CanCopy` before delegating to `RunGuardedAsync`, because the entry count it depends on does not change until the copy finishes, and without that a double-click copied the previous day twice.
- **Row order is decided in exactly one place.** `Day.OrderedEntries` sorts by `SortOrder` (unnumbered rows last) then `Time`; `GetEntriesAsync` deliberately imposes no order. Ordering in both meant SQLite's nulls-first and the client's nulls-last disagreeing, with the query's clause always overwritten anyway.
- **`Clock.Today` is how the app asks for the date**, so "is this today?" is not spelled three different ways and nothing reaches for `DateTime.Now` in a date-only app. Services take the date they should act on as a parameter — `CopyFromPreviousDayAsync(date)` rather than an internal "today" that silently depended on the caller's `IsToday` guard.
- `Pages/` holds routable pages; `Components/` holds reusable components (`Components/Shared/` for cross-cutting ones). Root routes (`/`, `/day`, `/day/{date}`) all resolve to `Pages/Day.razor`.
- **A component earns its keep on data in, not state relayed back out.** The day's table lives in `Day.razor` directly: as a separate component it was seven parameters, four of them relaying the page's own state and handlers straight back to it. What does extract cleanly takes plain values and owns everything else — `PageShell` (title, `IsLoading`, `Error`, a `Header` slot, content) is the loading/error scaffold both pages share, and `CopyButton` takes only the text to copy and keeps the interop, the two-second check, and the failure to itself. The `Header` slot exists so the error alert sits under the heading rather than shoving it down the page.
- **Both pages are built to the same shape**, so they read as one app: a `PageShell` header with an `h1.h2.mb-0` heading and its navigation right-aligned beside it, then a `card shadow overflow-hidden` wrapping a `table table-borderless table-sm`, then a trailing row holding the page's primary action (`Add Entry`, `Add Project`) at the right. The page owns the table, its `colgroup`, its header row, and the empty-state row that stands in when there is nothing to list; `EntryRow` and `ProjectRow` are `<tr>`s and nothing more.
- **A table's column widths live in the page's `colgroup`, the fields in the row component** — `Day.razor` with `EntryRow.razor`, `Projects.razor` with `ProjectRow.razor` — and nothing keeps the two files in sync, so change them together or the header stops lining up. Splitting the header into its own component would only move the two copies further apart.
- **Day-to-day navigation is plain `<a href>` links** (`NavButtons`), which the router handles without a round-trip. Only the date picker needs `NavigationManager`, since an `<input type="date">` cannot be an anchor.

**Styling.** Bootstrap 5 + Bootstrap Icons, served locally from `wwwroot/lib/`. Light/dark theme follows the OS preference via the inline script in `App.razor` (`data-bs-theme`). Project colors are applied as inline CSS custom properties (`--bs-border-color`). `wwwroot/css/site.css` is the only custom stylesheet and holds nothing but a monospace body font and a 14px base size — that font is why columns of times and totals line up.

**JS interop.** `App.razor`'s inline script is all of it: the theme switch above, and `copyToClipboard`, which `CopyButton` invokes. The latter falls back to a hidden textarea and `document.execCommand` outside a secure context, where `navigator.clipboard` does not exist; the Docker image serves plain HTTP, so that path is reachable in normal use.

## Deployment

`Dockerfile` produces a self-contained ASP.NET image listening on port 5161, expecting the SQLite DB on the `/etc/rubato` volume.
