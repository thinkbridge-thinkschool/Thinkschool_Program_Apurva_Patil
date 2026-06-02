# Day 14 — Task 1: Reactive Forms + Accessibility

## Brief Given to Claude Code

I directed Claude Code to build a reactive create-a-quote form
against my real ASP.NET Core API running on http://localhost:5255.

Real endpoint:
POST /api/quotes
Body: { "author": "string", "text": "string" }
Both fields are required — backend returns 400 if either is
empty or whitespace.
Success response: 201 Created with the created Quote object.

Instructions given to agent:
- Add createQuote(author, text) method to QuotesService
- Build a FormGroup with exactly two fields matching the API:
    author: required, minLength 2, maxLength 100
    text: required, minLength 5, maxLength 1000
- signals: submitting and serverError
- onSubmit() marks all fields touched, focuses first invalid
  field on failure, calls service, navigates to /quotes on success
- Full aria wiring on every field:
    label for/id association
    aria-invalid when field is invalid and touched
    aria-describedby linking input to its error message element
    error message p elements with matching id
- inject() only, no constructor injection
- No any anywhere

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

  createQuote(author: string, text: string): Observable<Quote> {
    return this.http.post<Quote>(this.apiUrl, { author, text });
  }
}

### create-quote.component.ts — AS AGENT GENERATED (before bug fix)
import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { QuotesService } from '../../quotes.service';

@Component({
  selector: 'app-create-quote',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './create-quote.component.html',
  styleUrl: './create-quote.component.css',
})
export class CreateQuoteComponent {
  private readonly fb = inject(FormBuilder);
  private readonly quotesService = inject(QuotesService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly form: FormGroup = this.fb.group({
    author: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    text: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(1000)]],
  });

  onSubmit(): void {
    this.form.markAllAsTouched();
    this.serverError.set(null);

    if (this.form.invalid) {
      const firstInvalidKey = Object.keys(this.form.controls).find(
        key => this.form.controls[key].invalid
      );
      if (firstInvalidKey) {
        const el = document.getElementById(firstInvalidKey);
        el?.focus();
      }
      return;
    }

    const { author, text } = this.form.value as { author: string; text: string };
    this.submitting.set(true);

    this.quotesService.createQuote(author, text).subscribe({
      next: () => {
        this.submitting.set(false);
        this.router.navigate(['/quotes']);
      },
      error: (err: { message?: string }) => {
        this.submitting.set(false);
        this.serverError.set(
          err.message ?? 'An unexpected error occurred. Please try again.'
        );
      },
    });
  }
}

### create-quote.component.ts — AFTER BUG FIX
import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { QuotesService } from '../../quotes.service';

@Component({
  selector: 'app-create-quote',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './create-quote.component.html',
  styleUrl: './create-quote.component.css',
})
export class CreateQuoteComponent {
  private readonly fb = inject(FormBuilder);
  private readonly quotesService = inject(QuotesService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly form: FormGroup = this.fb.group({
    author: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    text: ['', [Validators.required, Validators.minLength(5), Validators.maxLength(1000)]],
  });

  onSubmit(): void {
    this.form.markAllAsTouched();
    this.serverError.set(null);

    if (this.form.invalid) {
      const firstInvalidKey = Object.keys(this.form.controls).find(
        key => this.form.controls[key].invalid
      );
      if (firstInvalidKey) {
        const el = document.getElementById(firstInvalidKey);
        el?.focus();
      }
      return;
    }

    const { author, text } = this.form.value as { author: string; text: string };
    this.submitting.set(true);

    this.quotesService.createQuote(author, text).subscribe({
      next: () => {
        this.submitting.set(false);
        this.router.navigate(['/quotes'], {
          queryParams: { created: 'true' }
        });
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.serverError.set(
          err.error?.error ?? 'An unexpected error occurred. Please try again.'
        );
      },
    });
  }
}

### create-quote.component.html
<section class="create-quote-container">
  <h1>Create Quote</h1>

  @if (serverError()) {
    <div class="server-error" role="alert">{{ serverError() }}</div>
  }

