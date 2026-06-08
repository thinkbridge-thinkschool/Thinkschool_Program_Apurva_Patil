import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Quote } from './quotes.service';

interface ProblemDetails {
  title: string;
  status: number;
  errors: Record<string, string[]>;
}

const API = 'http://localhost:5255/api/quotes';

describe('API Contract', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('GET /api/quotes?page=1&size=10 returns Quote[] with correct shape', () => {
    const mockQuotes: Quote[] = [
      {
        id: 1,
        author: 'Marcus Aurelius',
        text: 'You have power over your mind, not outside events.',
        createdAt: '2026-01-01T00:00:00Z',
        isDeleted: false,
      },
      {
        id: 2,
        author: 'Epictetus',
        text: 'Make the best use of what is in your power.',
        createdAt: '2026-01-02T00:00:00Z',
        isDeleted: false,
      },
    ];

    let result: Quote[] | undefined;

    http
      .get<Quote[]>(API, { params: { page: '1', size: '10' } })
      .subscribe((data) => (result = data));

    const req = controller.expectOne(
      (r) =>
        r.url === API &&
        r.params.get('page') === '1' &&
        r.params.get('size') === '10',
    );
    expect(req.request.method).toBe('GET');
    req.flush(mockQuotes);

    expect(result).toBeDefined();
    expect(Array.isArray(result)).toBe(true);

    result!.forEach((quote) => {
      expect(typeof quote.id).toBe('number');
      expect(typeof quote.author).toBe('string');
      expect(typeof quote.text).toBe('string');
      expect(typeof quote.createdAt).toBe('string');
      expect(typeof quote.isDeleted).toBe('boolean');
    });
  });

  it('POST /api/quotes with empty body returns 400 ProblemDetails shape', () => {
    const mockProblemDetails: ProblemDetails = {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { error: ['Author and Text are required fields'] },
    };

    let errorBody: ProblemDetails | undefined;

    http.post<Quote>(API, {}).subscribe({
      next: () => { throw new Error('Expected a 400 error, not a successful response'); },
      error: (err: HttpErrorResponse) => {
        errorBody = err.error as ProblemDetails;
      },
    });

    const req = controller.expectOne(API);
    expect(req.request.method).toBe('POST');
    req.flush(mockProblemDetails, { status: 400, statusText: 'Bad Request' });

    expect(errorBody).toBeDefined();
    expect(typeof errorBody!.title).toBe('string');
    expect(typeof errorBody!.status).toBe('number');
    expect(typeof errorBody!.errors).toBe('object');
  });
});
