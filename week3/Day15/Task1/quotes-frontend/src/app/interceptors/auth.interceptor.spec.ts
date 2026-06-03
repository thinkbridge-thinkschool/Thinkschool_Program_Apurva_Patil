import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('adds Authorization header to GET requests', () => {
    http.get('/test').subscribe();
    const req = controller.expectOne('/test');
    expect(req.request.headers.get('Authorization')).toBe('Bearer quotes-app-token');
    req.flush(null);
  });

  it('adds Authorization header to POST requests', () => {
    http.post('/test', {}).subscribe();
    const req = controller.expectOne('/test');
    expect(req.request.headers.get('Authorization')).toBe('Bearer quotes-app-token');
    req.flush(null);
  });
});
