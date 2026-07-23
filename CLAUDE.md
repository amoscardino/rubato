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

**Services** (`Services/`, registered transient in `Program.cs`) are the only code that touches `RubatoDataContext`. Components inject `EntryService`/`ProjectService` and never query the `DbContext` directly.

**Database lifecycle.** SQLite. `db.Database.Migrate()` runs at startup in `Program.cs`, so migrations apply automatically on launch — you rarely need `dotnet ef database update`. The DB file path comes from the `DataPath` config value (defaults to `Database/`; the Docker image sets it to `/etc/rubato`, a mounted volume). `RubatoDataContext` also stores ASP.NET Data Protection keys (`IDataProtectionKeyContext`).

**Time computation is the domain crux.** An entry's `Time` is a multi-line free-text field (e.g. `9-10:30`, one range per line). `EntryModel.GetDuration()` parses each line with `TimeRegex` and sums the hours; the result is written to `Duration` on save. Duration is a computed, read-only display field in the UI — never edited directly. Note the 12-hour assumption in `ConvertToHours`: when the end hour is less than the start hour it adds 12 (so `9-5` means 9am–5pm).

**Component conventions.**

- Components that issue async/DB calls inherit `CancellableComponentBase` (`Components/Shared/`), which exposes a `CancellationToken` tied to the component lifetime and cancels it on dispose. Pass this token through to service calls.
- Saving is inline and immediate: `EntryRow.razor` uses `@bind:after` to persist each field on change, then raises `OnEntryChanged` so the parent (`Pages/Day.razor`) reloads and recomputes the day total.
- `Pages/` holds routable pages; `Components/` holds reusable components (`Components/Shared/` for cross-cutting ones). Root routes (`/`, `/day`, `/day/{date}`) all resolve to `Pages/Day.razor`.

**Styling.** Bootstrap 5 + Bootstrap Icons, served locally from `wwwroot/lib/`. Light/dark theme follows the OS preference via the inline script in `App.razor` (`data-bs-theme`). Project colors are applied as inline CSS custom properties (`--bs-border-color`).

## Deployment

`Dockerfile` produces a self-contained ASP.NET image listening on port 5161, expecting the SQLite DB on the `/etc/rubato` volume.
