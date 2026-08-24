# Rubato

A single-user daily time-tracking app: a Blazor Server (.NET 10) web app backed by a local SQLite database. You navigate day by day, add entries, and each entry's worked hours are computed from a free-text time field.

## Features

- Create time entries for any given day.
- An entry's duration is parsed from its time text automatically.
- Copy entries from the previous working day (when today is empty).
- Clone individual entries within the specified day.
- Manage projects, assign each a color and work item ID.
- Push the day's entries to 7Pace. Will delete any worklogs from 7Pace for the specified day first.

## Time syntax

The time field is free text, one range per line. Hours are summed across the lines.

| Input | Reads as | Hours |
| --- | --- | --- |
| `9-10:30` | 9:00am – 10:30am | 1.5 |
| `7:15-:30` | 7:15 – 7:30 (minutes alone use the start's hour; the colon is required) | 0.25 |
| `9-5` | 9:00am – 5:00pm (an end before the start means the afternoon) | 8 |
| `9-` | in progress, so no hours | 0 |

## 7Pace Push

Entries for a day can be pushed to 7Pace with the push of a button. Only entries with a non-zero duration and with a project that has a Work Item ID will be included. The activity type will be set to one of three options depending on the entry:

- Deployment - when the entry description contains "deploy" (so "Deploy to UAT" and "Production Deployment" would both be considered).
- Meeting - when the sort order is 1-9
- Development - all other entries

If the day contains an entry with a sort order of 0, then the duration of that particular entry will be added to the first non-meeting entry.

## Running locally

Run from the repo root.

```bash
# Build will target solution file in repo root
dotnet build

# Run by targeting the project directly
dotnet run --project Rubato

# Run with hot reload by targeting the project directly
dotnet watch --project Rubato
```

## Configuration

| Key | Purpose | Default |
| --- | --- | --- |
| `DataPath` | Directory holding `Rubato.db` | `Database/` (`/etc/rubato` in Docker) |
| `TimeZone` | IANA id (e.g. `America/New_York`) deciding what "today" means | the process's own zone (`TZ`, or UTC in Docker) |
| `7Pace:ApiUrl` | 7Pace instance base URL | — |
| `7Pace:ApiKey` | 7Pace API token | — |
| `7Pace:UserId` | The user whose worklogs a push replaces | — |
| `7Pace:MeetingActivityTypeId` | Activity type for entries with sort order 1–9 | — |
| `7Pace:DevelopmentActivityTypeId` | Activity type for sort order 10 and up | — |
| `7Pace:DeploymentActivityTypeId` | Activity type when the description mentions a deployment | — |

`TimeZone` is worth setting for any deployment west of UTC. The app is day-oriented, so "today" — the day the page opens on, the Today button, whether the previous day can be copied — comes from a single clock, and a container has no local zone unless one is handed to it. Left at UTC, an evening entry is filed under tomorrow's date. Setting the `TZ` environment variable works too; `TimeZone` just says so where the rest of the configuration lives, and an id the machine cannot resolve stops the launch rather than quietly reverting to UTC.

The 7Pace values are secrets and should live in user secrets rather than `appsettings.json` for development. Environment variables should be used when deployed.

```bash
cd Rubato # user secrets are easier to set from the project root
dotnet user-secrets set "7Pace:ApiUrl" "<url>"
dotnet user-secrets set "7Pace:ApiKey" "<token>"
# and the rest...
```

## Database

SQLite, created and migrated automatically — `db.Database.Migrate()` runs at startup, so you rarely need to apply migrations by hand. After changing anything in `Data/Models/`:

```bash
cd Rubato # EF operations are easier from the project root
dotnet ef migrations add <Name>
```

## Deployment

The `Dockerfile` produces a self-contained ASP.NET image listening on port 5161 and expects the SQLite database on the `/etc/rubato` volume.

Images are built and pushed by the `Publish image` GitHub Actions workflow, which publishes `ghcr.io/amoscardino/rubato` for `linux/amd64` and `linux/arm64`.

- **Release:** push a `vX.Y.Z` tag. That publishes `X.Y.Z`, `X.Y` and `latest`.

  ```bash
  git tag v1.0.1 && git push origin v1.0.1
  ```

- **Ad hoc:** run the workflow manually from the Actions tab. Leave the version input blank to republish `latest` only, or type a version to also tag it.

The images are published to the repo's GitHub Packages, which start out private. Either make the package public from its settings page, or `docker login ghcr.io` on the host with a personal access token that has `read:packages`.

To build locally without Actions:

```bash
docker build . -t rubato
```

Deploy using `docker compose`:

```yaml
services:
  rubato:
    restart: unless-stopped
    image: ghcr.io/amoscardino/rubato:latest
    ports:
      - 5161:5161 
    volumes:
      - /path/to/host/data:/etc/rubato:rw
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - TimeZone=America/New_York
      - 7Pace__UserId=00000000-0000-0000-0000-000000000000
      - 7Pace__MeetingActivityTypeId=00000000-0000-0000-0000-000000000000
      - 7Pace__DevelopmentActivityTypeId=00000000-0000-0000-0000-000000000000
      - 7Pace__DeploymentActivityTypeId=00000000-0000-0000-0000-000000000000
      # The rest of the API path including `/api` will be appended automatically. Uses API version 3.2.
      - 7Pace__ApiUrl=https://yourdomain.timehub.7pace.com 
      - 7Pace__ApiKey=000000000000000000000000000000000000000000000000
```