  <form [formGroup]="form" (ngSubmit)="onSubmit()" novalidate>
    <div class="field">
      <label for="author">Author</label>
      <input
        id="author"
        type="text"
        formControlName="author"
        [attr.aria-invalid]="form.controls['author'].invalid && form.controls['author'].touched"
        aria-describedby="author-error"
        autocomplete="off"
      />
      @if (form.controls['author'].invalid && form.controls['author'].touched) {
        <p id="author-error" class="field-error" role="alert">
          @if (form.controls['author'].hasError('required')) {
            Author is required
          } @else if (form.controls['author'].hasError('minlength')) {
            Author must be at least 2 characters
          } @else if (form.controls['author'].hasError('maxlength')) {
            Author cannot exceed 100 characters
          }
        </p>
      } @else {
        <p id="author-error" class="field-error" aria-hidden="true"></p>
      }
    </div>

    <div class="field">
      <label for="text">Quote Text</label>
      <textarea
        id="text"
        formControlName="text"
        rows="5"
        [attr.aria-invalid]="form.controls['text'].invalid && form.controls['text'].touched"
        aria-describedby="text-error"
      ></textarea>
      @if (form.controls['text'].invalid && form.controls['text'].touched) {
        <p id="text-error" class="field-error" role="alert">
          @if (form.controls['text'].hasError('required')) {
            Quote text is required
          } @else if (form.controls['text'].hasError('minlength')) {
            Quote text must be at least 5 characters
          } @else if (form.controls['text'].hasError('maxlength')) {
            Quote text cannot exceed 1000 characters
          }
        </p>
      } @else {
        <p id="text-error" class="field-error" aria-hidden="true"></p>
      }
    </div>

    <button type="submit" [disabled]="submitting()">
      {{ submitting() ? 'Submitting...' : 'Create Quote' }}
    </button>
  </form>
</section>

### create-quote.component.css
.create-quote-container {
  max-width: 600px;
  margin: 2rem auto;
  padding: 0 1rem;
}

.field {
  display: flex;
  flex-direction: column;
  margin-bottom: 1.25rem;
}

label {
  font-weight: 600;
  margin-bottom: 0.375rem;
}

input,
textarea {
  padding: 0.5rem 0.75rem;
  font-size: 1rem;
  border: 2px solid #ccc;
  border-radius: 4px;
  outline: none;
  transition: border-color 0.15s, box-shadow 0.15s;
}

input:focus,
textarea:focus {
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.35);
}

input[aria-invalid="true"],
textarea[aria-invalid="true"] {
  border-color: #dc2626;
}

input[aria-invalid="true"]:focus,
textarea[aria-invalid="true"]:focus {
  box-shadow: 0 0 0 3px rgba(220, 38, 38, 0.35);
}

.field-error {
  min-height: 1.25rem;
  margin: 0.25rem 0 0;
  font-size: 0.875rem;
  color: #dc2626;
}

.server-error {
  padding: 0.75rem 1rem;
  margin-bottom: 1rem;
  background: #fef2f2;
  border: 1px solid #fca5a5;
  border-radius: 4px;
  color: #b91c1c;
  font-size: 0.9rem;
}

button[type="submit"] {
  padding: 0.6rem 1.5rem;
  font-size: 1rem;
  font-weight: 600;
  color: #fff;
  background: #2563eb;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  transition: background 0.15s, box-shadow 0.15s;
}

button[type="submit"]:hover:not(:disabled) {
  background: #1d4ed8;
}

button[type="submit"]:focus-visible {
  outline: none;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.5);
}

button[type="submit"]:disabled {
  background: #93c5fd;
  cursor: not-allowed;
}

### quotes.routes.ts
import { Routes } from '@angular/router';
import { QuotesComponent } from './quotes.component';
import { QuoteDetailComponent } from './quote-detail/quote-detail.component';
import { CreateQuoteComponent } from './create-quote/create-quote.component';

export const quotesRoutes: Routes = [
  { path: '', redirectTo: 'quotes', pathMatch: 'full' },
  { path: 'quotes', component: QuotesComponent },
  { path: 'quotes/create', component: CreateQuoteComponent },
  { path: 'quotes/:id', component: QuoteDetailComponent },
];

---

## Verification Log

### States Exercised

Empty
- How tested: Opened form fresh at localhost:4200/quotes/create
- Proof: Screenshots/empty-state.png

Invalid
- How tested: Clicked Submit without filling any fields
- Both fields showed red border and error messages immediately
- Proof: Screenshots/invalid-state.png

