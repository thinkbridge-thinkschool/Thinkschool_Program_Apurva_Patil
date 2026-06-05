import {
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY } from 'rxjs';
import { switchMap } from 'rxjs/operators';
import { QuotesStateService } from './quotes-state.service';

const TAGS = ['wisdom', 'motivation', 'philosophy', 'success', 'life', 'humor', 'truth', 'change', 'perseverance', 'education'];
const CATS = ['classic', 'modern'];

@Component({
  selector: 'app-quote-detail',
  standalone: true,
  imports: [DatePipe, RouterLink],
  styleUrl: './quote-detail.component.css',
  template: `
    <div class="detail-page">
      <a class="back-link" [routerLink]="['/quotes']">← Back to list</a>

      @if (loading()) {
        <p class="state-msg">Loading…</p>
      } @else if (notFound()) {
        <p class="state-msg">Quote not found.</p>
      } @else if (error()) {
        <p class="state-msg error">{{ error() }}</p>
      } @else if (quote()) {
        <h2>Details</h2>
        <div class="detail-card">
          <table class="detail-table">
            <tbody>
              <tr>
                <th>QUOTE ID</th>
                <td>{{ quote()!.id }}</td>
              </tr>
              <tr>
                <th>QUOTE</th>
                <td><em>"{{ quote()!.text }}"</em></td>
              </tr>
              <tr>
                <th>BY AUTHOR</th>
                <td>{{ quote()!.author }}</td>
              </tr>
              <tr>
                <th>CREATED ON</th>
                <td>{{ quote()!.createdAt | date: 'mediumDate' }}</td>
              </tr>
              <tr>
                <th>TAG</th>
                <td>
                  <span class="badge tag-{{ tagFor(quote()!.id) }}">
                    {{ tagFor(quote()!.id) }}
                  </span>
                </td>
              </tr>
              <tr>
                <th>CATEGORY</th>
                <td>
                  <span class="badge cat-{{ catFor(quote()!.id) }}">
                    {{ catFor(quote()!.id) }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class QuoteDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly state = inject(QuotesStateService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = this.state.loading;
  readonly quote = this.state.selectedQuote;
  readonly notFound = computed(() => this.state.error() === 'NOT_FOUND');
  readonly error = computed(() => {
    const e = this.state.error();
    return e && e !== 'NOT_FOUND' ? e : null;
  });

  tagFor(id: number): string { return TAGS[id % TAGS.length]; }
  catFor(id: number): string { return CATS[id % CATS.length]; }

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        switchMap((params) => {
          const id = Number(params.get('id'));
          if (isNaN(id)) {
            this.state.setError('Invalid quote ID.');
            return EMPTY;
          }
          this.state.loadQuote(id);
          return EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }
}
