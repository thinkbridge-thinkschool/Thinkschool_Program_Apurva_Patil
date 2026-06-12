import { Injectable, inject, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { QuotesService, Quote } from '../../quotes.service';

@Injectable({ providedIn: 'root' })
export class QuotesStateService {
  private readonly quotesService = inject(QuotesService);

  readonly quotes = signal<Quote[]>([]);
  readonly loading = signal(false);
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

  setError(msg: string): void {
    this.error.set(msg);
    this.loading.set(false);
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
}
