## 1. My Prompt to the Agent

I have an Angular 21 + ASP.NET Core quotes app. The backend runs on http://localhost:5255.
Do three things.

First, add client-side routing. Create src/app/app.routes.ts with exactly these routes:
- '' redirects (pathMatch: 'full') to 'quotes'
- 'quotes' is lazy-loaded via loadComponent and protected by a functional authGuard
- 'quotes/:id' is a separate lazy-loaded route also protected by the same authGuard — this is the detail route; it must be its own chunk so the detail bundle is not downloaded until a user navigates there
- 'create' is lazy-loaded and guarded
- 'login' is lazy-loaded and public (no guard)

Second, create src/app/core/auth.guard.ts as a CanActivateFn. It must inject AuthService and Router. If AuthService.isLoggedIn() returns true, return true. Otherwise return router.createUrlTree(['/login']). No class-based guard; functional only.

Third, create src/app/features/quotes/quote-detail.component.ts. It reads the :id param from ActivatedRoute.paramMap, converts it with Number(), and if isNaN rejects it with an 'Invalid quote ID.' error signal without making any HTTP call. For valid ids it calls GET http://localhost:5255/api/quotes/${id} with an Authorization header from AuthService.getToken(). Handle three response states: loading (initial), notFound (404 status), and a generic error for anything else. Display id, text, author, createdAt (formatted with DatePipe), and computed tag/category badges derived from id modulo the badge arrays. Use Angular signals (signal, OnInit, OnDestroy) — no Subject, no async pipe.

---

## 2. Agent Output

### src/app/app.routes.ts

```typescript
import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const appRoutes: Routes = [
  { path: '', redirectTo: 'quotes', pathMatch: 'full' },

  {
    path: 'quotes',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/quotes/quotes-list.component').then(
        (m) => m.QuotesListComponent,
      ),
  },

  {
    path: 'quotes/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/quotes/quote-detail.component').then(
        (m) => m.QuoteDetailComponent,
      ),
  },

  {
    path: 'create',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./quotes/create-quote/create-quote.component').then(
        (m) => m.CreateQuoteComponent,
      ),
  },

  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
];
```

### src/app/core/auth.guard.ts

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLoggedIn() ? true : router.createUrlTree(['/login']);
};
```

### src/app/features/quotes/quote-detail.component.ts (key logic)

```typescript
interface QuoteEntity {
  id: number;
  author: string;
  text: string;
  createdAt: string;
}

ngOnInit(): void {
  this.sub = this.route.paramMap
    .pipe(
      tap(() => {
        this.loading.set(true);
        this.notFound.set(false);
        this.error.set(null);
        this.quote.set(null);
      }),
      switchMap((params) => {
        const id = Number(params.get('id'));
        if (isNaN(id)) {
          this.error.set('Invalid quote ID.');
          this.loading.set(false);
          return EMPTY;
        }
        return this.http.get<QuoteEntity>(
          `http://localhost:5255/api/quotes/${id}`,
          { headers: { Authorization: `Bearer ${this.auth.getToken()}` } },
        );
      }),
    )
    .subscribe({
      next: (q) => { this.quote.set(q); this.loading.set(false); },
      error: (err: HttpErrorResponse) => {
        if (err.status === 404) { this.notFound.set(true); }
        else { this.error.set('Failed to load quote.'); }
        this.loading.set(false);
      },
    });
}
```

---

## 3. Verification Log

### Spec files written for this task

**src/app/core/auth.guard.spec.ts**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from './auth.service';
import { vi } from 'vitest';

describe('authGuard', () => {
  let authService: AuthService;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([])],
    });
    authService = TestBed.inject(AuthService);
    router = TestBed.inject(Router);
  });

  afterEach(() => vi.restoreAllMocks());

  it('returns true when user is logged in', () => {
    vi.spyOn(authService, 'isLoggedIn').mockReturnValue(true);
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as never, {} as never),
    );
    expect(result).toBe(true);
  });

  it('returns a UrlTree pointing to /login when user is not logged in', () => {
    vi.spyOn(authService, 'isLoggedIn').mockReturnValue(false);
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as never, {} as never),
    );
    expect(result).toEqual(router.createUrlTree(['/login']));
  });
});
```

**src/app/features/quotes/quote-detail.component.spec.ts**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { QuoteDetailComponent } from './quote-detail.component';

function makeRoute(id: string) {
  return { paramMap: of(convertToParamMap({ id })) };
}

