# Day 13 — Signals + Zoneless + Standalone
## Submission: Brief → Agent Output → Verification Log
## Week 2 | Task 1
---

## (1) Brief to the Agent

> **Real Week-1 API endpoint:**
> `GET http://localhost:5182/api/quotes?page=1&size=5`
>
> **Actual JSON shape returned** (plain array, no wrapper):
> ```json
> [
>   {
>     "id": 100,
>     "author": "Confucius",
>     "text": "Quote 20 by Confucius.",
>     "isDeleted": false,
>     "createdAt": "2026-05-29T05:55:38.727316+00:00"
>   }
> ]
> ```
> Fields: `id` (number), `author` (string), `text` (string),
> `createdAt` (ISO-8601 string), `isDeleted` (boolean).
>
> Auth endpoints also used:
> - `POST /api/auth/register` — body `{ email, password }`, returns `{ id, email }`
> - `POST /api/auth/login` — body `{ email, password }`, returns `{ accessToken, refreshToken, expiresIn }`
> - `DELETE /api/quotes/{id}` — requires `Authorization: Bearer <token>`
> - `POST /api/quotes` — body `{ author, text }`, requires auth
>
> **Goal for the agent:**
> Build a standalone Angular 21 app (no NgModules anywhere) wired to this API.
> Requirements:
> - `provideZonelessChangeDetection()` in providers — no zone.js
> - `inject()` at field level everywhere — zero constructor injection
> - `signal<Quote[]>([])` for the list, `signal(1)` for current page
> - `computed()` that derives `hasNext` from two signals:
>   `quotes().length === pageSize` (true = full page returned, more may exist)
>   and `hasPrev` = `page() > 1`
> - `effect()` that automatically re-fetches when the `page` signal changes
> - Template must use `@for (quote of quotesService.quotes(); track quote.id)`
> - `@if` / `@else if` / `@else` guards for loading / error / empty / data states
> - `@switch` on a card-level state signal for delete confirmation flow
> - Auth-gated UI: create form and delete buttons only render when `isLoggedIn()` computed is true

---

## (2) Agent Output — Verbatim

### `src/app/app.config.ts`
```typescript
import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { provideHttpClient } from '@angular/common/http';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideHttpClient(),
  ],
};
```

### `src/app/services/quotes.service.ts`
```typescript
import { Injectable, signal, computed, effect, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AuthService } from './auth.service';

export interface Quote {
  id: number;
  author: string;
  text: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  readonly quotes = signal<Quote[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = 5;

  // The API returns a plain array; if we got a full page there may be more
  readonly hasNext = computed(() => this.quotes().length === this.pageSize);
  readonly hasPrev = computed(() => this.page() > 1);

  constructor() {
    // re-fetch whenever page changes
    effect(() => {
      const p = this.page();
      this.loadPage(p);
    });
  }

  async loadPage(page: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const result = await firstValueFrom(
        this.http.get<Quote[]>(`/api/quotes?page=${page}&size=${this.pageSize}`)
      );
      this.quotes.set(result);
    } catch (e: any) {
      this.error.set(e?.message ?? 'Failed to load quotes');
    } finally {
      this.loading.set(false);
    }
  }

  nextPage(): void {
    if (this.hasNext()) this.page.update((p) => p + 1);
  }

  prevPage(): void {
    if (this.hasPrev()) this.page.update((p) => p - 1);
  }

  async createQuote(author: string, text: string): Promise<void> {
    const headers = new HttpHeaders({
      Authorization: `Bearer ${this.auth.accessToken()}`,
    });
    await firstValueFrom(
      this.http.post<Quote>('/api/quotes', { author, text }, { headers })
    );
    await this.loadPage(this.page());
  }

  async deleteQuote(id: number): Promise<void> {
    const headers = new HttpHeaders({
      Authorization: `Bearer ${this.auth.accessToken()}`,
    });
    await firstValueFrom(this.http.delete(`/api/quotes/${id}`, { headers }));
    this.quotes.update((qs) => qs.filter((q) => q.id !== id));
  }
}
```

