# DentPulse — Backend (.NET 8 Web API)

ASP.NET Core Web API that integrates with the [Dentally REST API](https://developers.dentally.co),
syncs appointment data into a local JSON cache, and serves it to the
[Angular frontend](https://github.com/thops001/Dental_Test_Frontend).

> **Live vs mock:** The API talks to the **live Dentally API by default** (`Dentally:UseMock = false`)
> and needs a token. A fully-implemented **mock provider** is also included so the app can run
> end-to-end without a token — set `Dentally:UseMock = true`. See
> [Running against the live API](#running-against-the-live-dentally-api).

---

## Prerequisites

- .NET SDK **8.0+**

## Run

```bash
cd dental-backend/dental-backend
dotnet run
```

- Listens on **http://localhost:5042** (Swagger UI at `/swagger` in Development).
- CORS is allow-listed for the frontend at `http://localhost:4200`.

## Test

```bash
cd dental-backend
dotnet test
```

xUnit suite covering date-range resolution, the Dentally client (pagination, de-duplication,
error surfacing, query building), JSON cache round-tripping, and the sync/detail orchestration.

---

## API

| Method | Route | Purpose |
|--------|-------|---------|
| `POST` | `/api/sync` | Fetch appointments from Dentally for a date range, overwrite the cache. Returns count + duration. |
| `GET`  | `/api/appointments` | Return the cached appointments (`204 No Content` if no sync has run). |
| `GET`  | `/api/appointments/{id}` | Full detail for one appointment (cache enriched with live data); `404` if unknown. |

**`POST /api/sync` body**

```jsonc
{
  "token": "optional-bearer-token",   // falls back to the configured token / mock when omitted
  "range": "Today",                   // "Today" | "ThisWeek" | "Custom"
  "from": "2026-06-01",               // required when range = "Custom" (yyyy-MM-dd)
  "to":   "2026-06-30"
}
```

**Status codes:** `200` ok · `400` invalid request (e.g. custom range without dates) ·
`401` bad/missing token · `429` rate limited · `502` upstream Dentally failure · `499` client cancelled.

---

## Configuration

`dental-backend/dental-backend/appsettings.json`:

```jsonc
"Dentally": {
  "BaseUrl": "https://api.dentally.co/v1/",
  "Token": "",                  // server-side fallback token (prefer env var / user secrets)
  "UserAgent": "DentPulse/1.0", // Dentally rejects requests without a User-Agent
  "PageSize": 100,              // Dentally max per_page
  "MaxConcurrency": 5,          // parallel page / patient fetches
  "EnrichPatientNames": true,   // look up missing patient names via GET /patients/{id}
  "UseMock": false              // true => generated data, no token needed
},
"Storage": { "FilePath": "data/appointments.json" }
```

### Configuring the Dentally token

The token can be supplied three ways (most specific wins):

1. **Per-request** from the frontend Connect form.
2. **Environment variable** — `Dentally__Token` (double underscore = config nesting):
   ```bash
   export Dentally__Token="your-token"
   ```
3. **User Secrets** (idiomatic for local dev, kept out of the repo):
   ```bash
   cd dental-backend/dental-backend
   dotnet user-secrets init
   dotnet user-secrets set "Dentally:Token" "your-token"
   ```

`appsettings.json` ships with an empty token on purpose — **do not commit a real one**.

### Running against the live Dentally API

1. Ensure `Dentally:UseMock` is `false` (the default).
2. Provide a token (one of the ways above).
3. Sync. The client calls
   `GET /v1/appointments?after=<from>T00:00:00Z&before=<to>T23:59:59Z&page=<n>&per_page=100`
   with a `User-Agent` header, reads `meta.total` to compute the page count, and fetches the
   remaining pages concurrently.

---

## Sync strategy & trade-offs

**Accuracy**
- Page 1 is fetched to read `meta.total`; the page count is derived from it and the remaining pages
  are merged **in page order**, de-duplicated by appointment id (`HashSet`) so overlapping pages
  cannot produce duplicates and nothing is silently dropped.
- **Partial failures surface** — pages are awaited with `Task.WhenAll`; if any page ultimately fails
  the whole sync throws, so a partial result is **never written** to the cache.
- Timestamps are normalised to **UTC** on the way in and rendered in UTC by the frontend.
- The cache is written to a temp file then **atomically moved** into place — a crash mid-write can't
  leave a half-written cache.

**Speed**
- Pages (and patient lookups) are fetched **concurrently**, bounded by a `SemaphoreSlim`
  (`MaxConcurrency`).
- Only the requested date range is fetched (server-side `after`/`before` filters).
- Sync **duration** (`Stopwatch`) and **record count** are returned to the UI.

**Resilience**
- Transient faults (5xx, timeouts) are retried and `429` is handled — including honouring
  `Retry-After` — by the .NET standard resilience handler (Polly) on the typed `HttpClient`.
- `async`/`await` with `CancellationToken` throughout; the frontend Cancel button aborts the
  request and the backend returns `499`.

**Patient / practitioner names**
- The Dentally appointment resource exposes `practitioner_name` (used directly) but `patient_name`
  is often empty. When `EnrichPatientNames` is on, missing names are resolved via
  `GET /patients/{id}` during the sync (best-effort — a failed lookup never aborts the sync).
- Appointments with no linked patient (blocked/admin slots) legitimately have no name and are shown
  as *"No patient"*.

**Single-appointment detail**
- `GET /api/appointments/{id}` returns the cached core record enriched with live fields —
  `finish_time`, `notes`, `cancelled_at`, and patient contact (email, phone, date of birth) from
  `GET /patients/{id}`.

**Known limitations**
- Storage is a single JSON file **overwritten** on each sync (per spec) — no incremental/delta sync,
  no database. The full requested range is re-fetched each sync.

---

## Project layout

```
dental-backend/
  dental-backend/              # Web API project
    Controllers/               # AppointmentsController (sync, list, detail)
    Dentally/                  # IDentallyClient, DentallyClient, MockDentallyClient, DTOs, mapper
    Models/                    # Appointment, AppointmentDetail, DateRange, sync request/result
    Services/                  # SyncService (orchestration)
    Storage/                   # IAppointmentStore, JsonAppointmentStore (atomic writes)
  dental-backend.Tests/        # xUnit tests
  dental-backend.sln
```
