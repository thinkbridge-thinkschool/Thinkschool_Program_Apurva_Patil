/*
 * Signal Forms vs Reactive Forms — trade-offs in this component
 *
 * SIMPLER with Signal Forms:
 *  - No FormBuilder, FormGroup, or FormControl wiring. The model is a plain
 *    WritableSignal; form() wraps it and derives the FieldTree structure automatically.
 *  - Validators live in a typed schema function (required/minLength/maxLength)
 *    attached to field paths, not in ValidatorFn arrays passed to each FormControl.
 *  - Field state (value, invalid, touched, errors) are first-class Signals, so the
 *    template reads them reactively with no AbstractControl helper methods or .get().
 *  - Reading the submitted value is symmetric: the same model signal used to
 *    initialise the form is read to obtain final values — no .value casting needed.
 *  - focusBoundControl() on FieldState replaces the manual document.getElementById
 *    + focus() dance needed in Reactive Forms.
 *
 * STILL ROUGH / LIMITED (Angular 21.2, @experimental):
 *  - No markAllAsTouched() on the root FieldTree — each leaf field must be touched
 *    individually (author().markAsTouched(), text().markAsTouched()).
 *  - Errors are ValidationError objects with a 'kind' string, not the familiar Reactive
 *    Forms error map ({ required: true, minlength: {…} }); template error checks use
 *    .some(e => e.kind === 'required') rather than hasError('required').
 *  - No built-in RxJS bridge: Observable-returning services require manual .subscribe()
 *    or firstValueFrom(); there is no built-in action helper for Observable-based code.
 *  - Signal Forms lives at a separate entry point (@angular/forms/signals), not the
 *    standard @angular/forms barrel — an extra import path to keep in mind.
 *  - The API is @experimental and the surface area may change between minor versions.
 */

import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { QuotesService } from '../../quotes.service';
import { form, required, minLength, maxLength, FormField } from '@angular/forms/signals';

interface QuoteModel {
  author: string;
  text: string;
}

@Component({
  selector: 'app-create-quote',
  standalone: true,
  imports: [FormField],
  templateUrl: './create-quote.component.html',
  styleUrl: './create-quote.component.css',
})
export class CreateQuoteComponent {
  private readonly quotesService = inject(QuotesService);
  private readonly router = inject(Router);

  readonly submitting = signal(false);
  readonly serverError = signal<string | null>(null);

  readonly tags = ['wisdom', 'motivation', 'humor', 'philosophy', 'perseverance', 'success', 'life', 'education', 'truth', 'change'];
  readonly categories = ['classic', 'modern'];

  private readonly model = signal<QuoteModel>({ author: '', text: '' });

  readonly quoteForm = form(this.model, (p) => {
    required(p.author);
    minLength(p.author, 2);
    maxLength(p.author, 100);
    required(p.text);
    minLength(p.text, 100);
    maxLength(p.text, 1000);
  });

  readonly author = this.quoteForm.author;
  readonly text = this.quoteForm.text;

  onSubmit(event: Event): void {
    event.preventDefault();
    this.serverError.set(null);
    this.author().markAsTouched();
    this.text().markAsTouched();

    if (this.quoteForm().invalid()) {
      if (this.author().invalid()) {
        this.author().focusBoundControl();
      } else {
        this.text().focusBoundControl();
      }
      return;
    }

    const { author, text } = this.model();
    this.submitting.set(true);

    this.quotesService.createQuote(author, text).subscribe({
      next: () => {
        this.submitting.set(false);
        this.router.navigate(['/quotes'], { queryParams: { created: 'true' } });
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        const message = err.error?.title || 'An unexpected error occurred. Please try again.';
        this.serverError.set(message);
      },
    });
  }
}
