import { Component, computed, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
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
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly authorFilter = signal('');
  readonly error = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
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

    if (this.route.snapshot.queryParamMap.get('created') === 'true') {
      this.successMessage.set('Quote created successfully!');
      this.router.navigate([], { queryParams: {}, replaceUrl: true });
      setTimeout(() => this.successMessage.set(null), 3000);
    }
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
