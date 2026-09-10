# Database

SQL Server with EF Core. Phase 0 defines the model. The `InitialCreate` migration is Phase 1. Do not create tables in application startup.

All timestamps are `datetimeoffset`. Primary keys are `uniqueidentifier` (GUID).

## Tables

### Users

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| FullName | nvarchar(200) | Empty until profile setup |
| Mobile | nvarchar(20) | Unique filtered index where not null |
| Email | nvarchar(256) | Unique filtered index where not null |
| ProfilePhotoUrl | nvarchar(500) | Stored file name / relative URL, not a disk path |
| IsActive | bit | Default 1 |
| CreatedOn | datetimeoffset | |
| CreatedBy | uniqueidentifier | nullable |
| UpdatedOn | datetimeoffset | nullable |
| UpdatedBy | uniqueidentifier | nullable |
| LastLoginOn | datetimeoffset | nullable |

A user may register with mobile **or** email. At least one must be present (application rule).

### RefreshTokens

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| UserId | uniqueidentifier | FK → Users, cascade |
| TokenHash | nvarchar(128) | SHA-256 (or stronger) of the raw token |
| ExpiresOn | datetimeoffset | |
| RevokedOn | datetimeoffset | nullable |
| CreatedOn | datetimeoffset | |
| DeviceInfo | nvarchar(256) | nullable |

Store hashes only. Raw refresh tokens are returned once to the client.

### Notes

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| UserId | uniqueidentifier | FK → Users |
| Title | nvarchar(200) | nullable until AI completes |
| ShortSummary | nvarchar(500) | nullable |
| FullSummary | nvarchar(max) | nullable |
| Transcript | nvarchar(max) | English transcript |
| AudioFileUrl | nvarchar(500) | generated file name, not client filename |
| ProcessingStatus | int / string | see enum |
| ProcessingError | nvarchar(2000) | nullable; set when AI fails after STT |
| DurationSeconds | int | nullable |
| DetectedLanguages | nvarchar(200) | comma-separated or short JSON |
| ImportantEntitiesJson | nvarchar(max) | validated AI entities; nullable |
| CreatedOn | datetimeoffset | |
| CreatedBy | uniqueidentifier | nullable |
| UpdatedOn | datetimeoffset | nullable |
| UpdatedBy | uniqueidentifier | nullable |
| CompletedOn | datetimeoffset | nullable |
| IsDeleted | bit | soft delete |

### NoteActionItems

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| NoteId | uniqueidentifier | FK → Notes, cascade |
| Description | nvarchar(1000) | |
| DueDate | datetimeoffset | nullable; only if spoken |
| IsCompleted | bit | default 0 |
| Kind | int | `Action` or `FollowUp` |
| CreatedOn | datetimeoffset | |

`Kind` is the only additive column beyond the original product table list. It stores AI follow-up items without a second table.

### Leads

Same shape as `Notes` plus `PhotoUrl`.

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| UserId | uniqueidentifier | FK → Users |
| Title | nvarchar(200) | nullable until AI completes |
| ShortSummary | nvarchar(500) | nullable |
| FullSummary | nvarchar(max) | nullable |
| Transcript | nvarchar(max) | English transcript |
| AudioFileUrl | nvarchar(500) | generated file name, not client filename |
| PhotoUrl | nvarchar(500) | nullable; stored file name only. APIs expose `hasPhoto` and stream `GET .../photo` |
| ProcessingStatus | int / string | see enum |
| ProcessingError | nvarchar(2000) | nullable; set when AI fails after STT |
| DurationSeconds | int | nullable |
| DetectedLanguages | nvarchar(200) | comma-separated or short JSON |
| ImportantEntitiesJson | nvarchar(max) | validated AI entities; nullable |
| CreatedOn | datetimeoffset | |
| CreatedBy | uniqueidentifier | nullable |
| UpdatedOn | datetimeoffset | nullable |
| UpdatedBy | uniqueidentifier | nullable |
| CompletedOn | datetimeoffset | nullable |
| IsDeleted | bit | soft delete |

### LeadActionItems

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| LeadId | uniqueidentifier | FK → Leads, cascade |
| Description | nvarchar(1000) | |
| DueDate | datetimeoffset | nullable; only if spoken |
| IsCompleted | bit | default 0 |
| Kind | int | `Action` or `FollowUp` |
| CreatedOn | datetimeoffset | |

