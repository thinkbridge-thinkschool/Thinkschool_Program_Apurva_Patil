import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { throwError, timer } from 'rxjs';
import { retry } from 'rxjs/operators';

export const retryInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.method !== 'GET') {
    return next(req);
  }

  return next(req).pipe(
    retry({
      count: 3,
      delay: (err: unknown, retryCount: number) => {
        // Never retry client errors (4xx) — a bad/expired token won't fix itself.
        if (err instanceof HttpErrorResponse && err.status >= 400 && err.status < 500) {
          return throwError(() => err);
        }
        return timer(1000 * Math.pow(2, retryCount - 1));
      },
    }),
  );
};