describe('QuoteDetailComponent', () => {
  let controller: HttpTestingController;

  function setup(id: string) {
    TestBed.configureTestingModule({
      imports: [QuoteDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: makeRoute(id) },
      ],
    });
    controller = TestBed.inject(HttpTestingController);
    return TestBed.createComponent(QuoteDetailComponent);
  }

  afterEach(() => controller.verify());

  it('displays quote fields when GET /api/quotes/:id returns 200', () => {
    const fixture = setup('5');
    fixture.detectChanges();

    controller.expectOne('http://localhost:5255/api/quotes/5').flush({
      id: 5,
      author: 'Marcus Aurelius',
      text: 'You have power over your mind.',
      createdAt: '2026-01-01T00:00:00Z',
    });

    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.loading()).toBe(false);
    expect(comp.notFound()).toBe(false);
    expect(comp.error()).toBeNull();
    expect(comp.quote()?.id).toBe(5);
    expect(comp.quote()?.author).toBe('Marcus Aurelius');
  });

  it('sets notFound when GET /api/quotes/:id returns 404', () => {
    const fixture = setup('999');
    fixture.detectChanges();

    controller.expectOne('http://localhost:5255/api/quotes/999').flush(
      { error: 'Quote not found' },
      { status: 404, statusText: 'Not Found' },
    );

    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.notFound()).toBe(true);
    expect(comp.loading()).toBe(false);
    expect(comp.quote()).toBeNull();
  });

  it('sets error and makes no HTTP request when id param is non-numeric', () => {
    const fixture = setup('abc');
    fixture.detectChanges();

    const comp = fixture.componentInstance;
    expect(comp.error()).toBe('Invalid quote ID.');
    expect(comp.loading()).toBe(false);
    expect(comp.quote()).toBeNull();
  });
});
```

### Bug fix: 2 failing interceptor tests

`src/app/interceptors/auth.interceptor.spec.ts` had 2 failing tests before this task was
submitted. Both asserted `'Bearer quotes-app-token'` on the Authorization header, but the
`beforeEach` never seeded `localStorage`. The interceptor reads `localStorage.getItem('accessToken')`
and returns early with no header when the value is `null` — so both assertions got `null` and failed.

**Root cause:** The test was written for an older interceptor that had a hardcoded token. When the
interceptor was updated to read from `localStorage`, the spec was never updated to match.

**Fix applied to `auth.interceptor.spec.ts`:**

```typescript
beforeEach(() => {
  localStorage.setItem('accessToken', 'quotes-app-token');  // ← added
  TestBed.configureTestingModule({ ... });
  ...
});

afterEach(() => {
  controller.verify();
  localStorage.removeItem('accessToken');  // ← added
});
```

### Test run — all 16 passing after fix

Screenshot: `Screenshots/09-test-passed-re-runned-terminal.png`

```
Test Files  7 passed (7)
     Tests  16 passed (16)
  Duration  5.06s
```

---

### Edge: guard redirect (unauthenticated → /login)

Screenshot: `Screenshots/01_guard-redirect.png`

Navigated to `localhost:4200/quotes` with no `accessToken` in localStorage (DevTools Application
tab shows Local Storage for localhost:4200 is empty). The router executed `authGuard`, which called
`AuthService.isLoggedIn()` → `!!localStorage.getItem('accessToken')` → `false`, and returned
`router.createUrlTree(['/login'])`. Angular redirected to `localhost:4200/login`.

**Test coverage:** `auth.guard.spec.ts` — "returns a UrlTree pointing to /login when user is not
logged in" asserts `result` equals `router.createUrlTree(['/login'])` using `TestBed.runInInjectionContext`.

---

### Edge: guard pass (authenticated → route loads)

Screenshot: `Screenshots/04-quotes-list.png`

After logging in, `accessToken` is set in localStorage. Navigating to `/quotes` runs the guard,
`isLoggedIn()` returns `true`, and the route component is rendered at `localhost:4200/quotes`.

**Test coverage:** `auth.guard.spec.ts` — "returns true when user is logged in" asserts `result === true`.

---

### Edge: lazy chunk loading — list route

Screenshot: `Screenshots/02-lazy-loading-list-quotes.png`

Network tab (DevTools) at `localhost:4200/quotes` shows a separate script chunk whose name
contains `quotes-list.component` downloaded only after authentication. Before login, navigating
to `/quotes` triggers the guard redirect without downloading the chunk at all — the list bundle
is deferred behind the guard.

---

### Edge: lazy chunk loading — detail route

Screenshot: `Screenshots/03-lazy-loading-details-quotes.png`

Network tab at `localhost:4200/quotes/9` shows a second distinct chunk containing
`quote-detail.component` loaded only upon navigating to a detail URL. This chunk did not appear
in the previous network capture at the list route — the detail bundle is separate from the list
bundle, confirming the two `loadComponent` calls produce independent lazy chunks.

---

### Edge: valid id — detail page renders

Screenshot: `Screenshots/05-quotes-details.png`

`localhost:4200/quotes/10` shows: QUOTE ID 10, quote text, BY AUTHOR Marcus Stonny,
CREATED ON Jun 2, 2026, TAG wisdom, CATEGORY classic. The component called
`GET http://localhost:5255/api/quotes/10`, received the 4-field JSON `{ id, author, text, createdAt }`,
and rendered without error.