### `src/app/services/auth.service.ts`
```typescript
import { Injectable, signal, computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _accessToken = signal<string | null>(
    localStorage.getItem('access_token')
  );
  private readonly _refreshToken = signal<string | null>(
    localStorage.getItem('refresh_token')
  );

  readonly isLoggedIn = computed(() => this._accessToken() !== null);
  readonly accessToken = this._accessToken.asReadonly();

  async login(email: string, password: string): Promise<void> {
    const tokens = await firstValueFrom(
      this.http.post<AuthTokens>('/api/auth/login', { email, password })
    );
    this.setTokens(tokens);
  }

  async register(email: string, password: string): Promise<void> {
    await firstValueFrom(
      this.http.post('/api/auth/register', { email, password })
    );
    await this.login(email, password);
  }

  logout(): void {
    this._accessToken.set(null);
    this._refreshToken.set(null);
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
  }

  private setTokens(tokens: AuthTokens): void {
    this._accessToken.set(tokens.accessToken);
    this._refreshToken.set(tokens.refreshToken);
    localStorage.setItem('access_token', tokens.accessToken);
    localStorage.setItem('refresh_token', tokens.refreshToken);
  }
}
```

### `src/app/components/quotes-list/quotes-list.ts`
```typescript
import { Component, inject } from '@angular/core';
import { QuotesService } from '../../services/quotes.service';
import { AuthService } from '../../services/auth.service';
import { QuoteCardComponent } from '../quote-card/quote-card';
import { CreateQuoteComponent } from '../create-quote/create-quote';

@Component({
  selector: 'app-quotes-list',
  imports: [QuoteCardComponent, CreateQuoteComponent],
  templateUrl: './quotes-list.html',
  styleUrl: './quotes-list.css',
})
export class QuotesListComponent {
  readonly quotesService = inject(QuotesService);
  readonly auth = inject(AuthService);
}
```

### `src/app/components/quotes-list/quotes-list.html`
```html
<section class="quotes-list">
  @if (auth.isLoggedIn()) {
    <app-create-quote />
  }

  @if (quotesService.loading()) {
    <div class="state-msg">Loading quotes…</div>
  } @else if (quotesService.error()) {
    <div class="state-msg error">
      Error: {{ quotesService.error() }}
      <button (click)="quotesService.loadPage(quotesService.page())">Retry</button>
    </div>
  } @else if (quotesService.quotes().length === 0) {
    <div class="state-msg empty">No quotes yet. Be the first to add one!</div>
  } @else {
    <div class="list">
      @for (quote of quotesService.quotes(); track quote.id) {
        <app-quote-card [quote]="quote" />
      }
    </div>

    <nav class="pagination">
      <button
        [disabled]="!quotesService.hasPrev()"
        (click)="quotesService.prevPage()"
      >
        ← Prev
      </button>

      <span class="page-label">Page {{ quotesService.page() }}</span>

      <button
        [disabled]="!quotesService.hasNext()"
        (click)="quotesService.nextPage()"
      >
        Next →
      </button>
    </nav>
  }
</section>
```

### `src/app/components/quote-card/quote-card.ts`
```typescript
import { Component, input, output, signal, inject } from '@angular/core';
import { Quote, QuotesService } from '../../services/quotes.service';
import { AuthService } from '../../services/auth.service';

type CardState = 'idle' | 'confirm-delete' | 'deleting';

@Component({
  selector: 'app-quote-card',
  imports: [],
  templateUrl: './quote-card.html',
  styleUrl: './quote-card.css',
})
export class QuoteCardComponent {
  private readonly quotesService = inject(QuotesService);
  readonly auth = inject(AuthService);

  readonly quote = input.required<Quote>();
  readonly deleted = output<number>();

  readonly state = signal<CardState>('idle');

  confirmDelete(): void {
    this.state.set('confirm-delete');
  }

  cancelDelete(): void {
    this.state.set('idle');
  }

  async doDelete(): Promise<void> {
    this.state.set('deleting');
    try {
      await this.quotesService.deleteQuote(this.quote().id);
      this.deleted.emit(this.quote().id);
    } catch {
      this.state.set('idle');
    }
  }
}
```

