# SJewls .NET 8 Backend - Railway Deployment Guide

This guide provides step-by-step instructions for deploying the SJewls .NET 8 backend to Railway and explains the resolution to the build error:
> *"Railpack could not determine how to build the app."*

---

## 1. Problem Root Cause

The SJewls repository is structured as a monorepo containing multiple subprojects:
```
SJewls/
├── admin/    (Next.js 16 Web Application)
├── backend/  (.NET 8 Web API & Class Libraries)
├── docs/     (Documentation & OpenAPI Specifications)
├── mobile/   (Customer Mobile Application)
└── shared/   (Shared assets & database seed scripts)
```

By default, Railway analyzes the repository root (`/`). Because the root contains multiple language ecosystems without a root-level build file or Dockerfile, Railway's Nixpacks/Railpack engine was unable to detect the primary technology and failed with:
`Railpack could not determine how to build the app.`

Additionally, even within `/backend`, multi-project .NET solutions with clean architecture layers (`SJewls.Domain`, `SJewls.Application`, `SJewls.Infrastructure`, `SJewls.Api`) require explicit project targeting rather than automatic detection.

---

## 2. Changes Implemented

1. **Multi-Stage Dockerfile** (`backend/Dockerfile`):
   - **Stage 1 (Build)**: Uses `mcr.microsoft.com/dotnet/sdk:8.0`. Copies `.csproj` files first to maximize layer caching on package restore, restores dependencies, and publishes `SJewls.Api` in `Release` mode with `--no-restore`.
   - **Stage 2 (Runtime)**: Uses `mcr.microsoft.com/dotnet/aspnet:8.0`. Copies published binaries, sets production defaults, exposes port 8080, and runs `dotnet SJewls.Api.dll`.

2. **Docker Ignore** (`backend/.dockerignore`):
   - Excludes `bin/`, `obj/`, local secrets (`.env`, `.env.*`), `.git`, IDE files, and development scripts to ensure lightweight builds and prevent accidental leakage of local credentials.

3. **Railway Configuration Schema** (`backend/railway.json`):
   - Explicitly instructs Railway to use the `DOCKERFILE` builder pointing to `Dockerfile`.
   - Configures deployment health checking against `/api/v1/health` with a 120-second timeout.

4. **Dynamic Port Binding** (`backend/src/SJewls.Api/Program.cs`):
   - Added runtime detection for Railway's `PORT` environment variable:
     ```csharp
     var port = Environment.GetEnvironmentVariable("PORT");
     if (!string.IsNullOrEmpty(port))
     {
         builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
     }
     ```
   - **Preserves Local Development**: When `PORT` is not defined (local development via `dotnet run` or Visual Studio), Kestrel continues to listen on `http://localhost:5230` as configured in `launchSettings.json`.

---

## 3. Exact Railway Settings

In your Railway project dashboard:

| Setting | Value | Notes |
| :--- | :--- | :--- |
| **Repository** | `SageesanW/SJewls` | Your GitHub repository |
| **Branch** | `sagee-dev` | Branch containing these backend deployment fixes |
| **Root Directory** | `/backend` | **Crucial:** Directs Railway to build within the `backend/` folder |
| **Build Configuration** | Dockerfile | Managed automatically by `railway.json` |
| **Dockerfile Path** | `Dockerfile` | Relative to `/backend` |
| **Healthcheck Path** | `/api/v1/health` | Verifies app and database connectivity (HTTP 200) |
| **Healthcheck Timeout**| `120` seconds | Allows time for startup seeding and initial connection pooling |
| **Restart Policy** | On Failure (Max 10) | Ensures service auto-restarts if a transient failure occurs |

### Networking
1. Go to the **Settings** tab of your service in Railway.
2. Under **Networking**, click **Generate Domain** (or attach a custom domain like `api.sjewls.lk`).
3. Note the generated public URL (e.g., `https://sjewls-backend-production.up.railway.app`).

---

## 4. Required Railway Environment Variables

Configure the following variables in the **Variables** tab of the Railway service. **Never commit real values to version control.**