**Test coverage:** `quote-detail.component.spec.ts` — "displays quote fields when GET /api/quotes/:id
returns 200" flushes a 4-field object and asserts `quote()?.id === 5` and loading/error signals are clear.

---

### Edge: missing/non-existent id (404 from API)

Screenshot: `Screenshots/06-invalid-param-404.png`

`localhost:4200/quotes/999` — the component converted the param to `Number(999)`, passed the
`isNaN` check, and called `GET http://localhost:5255/api/quotes/999`. The backend returned 404
(`Results.NotFound`). The error callback checked `err.status === 404`, set `notFound.set(true)`,
and the template rendered "Quote not found." Network tab confirms the 404 response.

**Test coverage:** `quote-detail.component.spec.ts` — "sets notFound when GET /api/quotes/:id returns
404" flushes a 404 and asserts `notFound() === true`, `quote() === null`.

---

### Edge: invalid (non-numeric) route param

Screenshot: `Screenshots/07_invalid-non-numeric-param.png`

`localhost:4200/quotes/abc` — `Number('abc')` is `NaN`, so `isNaN(NaN)` is `true`. The component
sets `error.set('Invalid quote ID.')` and returns `EMPTY` — no HTTP request is made. The template
rendered the "Invalid quote ID." error message.

**Test coverage:** `quote-detail.component.spec.ts` — "sets error and makes no HTTP request when id
param is non-numeric" sets up the route with id `'abc'`, calls `detectChanges()`, and asserts
`error() === 'Invalid quote ID.'` with `loading() === false`. `controller.verify()` in `afterEach`
confirms no HTTP request was queued.

---

## 4. Concrete Bug the Agent Made

**Wrong assumption:** The agent's first draft of `QuoteDetailComponent` declared `isDeleted: boolean`
as a fifth field in the local `QuoteEntity` interface, copying the shape from `quotes.service.ts`
which declares:

```typescript
export interface Quote {
  id: number; author: string; text: string;
  isDeleted: boolean;   // ← agent assumed this existed in the detail response too
  createdAt: string;
}
```

**Why this is wrong — the real endpoint:**

`GET /api/quotes/{id}` in `EndpointExtensions.cs` (line 72–84) calls:

```csharp
var quote = await repository.GetByIdAsync(id, cancellationToken);
return quote is null ? Results.NotFound(...) : Results.Ok(quote);
```

`GetByIdAsync` returns the EF-mapped `Quote` C# model (`QuotesApi/Models/Quote.cs`):

```csharp
public class Quote
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

The model has **four fields only**. There is no `IsDeleted` property. `Results.Ok(quote)` serializes
exactly those four fields as `{ id, text, author, createdAt }`. At runtime `isDeleted` would be
`undefined` on every detail response.

**The fix:** Remove `isDeleted` from `QuoteDetailComponent`'s local `QuoteEntity` interface so it
matches the real `GET /api/quotes/${id}` response shape. The fixed interface has exactly the four
fields the backend actually sends.

**Proof the fix is correct:** `quote-detail.component.spec.ts` flushes
`{ id: 5, author: '...', text: '...', createdAt: '...' }` (four fields, no `isDeleted`) and
asserts `quote()?.id === 5`. If `isDeleted` were still in the interface and marked required,
TypeScript would reject the flush object at compile time.

---

## 5. What Breaks If the API's Detail Route or id Field Changes

**Backend renames route from `GET /api/quotes/{id}` to `GET /api/quote/{id}`**
Every detail fetch returns 404 and `notFound.set(true)` fires for every valid quote. The browser
shows "Quote not found." for IDs that actually exist.

**Backend renames `id` field to `quoteId` in JSON**
`quote()!.id` becomes `undefined`. The QUOTE ID row renders blank and `tagFor(undefined)` returns
`TAGS[NaN % 10]` which is `undefined`, breaking the badges.

**Backend changes `id` from integer to string (e.g. `"id": "10"`)**
`Number(params.get('id'))` still works for the URL param, but `quote()!.id` is a string.
`tagFor(id)` calls `string % number` which is `NaN`, leaving badges undefined. TypeScript strict
mode would flag `typeof quote.id !== 'number'`.

**Backend changes `createdAt` to a Unix timestamp number**
`DatePipe` receives a number instead of an ISO string. Angular's `DatePipe` handles numbers
(milliseconds), so the display still works, but a `typeof createdAt === 'string'` assertion in
`api-contract.spec.ts` would fail and catch the type change.

**`quote-detail.component.spec.ts` fixture URL is not updated after a route rename**
`controller.expectOne('http://localhost:5255/api/quotes/5')` throws "Expected one matching
request... found none", making tests fail and signalling the route string drifted.
