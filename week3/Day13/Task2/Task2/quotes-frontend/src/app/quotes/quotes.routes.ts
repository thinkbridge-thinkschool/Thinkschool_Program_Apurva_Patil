import { Routes } from '@angular/router';
import { QuotesComponent } from './quotes.component';
import { QuoteDetailComponent } from './quote-detail/quote-detail.component';

export const quotesRoutes: Routes = [
  { path: '', redirectTo: 'quotes', pathMatch: 'full' },
  { path: 'quotes', component: QuotesComponent },
  { path: 'quotes/:id', component: QuoteDetailComponent },
];