### Customers

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| UserId | uniqueidentifier | FK → Users |
| Name | nvarchar(200) | |
| CompanyName | nvarchar(200) | nullable |
| Mobile | nvarchar(20) | nullable |
| Email | nvarchar(256) | nullable |
| PhotoUrl | nvarchar(500) | nullable; stored file name only. APIs expose `hasPhoto` and stream `GET .../photo` |
| CreatedOn | datetimeoffset | |
| CreatedBy | uniqueidentifier | nullable |
| UpdatedOn | datetimeoffset | nullable |
| UpdatedBy | uniqueidentifier | nullable |
| IsActive | bit | default 1 |
| IsDeleted | bit | soft delete |

### CustomerInteractions

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| UserId | uniqueidentifier | FK → Users |
| CustomerId | uniqueidentifier | FK → Customers |
| PhotoUrl | nvarchar(500) | nullable |
| AudioFileUrl | nvarchar(500) | nullable |
| Transcript | nvarchar(max) | nullable |
| ShortSummary | nvarchar(500) | nullable |
| FullSummary | nvarchar(max) | nullable |
| InteractionDate | datetimeoffset | |
| DurationSeconds | int | nullable |
| ProcessingStatus | int / string | |
| ProcessingError | nvarchar(2000) | nullable |
| DetectedLanguages | nvarchar(200) | nullable |
| ImportantEntitiesJson | nvarchar(max) | nullable |
| CreatedOn | datetimeoffset | |
| CreatedBy | uniqueidentifier | nullable |
| UpdatedOn | datetimeoffset | nullable |
| UpdatedBy | uniqueidentifier | nullable |
| CompletedOn | datetimeoffset | nullable |
| IsDeleted | bit | soft delete (application filter) |

### CustomerInteractionActionItems

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| InteractionId | uniqueidentifier | FK → CustomerInteractions, cascade |
| Description | nvarchar(1000) | |
| DueDate | datetimeoffset | nullable |
| IsCompleted | bit | |
| Kind | int | Action or FollowUp |
| CreatedOn | datetimeoffset | |

### AuditLogs

| Column | Type | Notes |
| --- | --- | --- |
| Id | uniqueidentifier | PK |
| UserId | uniqueidentifier | nullable |
| Action | nvarchar(100) | |
| EntityType | nvarchar(100) | |
| EntityId | uniqueidentifier | nullable |
| Details | nvarchar(2000) | no secrets, no transcripts dumped wholesale |
| CreatedOn | datetimeoffset | |

## Indexes (v1)

Do not index every column.

**Users:** unique filtered indexes on `Mobile` and `Email` where the value is not null.

**Notes:** `(UserId, CreatedOn DESC)`, `(UserId, ProcessingStatus)`.

**Leads:** `(UserId, CreatedOn DESC)`, `(UserId, ProcessingStatus)`.

**Customers:** `(UserId, Name)`, `(UserId, CompanyName)`, `(UserId, Mobile)`.

**CustomerInteractions:** `(UserId, InteractionDate DESC)`, `(CustomerId, InteractionDate DESC)`.

**RefreshTokens:** `(UserId)`, `(TokenHash)`.

Search on notes (title, summary, transcript) will use parameterized `LIKE` or `EF.Functions.Like` in v1. Full-text search can be added later without API changes.

## Soft delete

`Notes`, `Leads`, and `Customers` use `IsDeleted` plus EF Core global query filters (Phase 1). Physical deletes are not used in normal operations.

## Ownership

Every user-owned row includes `UserId`. APIs must filter by the JWT user, not by a client-supplied user id.

## Migrations

Phase 1: `InitialCreate` (`20260819162552_InitialCreate`). Latest: `AddLeads` (`20260825164856_AddLeads`), which creates `Leads` and `LeadActionItems`. Apply with:

```powershell
dotnet tool restore
dotnet ef database update --project src/SeQrRecall.Infrastructure --startup-project src/SeQrRecall.Api
```

Where the SDK is unavailable, run the checked-in `migrate.sql` instead. It is idempotent and skips migrations already recorded in `__EFMigrationsHistory`. Regenerate it after adding a migration; see [Deployment](Deployment.md).

Every later schema change is a named EF Core migration. Production must never auto-apply destructive migrations.
