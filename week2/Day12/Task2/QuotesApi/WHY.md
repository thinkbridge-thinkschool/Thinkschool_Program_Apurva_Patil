# Day 12 — Read Models + CQRS-lite

## The Problem

Every endpoint was returning the raw `Quote` domain entity — the same object used to enforce
business rules (`SoftDelete`, `Create` validation). That means:
- Read consumers received fields they didn't need (`IsDeleted`, all private state)
- EF Core loaded and tracked full entities even for display-only calls
- No way to evolve the read shape (e.g. add a display field) without touching the domain model

## What Was Split

The **Quotes feature** was split into separate read and write paths.

### Write path — `Commands/`

```
POST /api/quotes
  → CreateQuoteCommand { Author, Text }
  → CreateQuoteCommandHandler.HandleAsync()
      → Quote.Create() — validation lives in the domain model
      → IQuoteRepository.AddAsync()
```

Handler owns validation + persistence. Endpoint becomes thin.

### Read path — `Queries/`

```
GET /api/quotes
GET /api/quotes/{id}
  → IQuoteQueryService
  → QuoteQueryService
      → DbContext directly
      → .AsNoTracking()
      → .Select(q => new QuoteReadModel(...))  ← projection, not entity load
```

Query service never instantiates a `Quote` domain entity. Only fetches what the screen needs.

### Delete — still uses the domain entity (intentional)

```
DELETE /api/quotes/{id}
  → IQuoteRepository.GetByIdAsync()  ← loads Quote entity
  → quote.SoftDelete()               ← business rule on the domain model
  → SaveChangesAsync()
```

`SoftDelete()` is a behaviour on the aggregate — it belongs in the write path.

---

## Proof — SQL Captured at Runtime

### READ: `GET /api/quotes?page=1&size=3`

EF Core emits a **4-column projection** — only what the screen needs:

```sql
SELECT [q].[Id], [q].[Author], [q].[Text], [q].[CreatedAt]
FROM [Quotes] AS [q]
WHERE [q].[IsDeleted] = CAST(0 AS bit)
ORDER BY [q].[CreatedAt] DESC
OFFSET @p ROWS FETCH NEXT @p1 ROWS ONLY
```

Response (read model — no IsDeleted, no OwnerId):
```json
[
  {"id":200,"author":"Confucius","text":"Quote 20 by Confucius.","createdAt":"2026-05-30T06:21:31Z"},
  {"id":199,"author":"Confucius","text":"Quote 19 by Confucius.","createdAt":"2026-05-30T06:21:31Z"},
  {"id":198,"author":"Confucius","text":"Quote 18 by Confucius.","createdAt":"2026-05-30T06:21:31Z"}
]
```

### READ: `GET /api/quotes/201`

```sql
SELECT TOP(1) [q].[Id], [q].[Author], [q].[Text], [q].[CreatedAt]
FROM [Quotes] AS [q]
WHERE [q].[Id] = @id AND [q].[IsDeleted] = CAST(0 AS bit)
```

Response:
```json
{"id":201,"author":"Marcus Aurelius","text":"The impediment to action advances action. What stands in the way becomes the way.","createdAt":"2026-05-30T06:25:24Z"}
```

### WRITE: `POST /api/quotes`

Request body (command shape, not entity):
```json
{"author":"Marcus Aurelius","text":"The impediment to action advances action. What stands in the way becomes the way."}
```

EF Core emits a full **INSERT** (domain entity created by `Quote.Create()`):

```sql
INSERT INTO [Quotes] ([Author], [CreatedAt], [IsDeleted], [Text])
OUTPUT INSERTED.[Id]
VALUES (@p0, @p1, @p2, @p3);
```

Response: `{"id":201}` — just the new ID, not the full entity.

### DELETE: `DELETE /api/quotes/201`

Delete **loads the full domain entity** first (5 columns) because `SoftDelete()` is a
business method — the domain model must be in memory to enforce it:

```sql
-- Step 1: load the entity (full columns — change tracked)
SELECT TOP(1) [q].[Id], [q].[Author], [q].[CreatedAt], [q].[IsDeleted], [q].[Text]
FROM [Quotes] AS [q]
WHERE [q].[Id] = @id

-- Step 2: SoftDelete() sets IsDeleted = true → EF Core UPDATE
UPDATE [Quotes] SET [IsDeleted] = @p0
OUTPUT 1
WHERE [Id] = @p1;
```

---

## The Contrast

| | Read (`GET`) | Write (`POST`) | Delete (`DELETE`) |
|---|---|---|---|
| Path | `IQuoteQueryService` | `CreateQuoteCommandHandler` | `IQuoteRepository` |
| Columns fetched | 4 | N/A (INSERT) | 5 (full entity) |
| Change tracking | `AsNoTracking` | N/A | Yes — needed for UPDATE |
| Domain entity loaded | No — projection only | Created via `Quote.Create()` | Yes — `SoftDelete()` needs it |
| Business rules | None | `Quote.Create()` validation | `SoftDelete()` on aggregate |

---

## How to Verify

```bash
cd Day12/Task1/QuotesApi
dotnet run --launch-profile http

# Seed
curl -X POST http://localhost:5182/api/dev/seed

# Register + login
curl -X POST http://localhost:5182/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Password123!"}'

TOKEN=$(curl -s -X POST http://localhost:5182/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Password123!"}' \
  | grep -o '"accessToken":"[^"]*"' | cut -d'"' -f4)

# Read path (watch terminal for 4-column SELECT)
curl "http://localhost:5182/api/quotes?page=1&size=3"
curl http://localhost:5182/api/quotes/1

# Write path (watch terminal for INSERT)
curl -X POST http://localhost:5182/api/quotes \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"author":"Seneca","text":"Nusquam est qui ubique est."}'

# Delete path (watch terminal for SELECT 5 cols + UPDATE)
curl -X DELETE http://localhost:5182/api/quotes/1 \
  -H "Authorization: Bearer $TOKEN"
```

Watch the terminal — EF Core logs every SQL statement at `Information` level
(`Microsoft.EntityFrameworkCore.Database.Command` is set to `Information` in
`appsettings.Development.json`).
