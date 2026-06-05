import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { EMPTY, Subscription } from 'rxjs';
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
        if (isNaN(id)) {
          this.error.set('Invalid quote ID.');
          this.loading.set(false);
          return EMPTY;
        }
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