| Variable Name | Description | Example / Placeholder Value |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET Core environment mode | `Production` |
| `PORT` | Dynamic listening port | `8080` *(Railway provides this automatically)* |
| `ConnectionStrings__DefaultConnection` | Supabase / PostgreSQL pooled connection string (Port 5432 session mode recommended for EF Core) | `Host=aws-0-ap-northeast-2.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.[PROJECT_REF];Password=[DB_PASSWORD];SslMode=Require;Trust Server Certificate=true` |
| `Jwt__Secret` | Cryptographic secret key for signing JWTs (min 32 chars) | `[MINIMUM_32_CHARACTER_CRYPTOGRAPHIC_SECRET_KEY]` |
| `Jwt__Issuer` | JWT issuer claim | `SJewls.Api` |
| `Jwt__Audience` | JWT audience claim | `SJewls.App` |
| `Jwt__ExpiryMinutes` | Token validity period in minutes | `1440` |
| `Supabase__Url` | Supabase Project API URL | `https://[PROJECT_REF].supabase.co` |
| `Supabase__ServiceKey` | Supabase Service Role Key (for storage upload/access) | `[SUPABASE_SERVICE_ROLE_KEY]` |
| `Supabase__BucketName` | Media bucket name | `sjewls-media` |
| `Email__Host` | SMTP server host | `smtp.gmail.com` |
| `Email__Port` | SMTP port | `587` |
| `Email__Username` | Gmail sender address | `[SENDER_EMAIL@gmail.com]` |
| `Email__Password` | Gmail 16-character App Password | `[GMAIL_16_CHAR_APP_PASSWORD]` |
| `Email__FromAddress` | Outgoing from email address | `[SENDER_EMAIL@gmail.com]` |
| `Email__FromName` | Display name for outgoing emails | `SJewls` |
| `Sms__Provider` | SMS Gateway provider (`TextLk` or `Mock`) | `TextLk` |
| `Sms__ApiUrl` | Text.lk REST endpoint | `https://app.text.lk/api/v3/sms/send` |
| `Sms__ApiToken` | Text.lk API token | `[TEXT_LK_API_TOKEN]` |
| `Sms__SenderId` | Registered SMS Sender ID | `TextLKDemo` |
| `App__Environment` | Application environment name | `Production` |
| `App__DefaultBranchCode`| Default branch code for customer registration | `JAF-01` |
| `Admin__InitialUsername`| Default Super Admin username | `superadmin` |
| `Admin__InitialEmail` | Default Super Admin email | `admin@sjewls.lk` |
| `Admin__InitialPassword`| Default Super Admin initial password | `[STRONG_SUPERADMIN_PASSWORD]` |
| `Admin__PortalUrl` | URL to the admin web portal | `https://admin.sjewls.lk` |

> [!NOTE]
> In ASP.NET Core, double underscores (`__`) map to JSON section dividers (e.g. `ConnectionStrings__DefaultConnection` -> `ConnectionStrings:DefaultConnection`).

---

## 5. Database Migrations

The application does **not** run destructive migrations on startup. Startup execution is limited to `SeedInitialSuperAdminAsync()` which creates default administrative roles and the initial Super Admin account if they do not exist.

### Applying Migrations Safely
To apply database migrations to your Supabase PostgreSQL instance:

#### Option A: Direct EF Core CLI (Recommended for Development / CI)
Run from the `backend/` directory:
```bash
dotnet ef database update \
  --project src/SJewls.Infrastructure \
  --startup-project src/SJewls.Api \
  --connection "Host=...;Database=postgres;Username=...;Password=...;SslMode=Require;Trust Server Certificate=true"
```

#### Option B: Idempotent SQL Script (Recommended for Production DBA Review)
Generate a script containing all pending migrations wrapped in safety checks:
```bash
dotnet ef migrations script --idempotent \
  --project src/SJewls.Infrastructure \
  --startup-project src/SJewls.Api \
  --output ./migrations.sql
```
Review `migrations.sql` and execute it directly in the Supabase SQL Editor.

---

## 6. Step-by-Step Railway Deployment Instructions

1. **Push Changes to GitHub**:
   Commit and push the changes to branch `sagee-dev`:
   ```bash
   git add backend/Dockerfile backend/.dockerignore backend/railway.json backend/src/SJewls.Api/Program.cs docs/RAILWAY_BACKEND_DEPLOYMENT_GUIDE.md
   git commit -m "fix(backend): configure Dockerfile and Railway settings for .NET 8 backend"
   git push origin sagee-dev
   ```

2. **Configure Service in Railway**:
   - Open your project on [Railway.app](https://railway.com).
   - Click on your Backend service (or create a new service from GitHub Repo `SageesanW/SJewls`).
   - Open **Settings**:
     - Under **Source**, set **Branch** to `sagee-dev`.
     - Under **Build**, set **Root Directory** to `/backend`.
     - Notice that Railway will recognize `backend/railway.json` and set the builder to `Dockerfile`.
   - Open **Variables**:
     - Add all environment variables listed in Section 4.

3. **Deploy & Verify**:
   - Trigger a deploy (or push a commit to `sagee-dev`).
   - Monitor the **Build Logs**:
     - `dotnet restore` will run and cache.
     - `dotnet publish` will produce the Release binaries.
     - The runtime image will start.
   - Monitor the **Deploy Logs**:
     - `Now listening on: http://0.0.0.0:[PORT]`
     - Health check request: `GET /api/v1/health` returning `200 OK`.
   - Open your public URL in the browser:
     - Visit `https://[your-service].up.railway.app/` -> Redirects to the interactive Scalar API documentation at `/scalar/v1`.
     - Visit `https://[your-service].up.railway.app/api/v1/health` -> Returns `{"status":"Healthy","databaseConnected":true,...}`.
