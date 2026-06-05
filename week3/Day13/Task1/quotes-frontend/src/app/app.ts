import { Component } from '@angular/core';
import { QuotesComponent } from './quotes/quotes.component';

@Component({
  selector: 'app-root',
  imports: [QuotesComponent],
  template: `<app-quotes />`,
})
export class App {}