Submitting
- How tested: DevTools Network set to Slow 3G, filled form,
  clicked Submit — button changed to Submitting... and was
  disabled during the request
- Proof: Screenshots/submitting-state.png

Server error
- How tested: Stopped backend, filled form, clicked Submit
- Proof: Screenshots/bug-before-fix.png and bug-after-fix.png

Success
- How tested: Started backend, filled valid values, submitted
- Redirected to /quotes showing green "Quote created
  successfully!" banner for 3 seconds
- Proof: Screenshots/success-message.png

Keyboard
- How tested: Tabbed through Author field, Quote Text field,
  Submit button in order without using mouse. Submitted empty
  form — focus moved automatically to Author field.
  Blue focus ring visible on active field at all times.
- Proof: Screenshots/keyboard-focus.png

Accessibility
- How tested: Inspected HTML manually — every input has label
  with matching for/id, aria-invalid set when field is invalid
  and touched, aria-describedby on every input pointing to its
  error p element with matching id, role="alert" on error
  messages so screen reader announces them automatically.
- Proof: Visible in create-quote.component.html agent output above

---

## Bug Caught — Wrong Error Type for HttpErrorResponse

BEFORE (agent output):
error: (err: { message?: string }) => {
  this.submitting.set(false);
  this.serverError.set(
    err.message ?? 'An unexpected error occurred. Please try again.'
  );
},

Problem: Angular HttpClient errors are HttpErrorResponse objects.
The err.message property does not contain the API error message.
It contains the raw Angular HTTP failure string like:
"Http failure response for http://localhost:5255/api/quotes: 0 Unknown Error"
This is a technical internal message that means nothing to the user.
Proof of bug visible: Screenshots/bug-before-fix.png

AFTER (fix applied):
import { HttpErrorResponse } from '@angular/common/http';

error: (err: HttpErrorResponse) => {
  this.submitting.set(false);
  this.serverError.set(
    err.error?.error ?? 'An unexpected error occurred. Please try again.'
  );
},

Fix reads err.error?.error which is the actual error field from
the API response body. Falls back to a clean user-friendly message
if the API body is empty.
Proof of fix visible: Screenshots/bug-after-fix.png
Fix visible in agent output above under AFTER BUG FIX section.

---

## What Breaks if API Contract Changes

author field renamed to name
- FormGroup still sends { author, text } to the API
- API ignores the field, returns 400 on every submit
- TypeScript does not catch this because field names in
  fb.group are plain strings, not typed properties
- Visible in create-quote.component.ts in the fb.group call

text field renamed to content
- Same problem as above — form sends wrong key silently
- API returns 400, server error signal shows the message

New required field added e.g. category
- Form has no category field
- API returns 400 on every submit with no frontend warning
- Developer only finds out at runtime, not compile time

minLength or maxLength tightened on backend
- Frontend validators would pass but backend rejects
- Server error signal catches it and shows the API message
  to the user gracefully

POST /api/quotes URL changes
- Every submit hits 404
- Server error signal handles it and shows fallback message

---

## What I Learned

I learned that reading the diff carefully matters more than
trusting the agent summary. The agent correctly described its
error handling in plain English but the actual type it used
was wrong. err.message on an HttpErrorResponse is not the
API message — it is the Angular internal HTTP failure string.
I caught this by reading the error handler type and knowing
what HttpErrorResponse actually contains, not by just reading
the agent's description of what it built.

I also noticed the form silently redirected with no user
feedback on success so I added a success message that shows
"Quote created successfully!" for 3 seconds after redirect.
Visible in Screenshots/success-message.png.

## What Would Break This

If the API renames the author or text fields the form breaks
silently because FormGroup keys are plain strings — visible in
create-quote.component.ts in the fb.group call where author
and text are hardcoded string keys with no compile-time check.
If a new required field is added to the API the form will
always return 400 on submit with no frontend warning — the
only signal is the serverError showing the API message to
the user.

---

## Screenshots
- empty-state.png — form opened fresh, nothing filled
- invalid-state.png — both fields showing red border and errors
- submitting-state.png — button showing Submitting... on slow network
- bug-before-fix.png — ugly raw HttpErrorResponse message shown
- bug-after-fix.png — clean user-friendly fallback message shown
- keyboard-focus.png — blue focus ring visible on author field
- success-message.png — green banner after successful submit