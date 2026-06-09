# QuotesApiNode - Minimal API without Express

A minimal Node.js 24 + TypeScript quotes API using native HTTP, SQLite (better-sqlite3), and structured logging (pino).

## Features

✅ Native Node.js HTTP module (no Express)  
✅ TypeScript strict mode  
✅ No build step - runs directly with `node --loader tsx`  
✅ SQLite database with auto-creation  
✅ Structured logging with Pino  
✅ Graceful shutdown handling  
✅ JSON request/response validation  
✅ Pagination support  

## Installation

```bash
# Install dependencies
npm install
```

## Running the Server

```bash
# Start the server
npm start
# or manually:
node --loader tsx src/server.ts
```

The server will start on `http://localhost:3000` and create a `quotes.db` SQLite database file automatically.

## API Endpoints

### Get All Quotes (Paginated)
```bash
GET /api/quotes?page=1&size=10
```

**Query Parameters:**
- `page` (optional, default: 1) - Page number
- `size` (optional, default: 10, max: 100) - Items per page

**Example:**
```bash
curl http://localhost:3000/api/quotes
curl http://localhost:3000/api/quotes?page=2&size=5
```

### Create Quote
```bash
POST /api/quotes
Content-Type: application/json

{
  "author": "string (required)",
  "text": "string (required)"
}
```

**Example:**
```bash
curl -X POST http://localhost:3000/api/quotes \
  -H "Content-Type: application/json" \
  -d '{"author":"Albert Einstein","text":"Life is like riding a bicycle"}'
```

### Get Quote by ID
```bash
GET /api/quotes/{id}
```

**Example:**
```bash
curl http://localhost:3000/api/quotes/1
```

### Delete Quote
```bash
DELETE /api/quotes/{id}
```

**Example:**
```bash
curl -X DELETE http://localhost:3000/api/quotes/1
```

## Complete Test Suite

Run these commands to test all endpoints:

### 1. Create a quote
```bash
curl -X POST http://localhost:3000/api/quotes \
  -H "Content-Type: application/json" \
  -d '{"author":"Albert Einstein","text":"Life is like riding a bicycle"}'
```

### 2. Create another quote
```bash
curl -X POST http://localhost:3000/api/quotes \
  -H "Content-Type: application/json" \
  -d '{"author":"Mark Twain","text":"The secret of getting ahead is getting started"}'
```

### 3. Get all quotes with pagination
```bash
curl http://localhost:3000/api/quotes
curl http://localhost:3000/api/quotes?page=1&size=10
```

### 4. Get specific quote by ID
```bash
curl http://localhost:3000/api/quotes/1
```

### 5. Delete a quote
```bash
curl -X DELETE http://localhost:3000/api/quotes/1
```

### 6. Verify deletion (should return 404)
```bash
curl http://localhost:3000/api/quotes/1
```

### 7. Test validation error (missing fields)
```bash
curl -X POST http://localhost:3000/api/quotes \
  -H "Content-Type: application/json" \
  -d '{"author":"No text"}'
```

## Error Handling

**400 Bad Request** - Invalid input or missing required fields  
**404 Not Found** - Quote ID doesn't exist  
**500 Internal Server Error** - Server-side error  

All errors return JSON:
```json
{
  "error": "Error message",
  "details": "Additional details (if applicable)"
}
```

## Graceful Shutdown

Press `Ctrl+C` to gracefully shutdown the server:
- Stops accepting new requests
- Waits for in-flight requests to complete
- Closes database connection cleanly
- Exits safely (with 10-second force timeout)

## TypeScript Configuration

The `tsconfig.json` includes:
- `"strict": true` - All strict type checking options enabled
- `"noUncheckedIndexedAccess": true` - Safe array/object indexing
- `"target": "ES2024"` - Modern JavaScript features
- Source maps and declaration files enabled

## Project Structure

```
QuotesApiNode/
├── package.json          # Dependencies and scripts
├── tsconfig.json         # TypeScript configuration
├── src/
│   └── server.ts         # Main server file with all routes
├── quotes.db             # SQLite database (auto-created)
└── .gitignore            # Git ignore file
```

## Database Schema

The `quotes` table is automatically created with:
- `id` - Auto-incrementing primary key
- `author` - Text field (required)
- `text` - Text field (required)
- `created_at` - Timestamp (auto-set)

## Logging

Uses Pino for structured logging. Logs include:
- Request method and path on each incoming request
- Errors with full stack traces
- Server startup and shutdown events

## Dependencies

- **better-sqlite3** - Fast SQLite driver
- **pino** - Structured logging
- **tsx** - TypeScript executor (no build step needed)
- **typescript** - TypeScript compiler (for type checking)

## Notes

- The `tsx` loader allows running TypeScript directly without a compilation step
- WAL mode is enabled in SQLite for better concurrency
- Maximum page size is limited to 100 items for performance
- All responses are JSON-formatted
- Empty POST body defaults to empty object (caught by validation)
