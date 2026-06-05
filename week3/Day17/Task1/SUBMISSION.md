Day 16 Submission

════════════════════════════════════════
PART 1 — THE BRIEF I GAVE THE AGENT
════════════════════════════════════════

## You are working inside my existing Angular project: quotes-frontend.

My API
Base URL: http://localhost:5255/api/quotes
Endpoints:
- GET  /api/quotes        → returns Quote[]  (requires Bearer token)
- GET  /api/quotes/{id}   → returns Quote    (requires Bearer token)
- POST /api/quotes        → creates a Quote
- DELETE /api/quotes/{id} → deletes a Quote

Quote model: { id: number, text: string, author: string, createdAt: string }

What already exists (do NOT rewrite these):
- quotes.service.ts         → has getAll(), getById(), createQuote()
- auth.service.ts           → has getToken() and isLoggedIn()
- auth.interceptor.ts       → automatically adds Bearer token to every request
- quotes-list.component.ts  → currently calls HttpClient directly in constructor
- quote-detail.component.ts → currently calls HttpClient directly in ngOnInit

The problem:
Both components inject HttpClient themselves and make API calls directly.
There is no shared state — every navigation re-fetches everything.

What I want you to build:
Create src/app/features/quotes/quotes-state.service.ts with:
- signals: quotes, loading, error, selectedQuote
- loadQuotes() — calls QuotesService.getAll(), updates signals
- loadQuote(id) — calls QuotesService.getById(id), updates selectedQuote
- setError(msg) — only way to write error/loading from outside the service

Update QuotesListComponent:
- Remove HttpClient injection
- Inject QuotesStateService, call loadQuotes() in constructor
- Keep pagedQuotes, totalPages, authorCount as local computed signals

Update QuoteDetailComponent:
- Remove HttpClient injection
- Use takeUntilDestroyed() instead of manual Subscription
- Call state.loadQuote(id) in ngOnInit

Constraints:
- Do NOT install NgRx
- Do NOT modify QuotesService
- Do NOT add auth headers manually


════════════════════════════════════════
PART 2 — AGENT OUTPUT
════════════════════════════════════════

## FILE: quotes-state.service.ts

import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { QuotesService, Quote } from '../../quotes.service';

@Injectable({ providedIn: 'root' })
export class QuotesStateService {
  private readonly quotesService = inject(QuotesService);

