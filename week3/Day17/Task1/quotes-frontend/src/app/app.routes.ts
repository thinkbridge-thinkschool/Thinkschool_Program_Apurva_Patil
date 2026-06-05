import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const appRoutes: Routes = [
  { path: '', redirectTo: 'quotes', pathMatch: 'full' },

  // Lazy + guarded: the list bundle is only downloaded after login;
  // keeping it in its own chunk means unauthenticated users pay zero cost.
  {
    path: 'quotes',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/quotes/quotes-list.component').then(
        (m) => m.QuotesListComponent,
      ),
  },

  // Lazy + guarded: own chunk separate from the list so the detail bundle
  // is only fetched when a user actually navigates to a specific quote.
  {
    path: 'quotes/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/quotes/quote-detail.component').then(
        (m) => m.QuoteDetailComponent,
      ),
  },

  {
    path: 'create',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./quotes/create-quote/create-quote.component').then(
        (m) => m.CreateQuoteComponent,
      ),
  },

  // Public route: login must be reachable without a token so unauthenticated
  // users can obtain credentials; lazy-loaded to keep the initial bundle lean.
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
];
