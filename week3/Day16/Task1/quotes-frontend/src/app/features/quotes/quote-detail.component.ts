import {
  Component,
  inject,
  signal,
  OnInit,
  OnDestroy,
} from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { EMPTY, Subscription } from 'rxjs';
import { switchMap, tap } from 'rxjs/operators';
import { AuthService } from '../../core/auth.service';

const TAGS = ['wisdom', 'motivation', 'philosophy', 'success', 'life', 'humor', 'truth', 'change', 'perseverance', 'education'];
const CATS = ['classic', 'modern'];

interface QuoteEntity {
  id: number;
  author: string;
  text: string;
  createdAt: string;
}

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
export class QuoteDetailComponent implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);

  readonly loading = signal(true);
  readonly notFound = signal(false);
  readonly error = signal<string | null>(null);
  readonly quote = signal<QuoteEntity | null>(null);

  private sub?: Subscription;

  tagFor(id: number): string { return TAGS[id % TAGS.length]; }
  catFor(id: number): string { return CATS[id % CATS.length]; }

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
        next: (q) => {
          this.quote.set(q);
          this.loading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          if (err.status === 404) {
            this.notFound.set(true);
          } else {
            this.error.set('Failed to load quote.');
          }
          this.loading.set(false);
        },
      });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }
}
