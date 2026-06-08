import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { QuoteDetailComponent } from './quote-detail.component';

function makeRoute(id: string) {
  return { paramMap: of(convertToParamMap({ id })) };
}

describe('QuoteDetailComponent', () => {
  let controller: HttpTestingController;

  function setup(id: string) {
    TestBed.configureTestingModule({
      imports: [QuoteDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ActivatedRoute, useValue: makeRoute(id) },
      ],
    });
    controller = TestBed.inject(HttpTestingController);
    return TestBed.createComponent(QuoteDetailComponent);
  }

  afterEach(() => controller.verify());

  it('displays quote fields when GET /api/quotes/:id returns 200', () => {
    const fixture = setup('5');
    fixture.detectChanges();

    controller.expectOne('http://localhost:5255/api/quotes/5').flush({
      id: 5,
      author: 'Marcus Aurelius',
      text: 'You have power over your mind.',
      createdAt: '2026-01-01T00:00:00Z',
    });

    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.loading()).toBe(false);
    expect(comp.notFound()).toBe(false);
    expect(comp.error()).toBeNull();
    expect(comp.quote()?.id).toBe(5);
    expect(comp.quote()?.author).toBe('Marcus Aurelius');
  });

  it('sets notFound when GET /api/quotes/:id returns 404', () => {
    const fixture = setup('999');
    fixture.detectChanges();

    controller.expectOne('http://localhost:5255/api/quotes/999').flush(
      { error: 'Quote not found' },
      { status: 404, statusText: 'Not Found' },
    );

    fixture.detectChanges();
    const comp = fixture.componentInstance;
    expect(comp.notFound()).toBe(true);
    expect(comp.loading()).toBe(false);
    expect(comp.quote()).toBeNull();
  });

  it('sets error and makes no HTTP request when id param is non-numeric', () => {
    const fixture = setup('abc');
    fixture.detectChanges();

    // No request should be made — controller.verify() in afterEach confirms this
    const comp = fixture.componentInstance;
    expect(comp.error()).toBe('Invalid quote ID.');
    expect(comp.loading()).toBe(false);
    expect(comp.quote()).toBeNull();
  });
});
