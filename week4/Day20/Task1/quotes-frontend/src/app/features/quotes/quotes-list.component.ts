import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { QuotesStateService } from './quotes-state.service';

const TAGS = ['wisdom', 'motivation', 'philosophy', 'success', 'life', 'humor', 'truth', 'change', 'perseverance', 'education'];
const CATS = ['classic', 'modern'];

@Component({
  selector: 'app-quotes-list',
  standalone: true,
  imports: [RouterLink],
  styleUrl: './quotes-list.component.css',
  template: `
    <div class="quotes-page">
      <div class="page-header">
        <h2>Quotes</h2>
        <div class="page-size-ctrl">
          <label for="pageSize">Per page</label>
          <select id="pageSize" (change)="onPageSizeChange(+$any($event.target).value)">
            <option value="10" selected>10</option>
            <option value="25">25</option>
            <option value="50">50</option>
          </select>
        </div>
      </div>

      @if (loading()) {
        <p class="state-msg">Loading…</p>
      } @else if (error()) {
        <p class="state-msg error">{{ error() }}</p>
      } @else {
        <p class="page-subtitle">
          {{ pagedQuotes().length }} quotes · {{ authorCount() }} authors on this page
        </p>

        <div class="goto-row">
          <input #goId type="number" class="goto-input" placeholder="Go to ID…" min="1" />
          <button class="goto-btn" (click)="navigateToId(goId.value)">Go</button>
        </div>

        @if (pagedQuotes().length === 0) {
          <p class="state-msg">No quotes yet.</p>
        } @else {
          <div class="cards">
            @for (quote of pagedQuotes(); track quote.id) {
              <a class="card" [routerLink]="['/quotes', quote.id]">
                <p class="quote-text">"{{ quote.text }}"</p>
                <p class="quote-author">{{ quote.author }}</p>
                <div class="badges">
                  <span class="badge tag-{{ tagFor(quote.id) }}">{{ tagFor(quote.id) }}</span>
                  <span class="badge cat-{{ catFor(quote.id) }}">{{ catFor(quote.id) }}</span>
                </div>
              </a>
            }
          </div>

          @if (totalPages() > 1) {
            <div class="pagination">
              <button class="page-btn" (click)="prevPage()" [disabled]="currentPage() === 1">‹ Prev</button>
              <span class="page-info">Page {{ currentPage() }} of {{ totalPages() }}</span>
              <button class="page-btn" (click)="nextPage()" [disabled]="currentPage() === totalPages()">Next ›</button>
            </div>
          }
        }
      }
    </div>
  `,
})
export class QuotesListComponent {
  private readonly state = inject(QuotesStateService);
  private readonly router = inject(Router);

  readonly loading = this.state.loading;
  readonly error = this.state.error;
  readonly pageSize = signal(10);
  readonly currentPage = signal(1);

  private readonly allQuotes = this.state.quotes;

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.allQuotes().length / this.pageSize()))
  );

  readonly pagedQuotes = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize();
    return this.allQuotes().slice(start, start + this.pageSize());
  });

  readonly authorCount = computed(() =>
    new Set(this.pagedQuotes().map(q => q.author)).size
  );

  tagFor(id: number): string { return TAGS[id % TAGS.length]; }
  catFor(id: number): string { return CATS[id % CATS.length]; }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.currentPage.set(1);
  }

  prevPage(): void {
    if (this.currentPage() > 1) this.currentPage.update(p => p - 1);
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) this.currentPage.update(p => p + 1);
  }

  navigateToId(val: string): void {
    const id = parseInt(val, 10);
    if (!isNaN(id) && id > 0) this.router.navigate(['/quotes', id]);
  }

  constructor() {
    this.state.loadQuotes();
  }
}
