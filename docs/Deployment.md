# Deployment

Target: **Windows Server** + **IIS** + **.NET 10 Runtime** + **SQL Server**.

Do not change source code to deploy. Use environment variables / IIS settings for secrets.

## Checklist

1. **Install .NET 10 Runtime** (ASP.NET Core 10 Hosting Bundle) from Microsoft.
2. **Install IIS** with IIS Management Console. Enable **ASP.NET Core Module V2** via the hosting bundle.
3. **Application pool**
   - Name: `SeQrRecall`
   - .NET CLR version: **No Managed Code**
   - Pipeline: Integrated
   - Identity: a dedicated account (recommended) or `ApplicationPoolIdentity`
   - Start Mode: AlwaysRunning (optional, for background processing)
4. **Create the IIS website** pointing at the published folder (for example `C:\inetpub\SeQrRecall`).
5. **HTTPS**
   - Bind 443 with a valid certificate.
   - Redirect HTTP to HTTPS.
   - The app enables HSTS outside Development (`max-age=31536000`; includeSubDomains).
   - Do not send JWTs over HTTP.
   - Published `web.config` sets IIS `requestLimits maxAllowedContentLength` to 104857600 (100 MB). The live audio/photo caps are `Uploads__*` below that ceiling.
6. **SQL Server**
   - Create database `SeQrRecall`.
   - Create a SQL login used only by the app.
   - Grant `db_datareader`, `db_datawriter`, and rights needed for migrations **only on a jump box / release account**, not the runtime identity if you apply migrations out of band.
7. **Create the database** (empty) then apply migrations from a release step (step 12).
8. **Application settings** (IIS Configuration Editor or environment variables):
   - `ConnectionStrings__DefaultConnection`
   - `Jwt__Secret`, `Jwt__Issuer`, `Jwt__Audience`
   - `Speech__ApiKey`, `Speech__BaseUrl`, `Speech__Provider`
   - `AI__ApiKey`, `AI__Provider`, `AI__Model`
   - `Storage__RootPath`
   - `Uploads__AudioMaxBytes`, `Uploads__PhotoMaxBytes` (optional; defaults 25 MB / 5 MB)
   - `RateLimiting__OtpSendPermitLimit`, `RateLimiting__OtpVerifyPermitLimit`, `RateLimiting__UploadPermitLimit` (optional)
   - `ASPNETCORE_ENVIRONMENT=Production`
9. **File storage directory**
   - Production: `D:\SeQrRecallData` (or the configured `Storage:RootPath`).
   - Subfolders: `Audio\Notes`, `Audio\Interactions`, `Audio\Leads`, `Photos\Customers`, `Photos\Interactions`, `Photos\Leads`, `Photos\Profiles`.
   - This machine may not have a `D:` drive; that is a production layout, not a development requirement.
10. **Permissions**
    - Grant the IIS app-pool identity **Modify** on `D:\SeQrRecallData` only.
    - Do not grant that identity local Administrator.
    - Do not store files under `wwwroot`.
11. **Publish**

```powershell
dotnet publish src\SeQrRecall.Api\SeQrRecall.Api.csproj -c Release -o C:\inetpub\SeQrRecall
```

`web.config` is included for ANCM. Hosting model: in-process. App pool remains **No Managed Code**. The published `web.config` removes the IIS WebDAV module so PUT and DELETE reach the app (profile update, note/customer delete).

12. **Migrations (controlled)**

```powershell
dotnet ef database update --project src\SeQrRecall.Infrastructure --startup-project src\SeQrRecall.Api --configuration Release
```

Run from the release pipeline or a secured admin session. Never auto-run destructive migrations on startup in Production.

Where the release account cannot run the .NET SDK, apply the checked-in idempotent script instead. `migrate.sql` covers every migration and skips the ones already recorded in `__EFMigrationsHistory`, so it is safe on a database at any migration level:

```powershell
sqlcmd -S <server> -d SeQrRecall -i migrate.sql
```

Regenerate it after adding a migration:

```powershell
dotnet ef migrations script --idempotent --project src\SeQrRecall.Infrastructure --startup-project src\SeQrRecall.Api --output migrate.sql
```

13. **Test the API**
    - `GET https://<host>/health`
    - `GET https://<host>/api/v1/system/version`
    - Authenticate with dummy OTP only in non-production.
14. **Logging**
    - Serilog file sink under a folder the pool identity can write (Phase 1).
    - Retain logs; do not log OTP, JWT, API keys, or audio contents.
15. **Backups**
    - SQL Server: daily full backup + transaction log backups for Full recovery.
    - Retain per company policy.
    - Backup `D:\SeQrRecallData` as well (files are not in SQL).
    - Do not implement backup inside the application.
16. **Restart / recovery**
    - Rapid-Fail Protection: enabled with a sensible threshold.
    - Start Mode AlwaysRunning if background processing must resume after recycle.
    - Document the recycle schedule; avoid recycling during long transcriptions or make the queue durable (Hangfire later).

## Certificate installation (HTTPS)

1. Import the PFX into Local Computer → Personal.
2. Grant the app-pool identity read on the private key if required.
3. Bind the cert on the IIS site, port 443.
4. Disable weak TLS via OS policy.

## SQL Server backup recommendation

- Recovery model: Full.
- Daily full backup (off-peak).
- Transaction log backup every 15–60 minutes.
- Weekly copy to off-server storage.
- Test restore quarterly.

## Health

- `/health` — process is up.
- `/health/ready` — database reachable. Do not return connection strings or exception text.
