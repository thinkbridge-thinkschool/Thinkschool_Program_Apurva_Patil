import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  styleUrl: './navbar.component.css',
  template: `
    <nav class="navbar">
      <div class="nav-brand">
        <span class="nav-title">Quotes UI</span>
        <span class="nav-subtitle">signal · computed · effect · &#64;if · &#64;for · &#64;switch · inject()</span>
      </div>
      <div class="nav-links">
        <a routerLink="/quotes" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Quotes</a>
        <a routerLink="/create" routerLinkActive="active">Create</a>
        <button class="logout-btn" (click)="logout()">Log out</button>
      </div>
    </nav>
  `,
})
export class NavbarComponent {
  private readonly router = inject(Router);
  private readonly auth = inject(AuthService);

  logout(): void {
    this.auth.clearToken();
    this.router.navigate(['/login']);
  }
}
