import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { vi } from 'vitest';
import { retryInterceptor } from './retry.interceptor';

describe('retryInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([retryInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    controller.verify();
    vi.useRealTimers();
  });

  it('does not retry POST on error — fires exactly once', () => {
    let error: unknown;

    http.post('/test', {}).subscribe({ error: (e) => (error = e) });
    controller.expectOne('/test').flush('err', { status: 500, statusText: 'Error' });

    expect(error).toBeDefined();
    // afterEach controller.verify() confirms no extra requests were made
  });

  it('retries GET 3 times with 1s/2s/4s backoff then errors', async () => {
    vi.useFakeTimers();
    let error: unknown;

    http.get('/test').subscribe({ error: (e) => (error = e) });

    // Initial attempt
    controller.expectOne('/test').flush('err', { status: 500, statusText: 'Error' });

    // 1st retry after 1000ms
    await vi.advanceTimersByTimeAsync(1000);
    controller.expectOne('/test').flush('err', { status: 500, statusText: 'Error' });

    // 2nd retry after 2000ms
    await vi.advanceTimersByTimeAsync(2000);
    controller.expectOne('/test').flush('err', { status: 500, statusText: 'Error' });

    // 3rd retry after 4000ms
    await vi.advanceTimersByTimeAsync(4000);
    controller.expectOne('/test').flush('err', { status: 500, statusText: 'Error' });

    expect(error).toBeDefined();
  });

  it('GET succeeds on first retry and stops retrying', async () => {
    vi.useFakeTimers();
    let result: unknown;

    http.get('/test').subscribe({ next: (r) => (result = r) });

    // Initial attempt fails
    controller.expectOne('/test').flush('err', { status: 500, statusText: 'Error' });

    // 1st retry succeeds
    await vi.advanceTimersByTimeAsync(1000);
    controller.expectOne('/test').flush({ id: 1, text: 'ok' });

    expect(result).toEqual({ id: 1, text: 'ok' });
    // afterEach controller.verify() confirms no further requests were made
  });
});
