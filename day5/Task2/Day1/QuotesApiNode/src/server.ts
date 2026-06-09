import Database from 'better-sqlite3';
import pino from 'pino';
import { createServer } from 'http';
import { URL } from 'url';

// ============================================================================
// SETUP: Logger and Database
// ============================================================================

const logger = pino();

// Initialize SQLite database - automatically creates file if it doesn't exist
const db = new Database('quotes.db');

// Enable foreign keys and create the quotes table if it doesn't exist
db.pragma('journal_mode = WAL');
db.exec(`
  CREATE TABLE IF NOT EXISTS quotes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    author TEXT NOT NULL,
    text TEXT NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
  )
`);

// ============================================================================
// TYPES
// ============================================================================

interface Quote {
  id: number;
  author: string;
  text: string;
  created_at: string;
}

interface JsonBody {
  author?: string;
  text?: string;
}

interface ErrorResponse {
  error: string;
  details?: string;
}

// ============================================================================
// HELPER FUNCTIONS
// ============================================================================

/**
 * Parse request body as JSON
 */
function parseJsonBody(
  req: NodeJS.ReadableStream,
): Promise<JsonBody> {
  return new Promise((resolve, reject) => {
    let body = '';
    req.on('data', (chunk) => {
      body += chunk.toString();
    });
    req.on('end', () => {
      try {
        const parsed: JsonBody = body ? JSON.parse(body) : {};
        resolve(parsed);
      } catch (error) {
        reject(new Error('Invalid JSON'));
      }
    });
    req.on('error', reject);
  });
}

/**
 * Send JSON response with status code
 */
function sendJson(
  res: NodeJS.WritableStream & { statusCode?: number },
  statusCode: number,
  data: Quote[] | Quote | ErrorResponse,
): void {
  res.statusCode = statusCode;
  res.setHeader('Content-Type', 'application/json');
  res.end(JSON.stringify(data));
}

/**
 * Validate POST request body
 */
function validateQuoteInput(
  body: JsonBody,
): { valid: boolean; error?: string } {
  if (!body.author || typeof body.author !== 'string') {
    return { valid: false, error: 'Missing or invalid "author" field' };
  }
  if (!body.text || typeof body.text !== 'string') {
    return { valid: false, error: 'Missing or invalid "text" field' };
  }
  return { valid: true };
}

// ============================================================================
// ROUTE HANDLERS
// ============================================================================

/**
 * GET /api/quotes?page=N&size=N
 * Returns paginated list of quotes
 */
async function getAllQuotes(
  req: NodeJS.ReadableStream & { url?: string },
  res: NodeJS.WritableStream & { statusCode?: number },
): Promise<void> {
  try {
    const urlObj = new URL(req.url ?? '', 'http://localhost');
    const page = Math.max(1, parseInt(urlObj.searchParams.get('page') ?? '1'));
    const size = Math.max(1, Math.min(100, parseInt(urlObj.searchParams.get('size') ?? '10')));

    const offset = (page - 1) * size;

    // Get total count
    const countResult = db
      .prepare('SELECT COUNT(*) as count FROM quotes')
      .get() as { count: number };
    const total = countResult.count;

    // Get paginated data
    const quotes = db
      .prepare('SELECT * FROM quotes ORDER BY created_at DESC LIMIT ? OFFSET ?')
      .all(size, offset) as Quote[];

    sendJson(res, 200, quotes);
  } catch (error) {
    logger.error(error);
    sendJson(res, 500, { error: 'Internal server error' });
  }
}

/**
 * POST /api/quotes
 * Creates a new quote
 */
async function createQuote(
  req: NodeJS.ReadableStream & { url?: string },
  res: NodeJS.WritableStream & { statusCode?: number },
): Promise<void> {
  try {
    const body = await parseJsonBody(req);

    // Validate input
    const validation = validateQuoteInput(body);
    if (!validation.valid) {
      sendJson(res, 400, {
        error: 'Validation error',
        details: validation.error,
      });
      return;
    }

    // Insert into database
    const stmt = db.prepare('INSERT INTO quotes (author, text) VALUES (?, ?)');
    const result = stmt.run(body.author, body.text);

    // Fetch created quote
    const quote = db
      .prepare('SELECT * FROM quotes WHERE id = ?')
      .get(result.lastInsertRowid) as Quote | undefined;

    if (!quote) {
      sendJson(res, 500, { error: 'Failed to retrieve created quote' });
      return;
    }

    sendJson(res, 201, quote);
  } catch (error) {
    logger.error(error);
    sendJson(res, 400, {
      error: 'Bad request',
      details: error instanceof Error ? error.message : 'Unknown error',
    });
  }
}

/**
 * GET /api/quotes/:id
 * Returns a single quote by ID
 */