  readonly quotes = signal<Quote[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly selectedQuote = signal<Quote | null>(null);

  loadQuotes(): void {
    this.loading.set(true);
    this.error.set(null);
    this.quotesService.getAll().subscribe({
      next: (data) => {
        this.quotes.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Failed to load quotes.');
        this.loading.set(false);
      },
    });
  }

  loadQuote(id: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.selectedQuote.set(null);
    this.quotesService.getById(id).subscribe({
      next: (quote) => {
        this.selectedQuote.set(quote);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(err.status === 404 ? 'NOT_FOUND' : 'Failed to load quote.');
        this.loading.set(false);
      },
    });
  }

  setError(msg: string): void {
    this.error.set(msg);
    this.loading.set(false);
  }
}

## FILE: quote-detail.component.ts

import {
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { QuotesStateService } from './quotes-state.service';

const TAGS = ['wisdom', 'motivation', 'philosophy', 'success', 'life', 'humor', 'truth', 'change', 'perseverance', 'education'];
const CATS = ['classic', 'modern'];

@Component({
  selector: 'app-quote-detail',
  standalone: true,
  imports: [DatePipe, RouterLink],
  styleUrl: './quote-detail.component.css',
  template: `
    <div class="detail-page">
      <a class="back-link" [routerLink]="['/quotes']">← Back to list</a>
      @if (loading()) {
        <p class="state-msg">Loading…</p>
      } @else if (notFound()) {
        <p class="state-msg">Quote not found.</p>
      } @else if (error()) {
        <p class="state-msg error">{{ error() }}</p>
      } @else if (quote()) {
        <h2>Details</h2>
        <div class="detail-card">
          <table class="detail-table">
            <tbody>
              <tr><th>QUOTE ID</th><td>{{ quote()!.id }}</td></tr>
              <tr><th>QUOTE</th><td><em>"{{ quote()!.text }}"</em></td></tr>
              <tr><th>BY AUTHOR</th><td>{{ quote()!.author }}</td></tr>
              <tr><th>CREATED ON</th><td>{{ quote()!.createdAt | date: 'mediumDate' }}</td></tr>
              <tr>
                <th>TAG</th>
                <td><span class="badge tag-{{ tagFor(quote()!.id) }}">{{ tagFor(quote()!.id) }}</span></td>
              </tr>
              <tr>
                <th>CATEGORY</th>
                <td><span class="badge cat-{{ catFor(quote()!.id) }}">{{ catFor(quote()!.id) }}</span></td>
              </tr>
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class QuoteDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly state = inject(QuotesStateService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = this.state.loading;
  readonly quote = this.state.selectedQuote;
  readonly notFound = computed(() => this.state.error() === 'NOT_FOUND');
  readonly error = computed(() => {
    const e = this.state.error();
    return e && e !== 'NOT_FOUND' ? e : null;
  });

  tagFor(id: number): string { return TAGS[id % TAGS.length]; }
  catFor(id: number): string { return CATS[id % CATS.length]; }

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const id = Number(params.get('id'));
          if (isNaN(id)) {
            this.state.setError('Invalid quote ID.');
            return EMPTY;
          }
          this.state.loadQuote(id);
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }
}

### NgRx Threshold Rule

I would move from signals to NgRx when the app becomes large and complex.
Specifically when multiple features need to share data from one central place,
when I need to track every state change for debugging, and when the app has
side effects like HTTP calls that need to be managed in a structured way.
For a small app like my Quotes app, signals-in-a-service is enough.


════════════════════════════════════════
PART 3 — VERIFICATION LOG
════════════════════════════════════════

## States exercised:
-- Concurrent requests bug — before and after:

  BEFORE (ConcurrentRequests_BEFORE.png):
  Network tab shows GET /api/quotes/6 firing twice, GET /api/quotes/9,
  GET /api/quotes/7, GET /api/quotes/13 all firing without cancelling
  previous requests. Total 10 requests. Initiator shows
  quotes-state.service.ts:33 but subscriptions were not cleaned up
  because quote-detail.component.ts was using manual Subscription
  instead of takeUntilDestroyed().

  AFTER (ConcurrentRequests_AFTER.png):
  Network tab shows GET /api/quotes/9, GET /api/quotes/12,
  GET /api/quotes/6 each firing exactly once. No duplicate requests.
  Total 6 requests. Initiator shows quotes-state.service.ts:38
  confirming takeUntilDestroyed() is now tearing down subscriptions
  correctly on each navigation.

One concrete bug caught and fixed:
Agent kept QuotesService injected directly in quote-detail.component.ts
with manual Subscription and OnDestroy — ignoring two explicit instructions
in the brief. Fixed by replacing with QuotesStateService injection and
takeUntilDestroyed(this.destroyRef).

Before:
  private readonly quotesService = inject(QuotesService);
  private subscription?: Subscription;
  ngOnDestroy(): void { this.subscription?.unsubscribe(); }

After:
  private readonly state = inject(QuotesStateService);
  private readonly destroyRef = inject(DestroyRef);
  // takeUntilDestroyed(this.destroyRef) handles cleanup automatically

Second bug caught:
Component was directly mutating service signals:
  this.state.error.set('Invalid quote ID.');   // wrong
  this.state.loading.set(false);               // wrong
Fixed by adding setError() to the service:
  this.state.setError('Invalid quote ID.');    // correct

What breaks if the API contract changes:
- createdAt removed from GET /api/quotes/{id} → detail page silently
  shows blank date, no error thrown
- id renamed to quoteId → pagination, tagFor(), and routerLink all
  silently break since every component references quote.id
- GET /api/quotes starts requiring ?page=1&pageSize=10 → QuotesService
  sends no params, server returns 400, entire list shows error state
- 401 changes to 403 for expired tokens → auth.interceptor.ts only
  handles 401 redirects, 403 falls through to generic error message
  instead of redirecting to login