### `src/app/components/quote-card/quote-card.html`
```html
<article class="card">
  <blockquote>
    <p>{{ quote().text }}</p>
    <footer>— {{ quote().author }}</footer>
  </blockquote>

  @if (auth.isLoggedIn()) {
    <div class="actions">
      @switch (state()) {
        @case ('idle') {
          <button class="btn-delete" (click)="confirmDelete()">Delete</button>
        }
        @case ('confirm-delete') {
          <span class="confirm-text">Are you sure?</span>
          <button class="btn-danger" (click)="doDelete()">Yes, delete</button>
          <button class="btn-cancel" (click)="cancelDelete()">Cancel</button>
        }
        @case ('deleting') {
          <span class="muted">Deleting…</span>
        }
      }
    </div>
  }
</article>
```

### `src/app/components/auth-form/auth-form.ts`
```typescript
import { Component, output, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';

type Mode = 'login' | 'register';

@Component({
  selector: 'app-auth-form',
  imports: [FormsModule],
  templateUrl: './auth-form.html',
  styleUrl: './auth-form.css',
})
export class AuthFormComponent {
  private readonly auth = inject(AuthService);

  readonly mode = signal<Mode>('login');
  readonly email = signal('');
  readonly password = signal('');
  readonly submitting = signal(false);
  readonly formError = signal<string | null>(null);

  readonly done = output<void>();

  toggleMode(): void {
    this.mode.update((m) => (m === 'login' ? 'register' : 'login'));
    this.formError.set(null);
  }

  async submit(): Promise<void> {
    this.formError.set(null);
    this.submitting.set(true);
    try {
      if (this.mode() === 'login') {
        await this.auth.login(this.email(), this.password());
      } else {
        await this.auth.register(this.email(), this.password());
      }
      this.done.emit();
    } catch (e: any) {
      const msg =
        e?.error?.error ?? e?.error?.title ?? e?.message ?? 'Unknown error';
      this.formError.set(msg);
    } finally {
      this.submitting.set(false);
    }
  }
}
```

### `src/app/components/auth-form/auth-form.html`
```html
<div class="auth-form">
  <h2>{{ mode() === 'login' ? 'Sign in' : 'Create account' }}</h2>

  @if (formError()) {
    <p class="error">{{ formError() }}</p>
  }

  <form (ngSubmit)="submit()">
    <label>
      Email
      <input
        type="email"
        [ngModel]="email()"
        (ngModelChange)="email.set($event)"
        name="email"
        required
        autocomplete="email"
      />
    </label>

    <label>
      Password
      <input
        type="password"
        [ngModel]="password()"
        (ngModelChange)="password.set($event)"
        name="password"
        required
        autocomplete="current-password"
      />
    </label>

    <button type="submit" [disabled]="submitting()">
      @if (submitting()) {
        Working…
      } @else {
        {{ mode() === 'login' ? 'Sign in' : 'Register' }}
      }
    </button>
  </form>

  <p class="toggle">
    @if (mode() === 'login') {
      No account?
      <button class="link-btn" (click)="toggleMode()">Register</button>
    } @else {
      Already have one?
      <button class="link-btn" (click)="toggleMode()">Sign in</button>
    }
  </p>
</div>
```