async function getQuoteById(
  req: NodeJS.ReadableStream & { url?: string },
  res: NodeJS.WritableStream & { statusCode?: number },
  id: string,
): Promise<void> {
  try {
    const quoteId = parseInt(id);

    if (isNaN(quoteId)) {
      sendJson(res, 400, { error: 'Invalid quote ID' });
      return;
    }

    const quote = db
      .prepare('SELECT * FROM quotes WHERE id = ?')
      .get(quoteId) as Quote | undefined;

    if (!quote) {
      sendJson(res, 404, { error: 'Quote not found' });
      return;
    }

    sendJson(res, 200, quote);
  } catch (error) {
    logger.error(error);
    sendJson(res, 500, { error: 'Internal server error' });
  }
}

/**
 * DELETE /api/quotes/:id
 * Deletes a quote by ID
 */
async function deleteQuote(
  req: NodeJS.ReadableStream & { url?: string },
  res: NodeJS.WritableStream & { statusCode?: number },
  id: string,
): Promise<void> {
  try {
    const quoteId = parseInt(id);

    if (isNaN(quoteId)) {
      sendJson(res, 400, { error: 'Invalid quote ID' });
      return;
    }

    // Check if quote exists
    const quote = db
      .prepare('SELECT * FROM quotes WHERE id = ?')
      .get(quoteId) as Quote | undefined;

    if (!quote) {
      sendJson(res, 404, { error: 'Quote not found' });
      return;
    }

    // Delete the quote
    const stmt = db.prepare('DELETE FROM quotes WHERE id = ?');
    stmt.run(quoteId);

    sendJson(res, 200, { ...quote });
  } catch (error) {
    logger.error(error);
    sendJson(res, 500, { error: 'Internal server error' });
  }
}

// ============================================================================
// ROUTER
// ============================================================================

/**
 * Main request router
 */
async function router(
  req: NodeJS.ReadableStream & { url?: string; method?: string },
  res: NodeJS.WritableStream & { statusCode?: number },
): Promise<void> {
  const url = req.url ?? '/';
  const method = req.method ?? 'GET';

  // Log incoming request
  logger.info({ method, path: url }, 'Incoming request');

  // Parse URL
  const pathname = new URL(url, 'http://localhost').pathname;

  // Route: GET /api/quotes - Get all quotes with pagination
  if (method === 'GET' && pathname === '/api/quotes') {
    await getAllQuotes(req, res);
    return;
  }

  // Route: POST /api/quotes - Create new quote
  if (method === 'POST' && pathname === '/api/quotes') {
    await createQuote(req, res);
    return;
  }

  // Route: GET /api/quotes/:id - Get quote by ID
  if (method === 'GET' && pathname.match(/^\/api\/quotes\/\d+$/)) {
    const id = pathname.split('/')[3] ?? '';
    await getQuoteById(req, res, id);
    return;
  }

  // Route: DELETE /api/quotes/:id - Delete quote by ID
  if (method === 'DELETE' && pathname.match(/^\/api\/quotes\/\d+$/)) {
    const id = pathname.split('/')[3] ?? '';
    await deleteQuote(req, res, id);
    return;
  }

  // Route not found
  res.statusCode = 404;
  res.setHeader('Content-Type', 'application/json');
  res.end(JSON.stringify({ error: 'Not found' }));
}

// ============================================================================
// SERVER SETUP
// ============================================================================

const PORT = 3000;
const server = createServer(router);

// Track active requests for graceful shutdown
let activeRequests = 0;
server.on('request', () => {
  activeRequests++;
});
server.on('finish', () => {
  activeRequests--;
});

// Graceful shutdown handler
function gracefulShutdown(): void {
  logger.info('Shutting down gracefully...');

  // Stop accepting new requests
  server.close(() => {
    logger.info('Server closed');

    // Wait for in-flight requests to complete
    const shutdownTimeout = setInterval(() => {
      if (activeRequests === 0) {
        clearInterval(shutdownTimeout);
        // Close database
        db.close();
        logger.info('Database closed');
        process.exit(0);
      }
    }, 100);

    // Force exit after 10 seconds
    setTimeout(() => {
      logger.warn('Forcing shutdown after 10 seconds');
      db.close();
      process.exit(1);
    }, 10000);
  });
}

// Handle SIGINT (Ctrl+C)
process.on('SIGINT', gracefulShutdown);
process.on('SIGTERM', gracefulShutdown);

// Start server
server.listen(PORT, () => {
  logger.info(`Quotes API running on http://localhost:${PORT}`);
  logger.info('Available endpoints:');
  logger.info('  GET    /api/quotes?page=1&size=10');
  logger.info('  POST   /api/quotes');
  logger.info('  GET    /api/quotes/{id}');
  logger.info('  DELETE /api/quotes/{id}');
});
