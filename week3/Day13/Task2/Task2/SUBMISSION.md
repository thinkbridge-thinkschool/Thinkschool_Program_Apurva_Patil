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

---

## Agent Output — Full Files as Generated

### quotes.service.ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Quote {
  id: number;
  author: string;
  text: string;
  isDeleted: boolean;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = 'http://localhost:5255/api/quotes';

  getAll(): Observable<Quote[]> {
    return this.http.get<Quote[]>(this.apiUrl);
  }

  getById(id: number): Observable<Quote> {
    return this.http.get<Quote>(`${this.apiUrl}/${id}`);
  }
}

### quotes.component.ts
import { Component, computed, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { Quote, QuotesService } from '../quotes.service';

@Component({
  selector: 'app-quotes',
  imports: [DatePipe],
  templateUrl: './quotes.component.html',
  styleUrl: './quotes.component.css',
})
export class QuotesComponent {
  private readonly quotesService = inject(QuotesService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly authorFilter = signal('');
  readonly error = signal<string | null>(null);
  private readonly allQuotes = signal<Quote[]>([]);

  readonly filteredQuotes = computed(() => {
    const filter = this.authorFilter().trim().toLowerCase();
    if (!filter) return this.allQuotes();
    return this.allQuotes().filter((q) =>
      q.author.toLowerCase().includes(filter)
    );
  });

  constructor() {
    effect(() => {
      console.log('[QuotesComponent] authorFilter changed:', this.authorFilter());
    });
    this.loadQuotes();
  }

  private loadQuotes(): void {
    this.loading.set(true);
    this.quotesService.getAll().subscribe({
      next: (quotes) => {
        this.allQuotes.set(quotes);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load quotes:', err);
        this.error.set('Failed to load quotes. Please try again.');
        this.loading.set(false);
      },
    });
  }

  onFilterInput(event: Event): void {
    this.authorFilter.set((event.target as HTMLInputElement).value);
  }

  onQuoteClick(id: number): void {
    this.router.navigate(['/quotes', id]);
  }
}

### quotes.component.html — AS AGENT GENERATED (before bug fix)
<section class="quotes-container">
  <h1>Quotes</h1>

  <div class="filter-bar">
    <label for="authorFilter">Filter by author</label>
    <input
      id="authorFilter"
      type="text"
      placeholder="e.g. Aristotle"
      [value]="authorFilter()"
      (input)="onFilterInput($event)"
    />
  </div>

  @if (loading()) {
    <p class="status">Loading quotes…</p>
  }
  @if (error()) {
    <p class="status" style="color: red;">{{ error() }}</p>
  }

  @if (!loading()) {
    @if (filteredQuotes().length === 0) {
      <p class="status">No quotes found.</p>
    }
    @for (q of filteredQuotes(); track q.id) {
      <article class="quote-card" (click)="onQuoteClick(q.id)">
        <blockquote>{{ q.text }}</blockquote>
        <footer>— {{ q.author }}</footer>
        <time>{{ q.createdAt | date: 'mediumDate' }}</time>
      </article>
    }
  }
</section>

### quote-detail.component.ts — AS AGENT GENERATED (before bug fix)
import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { switchMap, tap } from 'rxjs/operators';
import { Quote, QuotesService } from '../../quotes.service';

@Component({
  selector: 'app-quote-detail',
  imports: [DatePipe],
  templateUrl: './quote-detail.component.html',
  styleUrl: './quote-detail.component.css',
})
export class QuoteDetailComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly quotesService = inject(QuotesService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly quote = signal<Quote | null>(null);

  private subscription?: Subscription;

  ngOnInit(): void {
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
      next: (q) => {
        this.quote.set(q);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load quote:', err);
        this.error.set('Failed to load quote. Please try again.');
        this.loading.set(false);
      },
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
  }

  goBack(): void {
    this.router.navigate(['/quotes']);
  }
}

### quote-detail.component.html
<section class="detail-container">
  <button class="back-btn" (click)="goBack()">&#8592; Back to Quotes</button>

  @if (loading()) {
    <p class="status">Loading quote…</p>
  }

  @if (error()) {
    <p class="status error">{{ error() }}</p>
  }

  @if (!loading() && !error() && !quote()) {
    <p class="status">Quote not found.</p>
  }

  @if (quote(); as q) {
    <article class="quote-detail-card">
      <blockquote>{{ q.text }}</blockquote>
      <footer>— {{ q.author }}</footer>
      <time>{{ q.createdAt | date: 'mediumDate' }}</time>
      @if (q.isDeleted) {
        <span class="deleted-badge">Deleted</span>
      }
    </article>
  }
</section>

---

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
- Proof: switchMap on route.paramMap cancels previous HTTP call
  when route param changes. Visible in quote-detail.component.ts
  agent output above.

---

## Bugs Caught and Fixed

### Bug 1 — Error and Empty State Showing at Same Time

BEFORE (agent output):
@if (!loading()) {
  @if (filteredQuotes().length === 0) {
    No quotes found.
  }
}

Problem: when error() is true, loading() is also false
and filteredQuotes() is empty — so both the error message
AND "No quotes found" appeared at the same time.
Proof of bug: Screenshots/bug1-before-fix.png

AFTER (fix I told agent to apply):
@if (!loading() && !error()) {
  @if (filteredQuotes().length === 0) {
    No quotes found.
  }
}

### Bug 2 — Silent NaN on Invalid Route ID

BEFORE (agent output):
switchMap(params => {
  const id = Number(params.get('id'));
  return this.quotesService.getById(id);
}),

Problem: if route param is not a valid number e.g. /quotes/abc,
Number() returns NaN silently and calls GET /api/quotes/NaN
producing a confusing 400 response with no user feedback.

AFTER (fix I told agent to apply):
switchMap(params => {
  const id = Number(params.get('id'));
  if (isNaN(id)) {
    this.error.set('Invalid quote ID.');
    this.loading.set(false);
    return EMPTY;
  }
  return this.quotesService.getById(id);
}),

---

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

---

## Screenshots
- loading-state.png — loading spinner on slow network
- error-state.png — error message when backend is down
- empty-state.png — empty state when filter has no match
- data-state.png — all quotes loaded normally
- onclick.png — detail page after clicking a quote
- bug1-before-fix.png — both error and empty showing at same time