### `src/app/components/create-quote/create-quote.ts`
```typescript
import { Component, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { QuotesService } from '../../services/quotes.service';

@Component({
  selector: 'app-create-quote',
  imports: [FormsModule],
  templateUrl: './create-quote.html',
  styleUrl: './create-quote.css',
})
export class CreateQuoteComponent {
  private readonly quotesService = inject(QuotesService);

  readonly author = signal('');
  readonly text = signal('');
  readonly submitting = signal(false);
  readonly formError = signal<string | null>(null);
  readonly expanded = signal(false);

  toggle(): void {
    this.expanded.update((v) => !v);
    this.formError.set(null);
  }

  async submit(): Promise<void> {
    if (!this.author().trim() || !this.text().trim()) {
      this.formError.set('Author and text are required.');
      return;
    }
    this.formError.set(null);
    this.submitting.set(true);
    try {
      await this.quotesService.createQuote(this.author(), this.text());
      this.author.set('');
      this.text.set('');
      this.expanded.set(false);
    } catch (e: any) {
      const msg =
        e?.error?.errors?.quote?.[0] ??
        e?.error?.title ??
        e?.message ??
        'Failed to create quote';
      this.formError.set(msg);
    } finally {
      this.submitting.set(false);
    }
  }
}
```

### `src/app/components/create-quote/create-quote.html`
```html
<div class="create-quote">
  @if (!expanded()) {
    <button class="btn-add" (click)="toggle()">+ Add quote</button>
  } @else {
    <form class="form" (ngSubmit)="submit()">
      <h3>New quote</h3>

      @if (formError()) {
        <p class="error">{{ formError() }}</p>
      }

      <label>
        Author
        <input
          type="text"
          [ngModel]="author()"
          (ngModelChange)="author.set($event)"
          name="author"
          placeholder="e.g. Marcus Aurelius"
        />
      </label>

      <label>
        Quote text
        <textarea
          [ngModel]="text()"
          (ngModelChange)="text.set($event)"
          name="text"
          rows="3"
          placeholder="Enter the quote…"
        ></textarea>
      </label>

      <div class="form-actions">
        <button type="submit" [disabled]="submitting()">
          {{ submitting() ? 'Saving…' : 'Save' }}
        </button>
        <button type="button" class="btn-cancel" (click)="toggle()">
          Cancel
        </button>
      </div>
    </form>
  }
</div>
```

### `src/app/app.ts`
```typescript
import { Component, inject } from '@angular/core';
import { AuthService } from './services/auth.service';
import { AuthFormComponent } from './components/auth-form/auth-form';
import { QuotesListComponent } from './components/quotes-list/quotes-list';

@Component({
  selector: 'app-root',
  imports: [AuthFormComponent, QuotesListComponent],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  readonly auth = inject(AuthService);
}
```

### `src/app/app.html`
```html
<div class="shell">
  <header class="topbar">
    <span class="brand">Quotes</span>

    @if (auth.isLoggedIn()) {
      <button class="btn-logout" (click)="auth.logout()">Sign out</button>
    } @else {
      <span class="hint">Sign in to add or delete quotes</span>
    }
  </header>

  <main class="content">
    <app-quotes-list />

    @if (!auth.isLoggedIn()) {
      <app-auth-form />
    }
  </main>
</div>
```

---

## (3) Verification Log

### States and edges exercised

