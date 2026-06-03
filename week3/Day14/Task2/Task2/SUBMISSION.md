Task 2 — Signal Forms Preview: Submission

  ---
  Brief

  Build a create-quote form using Angular's
  experimental Signal Forms API
  (@angular/forms/signals) instead of
  ReactiveFormsModule. The form captures two fields
  — author and text — validates them client-side,
  submits to POST http://localhost:5255/api/quotes,
  shows server errors on failure, disables the
  submit button during the request, and redirects
  to /quotes?created=true on success.

  ---
  Agent Output — Signal Forms Version

  The agent produced the following. This is the
  exact generated code, nothing added.

  Model and form wiring

  interface QuoteModel { author: string; text:
  string; }

  private readonly model = signal<QuoteModel>({
  author: '', text: '' });

  readonly quoteForm = form(this.model, (p) => {
    required(p.author);
    minLength(p.author, 2);
    maxLength(p.author, 100);
    required(p.text);
    minLength(p.text, 5);
    maxLength(p.text, 1000);
  });

  readonly author = this.quoteForm.author;
  readonly text   = this.quoteForm.text;

  Submit handler

  onSubmit(event: Event): void {
    event.preventDefault();
    this.serverError.set(null);
    this.author().markAsTouched();
    this.text().markAsTouched();

    if (this.quoteForm().invalid()) {
      this.author().invalid()
        ? this.author().focusBoundControl()
        : this.text().focusBoundControl();
      return;
    }

    const { author, text } = this.model();
    this.submitting.set(true);

    this.quotesService.createQuote(author,
  text).subscribe({
      next: () => {
        this.submitting.set(false);
        this.router.navigate(['/quotes'], {
  queryParams: { created: 'true' } });
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        const message = err.error?.title || 'An
  unexpected error occurred. Please try again.';
        this.serverError.set(message);
      },
    });
  }

  Template (key binding)

  <input id="author" type="text" 
  [formField]="author"
         [attr.aria-invalid]="author().invalid() &&
  author().touched()"
         aria-describedby="author-error" 
  autocomplete="off" />

  @if (author().invalid() && author().touched()) {
    <p id="author-error" role="alert">
      @if (author().errors().some(e => e.kind ===
  'required'))      { Author is required }
      @else if (author().errors().some(e => e.kind
  === 'minLength')){ Author must be at least 2
  characters }
      @else if (author().errors().some(e => e.kind
  === 'maxLength')){ Author cannot exceed 100
  characters }
    </p>
  }

  ---
  Verification Log

  Every claim below has a screenshot as proof.
  Screenshot names match the files in Screenshots/.

  Pristine form load — 01-pristine-form.png
  Form loads at localhost:4200/quotes/create. Both
  fields empty, no red borders, no error messages,
  button enabled and labelled "Create Quote".

  Required errors appear on touch —
  02-required-errors-on-touch.png
  Both fields blurred while empty. Both show red
  border, "Author is required" and "Quote text is
  required" appear below the respective fields.

  Required errors on submit —
  03-required-errors-on-submit.png
  Submit clicked with both fields empty. Same
  required errors visible. Proves
  author().markAsTouched() and
  text().markAsTouched() inside onSubmit are firing
  correctly.

  minLength validation —
  04-minlength-validation.png
  Author field contains "A" (1 character), text
  field contains "AAAA" (4 characters). Both show
  red border. Author shows "Author must be at least
  2 characters", text shows "Quote text must be at
  least 5 characters". Confirms e.kind ===
  'minLength' is the correct kind string.

  Focus moves to first invalid field —
  05-focus-moves-to-first-invalid-field.png
  After submit with author empty, the browser's
  blue focus ring is on the author input. This
  confirms author().focusBoundControl() moved
  keyboard focus programmatically.

  Submitting state —
  06-submitting-button-disabled.png
  Author set to "Marcus Stonny", text set to
  "Always control your 'M'". Button shows
  "Submitting..." and is visually disabled.
  DevTools Network tab is open and shows the POST
  request in flight to localhost:5255/api/quotes.

  Success redirect and banner —
  07-success-redirect-and-banner.png
  After the POST returns 201, the app navigated to
  /quotes. The "Quote created successfully!"
  success banner is visible in green at the top of
  the list. The newly created quote appears in the
  list.

  Bug before fix — 08-bug-before-raw-http-error.png
  With the backend stopped, the form showed the raw
  message "Http failure response for
  http://localhost:5255/api/quotes: 0 Unknown
  Error" in the error banner. This was the broken
  state before the error handler was corrected.

  Bug after fix — network error —
  09-bug-after-network-fallback.png
  Same backend-stopped scenario after the fix. The
  banner now shows "An unexpected error occurred.
  Please try again." — the fallback fires correctly
  when err.error is null.

  err.error?.title path proven —
  11-err-title-fix-proven.png
  Backend running, submitted with an empty author
  field to force a server-side 400. The banner
  shows "One or more validation errors occurred." —
  this is the exact title string from the ASP.NET
  Core response body, proving err.error?.title is
  reading the correct field.

  ---
  Bug Caught and Fixed

  What the agent wrote:

  this.serverError.set(err.error?.error ?? 'An
  unexpected error occurred. Please try again.');

  Why it was wrong:

  The agent assumed the backend returns { error:
  "message" }. The actual ASP.NET Core response
  shape for a 400 is:

  {
    "title": "One or more validation errors
  occurred.",
    "status": 400,
    "errors": {
      "Author": ["The Author field is required."],
      "Text": ["The Text field is required."]
    }
  }

  err.error?.error is always undefined on this
  shape. The ?? fallback fired every time, so every
  server error — whether a validation failure or a
  real unexpected error — showed the identical
  generic string. The user had no way to know the
  actual reason.

  The fix:

  const message = err.error?.title || 'An
  unexpected error occurred. Please try again.';
  this.serverError.set(message);

  Proof the fix works: 11-err-title-fix-proven.png
  shows the UI displaying "One or more validation
  errors occurred." which is the backend's actual
  title value.

  ---
  Unexpected Finding — maxLength is Dead Code

  While testing, pasting 120 characters into the
  author field did not show "Author cannot exceed
  100 characters". Instead the value was silently
  truncated to 100 characters and submitted
  successfully.

  The cause is that the [formField] directive from
  @angular/forms/signals reads the declared
  validators and sets native HTML attributes on the
  bound element automatically. Because
  maxLength(p.author, 100) is declared, the
  directive adds maxlength="100" to the <input> DOM
  element. The browser enforces this natively and
  prevents the value from ever exceeding 100
  characters.

  As a result, the Signal Forms maxLength validator
  never fires an error, and this branch in the
  template is unreachable:

  @else if (author().errors().some(e => e.kind ===
  'maxLength')) {
    Author cannot exceed 100 characters    ← can
  never be reached
  }

  In Reactive Forms you control the HTML attributes
  manually. If you add only
  Validators.maxLength(100) without a
  maxlength="100" attribute, the user can paste
  over-length text and see the error message.
  Signal Forms collapses both concerns into one,
  making the validation message path unreachable.
  This is a concrete @experimental behaviour that
  is not obvious from the documentation.

  ---
  What Breaks if the Week-1 API Contract Changes

  The endpoint is POST
  http://localhost:5255/api/quotes with body {
  author, text } as declared in
  quotes.service.ts:26–28.

  If a field is renamed — say author becomes
  authorName — the POST body still sends { author,
  text }. The backend ignores the unknown field
  silently. No compile error because QuoteModel
  still has author. The quote gets created with an
  empty author on the server side with no
  client-side warning.

  If a new required field is added — say category —
  client-side validation passes since the form has
  no category field. The POST reaches the server,
  fails with a 400, and the user sees "One or more
  validation errors occurred." with no field-level
  guidance about which field is missing.

  If the endpoint URL changes —
  quotes.service.ts:16 has
  http://localhost:5255/api/quotes hardcoded. Every
  environment other than your local machine breaks
  immediately.

  If the error response shape changes — say title
  is renamed to message — err.error?.title becomes
  undefined again and the generic fallback fires
  every time, same category of bug as the original.

  If the success response shape changes — the Quote
  interface in quotes.service.ts goes stale, but
  there is no runtime crash because the response
  value is not read after the POST. Navigation
  happens regardless.

  ---
  Signal Forms vs Reactive Forms

  Wiring — Reactive Forms requires FormBuilder,
  FormGroup, and a FormControl per field wired
  together. Signal Forms replaces all of that with
  a single WritableSignal<QuoteModel> passed to
  form().

  Validators — Reactive Forms attaches
  ValidatorFn[] arrays to each control. Signal
  Forms uses a typed schema callback where you
  write required(p.author) and minLength(p.author,
  2) against typed field paths, so a rename in the
  interface is caught by TypeScript.

  Template reads — Reactive Forms uses
  .get('author'), .hasError('required'), and
  AbstractControl methods. Signal Forms fields are
  plain signals — author().invalid(),
  author().errors().some(e => e.kind ===
  'required') — no special API surface needed.

  Touch all on submit — Reactive Forms has a single
  form.markAllAsTouched() call. Signal Forms has
  no equivalent on the root FieldTree, so you must
  call author().markAsTouched() and
  text().markAsTouched() individually. On a form
  with ten fields this becomes ten separate calls.

  Focus on error — Reactive Forms requires a manual
  document.getElementById('author')?.focus().
  Signal Forms provides
  author().focusBoundControl() as a built-in,
  proven working in
  05-focus-moves-to-first-invalid-field.png.

  Reading the final value — Reactive Forms reads
  this.form.value with type casting. Signal Forms
  reads this.model() — the same typed signal used
  to initialise the form.

  Stability — Reactive Forms is stable and fully
  documented. Signal Forms is @experimental on
  Angular 21.2. The API surface, including the kind
  strings on ValidationError and the behaviour of
  maxLength adding a native HTML attribute, can
  change in any minor version without a deprecation
  cycle.