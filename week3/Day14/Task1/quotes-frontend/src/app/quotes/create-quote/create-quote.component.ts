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
        this.router.navigate(['/quotes'], { queryParams: { created: 'true' } });
      },
      error: (err: HttpErrorResponse) => {
        this.submitting.set(false);
        this.serverError.set(err.error?.error ?? 'An unexpected error occurred. Please try again.');
      },
    });
  }
}
