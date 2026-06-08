import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { AppError, errorInterceptor } from './error.interceptor';

describe('errorInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('maps 4xx with title to AppError.message', () => {
    let error: AppError | undefined;

    http.get('/test').subscribe({ error: (e: AppError) => (error = e) });
    controller.expectOne('/test').flush(
      { title: 'One or more validation errors occurred.', status: 400, errors: { name: ['Required'] } },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(error?.message).toBe('One or more validation errors occurred.');
    expect(error?.status).toBe(400);
    expect(error?.details).toEqual({ name: ['Required'] });
  });

  it('falls back to default message when title is absent', () => {
    let error: AppError | undefined;

    http.get('/test').subscribe({ error: (e: AppError) => (error = e) });
    controller.expectOne('/test').flush(
      { status: 400 },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(error?.message).toBe('An unexpected error occurred.');
    expect(error?.status).toBe(400);
    expect(error?.details).toBeUndefined();
  });

  it('passes 5xx errors through as HttpErrorResponse', () => {
    let error: unknown;

    http.get('/test').subscribe({ error: (e: unknown) => (error = e) });
    controller.expectOne('/test').flush('Server Error', {
      status: 500,
      statusText: 'Internal Server Error',
    });

    expect(error instanceof HttpErrorResponse).toBe(true);
  });
});
