# Day 13 — Signals + Zoneless + Standalone

# Day 13 — Task 2: Quotes List + Detail Component

## Brief Given to Claude Code

I directed Claude Code to extend my existing Angular quotes app
from Week 1 with a list+detail component against my real
ASP.NET Core API running on http://localhost:5255.

Real endpoints:
- GET /api/quotes       → Quote[]
- GET /api/quotes/{id}  → Quote

Real Quote model fields:
- id: number
- author: string
- text: string
- isDeleted: boolean
- createdAt: string

Instructions given to agent:
- Add getById(id: number): Observable<Quote> to QuotesService
- Add onQuoteClick(id) with inject(Router) to QuotesComponent
- Create QuoteDetailComponent using inject(ActivatedRoute),
  inject(Router), inject(QuotesService)
- Signals: loading, error, quote — no any anywhere
- Use switchMap on route.paramMap to prevent stale race responses
- Handle all 4 states in templates: loading/error/empty/data
- inject() only, no constructor injection

## Agent Output

### quotes.service.ts — getById added
getById(id: number): Observable<Quote> {
  return this.http.get<Quote>(`${this.apiUrl}/${id}`);
}

### quote-detail.component.ts — key logic
this.subscription = this.route.paramMap.pipe(
  tap(() => {
    this.loading.set(true);
    this.error.set(null);
    this.quote.set(null);
  }),
  switchMap(params => {
    const id = Number(params.get('id'));
    return this.quotesService.getById(id);
  }),
).subscribe({
  next: (q) => { this.quote.set(q); this.loading.set(false); },
  error: (err) => {
    this.error.set('Failed to load quote. Please try again.');
    this.loading.set(false);
  }
});

## Verification Log

### States Exercised

Loading
- How tested: DevTools Network → Slow 3G → refresh
- Proof: Screenshots/loading-state.png

Error
- How tested: Stopped backend, refreshed page
- Proof: Screenshots/error-state.png

Empty
- How tested: Typed filter with no matching author
- Proof: Screenshots/empty-state.png

Data
- How tested: Normal flow, backend running
- Proof: Screenshots/data-state.png

Detail
- How tested: Clicked a quote from the list
- Proof: Screenshots/onclick.png

Race condition
- How tested: Clicked quote 1 then immediately quote 2
- Proof: switchMap cancelled first request, correct quote shown

### Bug 1 Caught — Error + Empty State Collision

Agent produced:
@if (!loading()) {
  @if (filteredQuotes().length === 0) {
    No quotes found.
  }
}

Problem: when error() is true, loading() is false and
filteredQuotes() is empty — so both the error message
AND "No quotes found" appeared at the same time.
Proof: Screenshots/bug1-before-fix.png

Fix applied:
@if (!loading() && !error()) {
  @if (filteredQuotes().length === 0) {
    No quotes found.
  }
}

### Bug 2 Caught — Silent NaN on Invalid Route ID

Agent produced:
const id = Number(params.get('id'));
return this.quotesService.getById(id);

Problem: if route param is not a valid number (e.g. /quotes/abc),
Number() returns NaN silently and calls GET /api/quotes/NaN
producing a confusing 400 response with no user feedback.

Fix applied:
const id = Number(params.get('id'));
if (isNaN(id)) {
  this.error.set('Invalid quote ID.');
  this.loading.set(false);
  return EMPTY;
}
return this.quotesService.getById(id);

## What Breaks if Week-1 API Contract Changes

text renamed to content
- Template breaks silently, quotes show blank

author renamed to name
- Template breaks silently, author shows blank

id changed from number to string
- TypeScript catches it at compile time

/api/quotes/{id} URL changes
- 404 error, error signal handles it gracefully

createdAt removed
- DatePipe throws, template breaks

isDeleted removed
- Badge never shows, silent graceful degradation

## Screenshots
- loading-state.png — loading spinner on slow network
- error-state.png — error message when backend is down
- empty-state.png — empty state when filter has no match
- data-state.png — all quotes loaded normally
- onclick.png — detail page after clicking a quote
- bug1-before-fix.png — both error and empty showing at same time