| State | How I triggered it | What the UI showed |
|---|---|---|
| **Loading** | Fresh page load, API takes ~50 ms | `Loading quotes…` message visible briefly before quotes appear |
| **Error** | Stopped the API (`Ctrl+C`), reloaded the page | `Error: Http failure response…` + Retry button rendered via `@else if (quotesService.error())` |
| **Retry works** | Clicked Retry after restarting the API | `loadPage()` re-called, quotes loaded correctly |
| **Empty list** | Hit `POST /api/dev/seed` to wipe DB, then called a page beyond the data | `No quotes yet. Be the first to add one!` rendered |
| **Populated list** | Normal load with 100 seeded quotes | 5 cards rendered via `@for (quote of quotesService.quotes(); track quote.id)` |
| **`hasNext` computed true** | Pages 1–19 each return exactly 5 items | "Next →" button enabled; `computed()` evaluated `quotes().length === 5` → `true` |
| **`hasNext` computed false** | Page 20 returns only 3 items (last partial page) | "Next →" button disabled — `computed()` evaluated `3 === 5` → `false`, no extra fetch |
| **`hasPrev` computed** | Navigated to page 3 | "← Prev" enabled; on page 1 it was disabled |
| **`effect()` auto-fetch** | Clicked Next/Prev — `page` signal incremented/decremented | `effect()` fired automatically, new page loaded without any manual `loadPage()` call in the template |
| **Auth-gated: logged out** | No token in localStorage | Delete buttons hidden, "Sign in to add or delete quotes" in header, create form hidden |
| **Auth-gated: logged in** | Registered `test@test.com` / `password123`, logged in | Delete buttons appeared on all cards, `+ Add quote` button appeared |
| **Delete flow @switch** | Clicked Delete on a card | `state()` changed `idle → confirm-delete`, "Are you sure?" rendered; confirmed → `deleting` state → card removed from list |
| **Cancel delete** | Clicked Cancel during confirm-delete | `state()` reset to `idle`, normal card restored |
| **Create quote** | Filled form, submitted | `POST /api/quotes` called with `Authorization: Bearer …`, list refreshed, new quote appeared at top |
| **Validation error on create** | Submitted with empty author | `formError` signal set, error shown inline, no API call made |

---

### Bug 1 — Wrong API name caught at build time

**What the agent wrote (first attempt):**
```typescript
import {
  provideExperimentalZonelessChangeDetection,   // ← WRONG
} from '@angular/core';
```

**Build error:**
```
TS2724: '"@angular/core"' has no exported member named
'provideExperimentalZonelessChangeDetection'.
Did you mean 'provideZonelessChangeDetection'?
```

**Why it happened:** The agent was trained on Angular 17–19 docs where the function was still experimental. Angular 21 promoted it to stable and dropped the `Experimental` prefix. The agent didn't know the API had graduated.

**Fix applied:**
```typescript
import {
  provideZonelessChangeDetection,   // ← correct for Angular 21
} from '@angular/core';
```

This is exactly the stale-training-data failure mode to watch for when Angular versions move fast.

---

### Bug I Caught and Fixed

**Bug:** The loading signal was initialized as signal(false) instead of signal(true).

**Problem:** When the component first loads it immediately fetches quotes from the API.
But since loading started as false, for a brief moment the UI showed
"No quotes found." instead of "Loading quotes…" — wrong initial state.

**What I told the agent:**
"The loading signal should start as true not false because quotes are
being fetched immediately on component init."

**Agent fixed it to:**
```typescript
readonly loading = signal(true);


### What breaks if the API contract changes

| API change | What silently breaks in the Angular app |
|---|---|
| `author` renamed to `authorName` | Every `{{ quote().author }}` renders blank. No compile error — the `Quote` interface is TypeScript only; HTTP responses are cast at runtime with no validation |
| Response wrapped: `{ items: Quote[], total: number }` instead of plain array | `this.quotes.set(result)` sets an object, not an array. `@for` iterates nothing. `hasNext` computed does `object.length === 5` → `undefined === 5` → `false`, Next button permanently disabled |
| `id` field removed | `track quote.id` tracks `undefined` for every item; Angular falls back to index-based tracking, losing per-item identity. `DELETE /api/quotes/undefined` gets a 400 from the API |
| Login response field renamed `accessToken` → `access_token` | `AuthService.setTokens()` stores `undefined`; every authenticated request sends `Authorization: Bearer undefined`; all create/delete calls get 401 silently — no error in the console because the token is technically set |
| Pagination added (API starts returning `{ items, totalCount }`) | Same as wrapping case — `quotes.set()` receives the wrapper object; all list rendering breaks |

The root fix for the last two categories is a typed HTTP interceptor or a Zod/class-transformer validation layer at the service boundary. As built, the contract is implicit and unchecked at runtime.
