import { Component, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  styleUrl: './login.component.css',
  template: `
    <div class="login-wrapper">
      <div class="card">
        <h2>Welcome back</h2>
        <p class="subtitle">Enter your user ID to continue</p>
        <form (ngSubmit)="onSubmit()">
          <div class="field">
            <label for="userId">User ID</label>
            <input
              id="userId"
              type="text"
              name="userId"
              [(ngModel)]="userId"
              placeholder="e.g. alice"
              autocomplete="username"
              required
            />
          </div>
          <button type="submit" [disabled]="!userId.trim()">Sign in</button>
        </form>
        @if (error()) {
          <p class="error">{{ error() }}</p>
        }
      </div>
    </div>
  `,
})
export class LoginComponent {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  userId = '';
  readonly error = signal<string | null>(null);

  onSubmit(): void {
    if (!this.userId.trim()) return;
    this.http
      .post<{ accessToken: string }>(
        `${environment.apiBaseUrl}/auth/token`,
        { userId: this.userId, scopes: [] },
      )
      .subscribe({
        next: (res) => {
          this.auth.setToken(res.accessToken);
          this.router.navigate(['/quotes']);
        },
        error: () => {
          this.error.set('Login failed. Please try again.');
        },
      });
  }
}
