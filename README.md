# DentPulse — Backend (.NET 8 Web API)

Integration API between the [Angular frontend](https://github.com/thops001/Dental_Test_Frontend)
and the Dentally REST API.

## Prerequisites

- .NET SDK **8.0+**

## Environment setup

The backend reads the Dentally API token from configuration. Provide it in **one** of these ways
(do not commit a real token):

**Environment variable** (`__` = config nesting):

```bash
export Dentally__Token="your-dentally-token"
```

**or User Secrets:**

```bash
cd dental-backend/dental-backend
dotnet user-secrets init
dotnet user-secrets set "Dentally:Token" "your-dentally-token"
```

Other settings live in `dental-backend/dental-backend/appsettings.json`:

- `Dentally:UseMock` — set to `true` to run with generated data (no token needed); `false` (default)
  uses the live Dentally API.

## Run

```bash
cd dental-backend/dental-backend
dotnet run
```

API runs on **http://localhost:5042** (Swagger UI at `/swagger`).

## Test

```bash
cd dental-backend
dotnet test
```
