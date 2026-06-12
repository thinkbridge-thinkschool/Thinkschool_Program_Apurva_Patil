import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

export interface AppError {
  message: string;
  status: number;
  details?: Record<string, string[]>;
}

interface ProblemDetails {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        localStorage.removeItem('accessToken');
        router.navigate(['/login']);
        return throwError(() => ({ message: 'Session expired.', status: 401 } as AppError));
      }
      if (err.status >= 400 && err.status < 500) {
        const problem = err.error as ProblemDetails;
        const appError: AppError = {
          message: problem?.title ?? 'An unexpected error occurred.',
          status: err.status,
          details: problem?.errors,
        };
        return throwError(() => appError);
      }
      return throwError(() => err);
    }),
  );
};
