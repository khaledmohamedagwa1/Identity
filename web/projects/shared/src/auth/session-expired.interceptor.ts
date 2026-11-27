import { HttpErrorResponse, type HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { API_ROOTS } from '@ip/shared/api-clients';
import { BffAuthService } from './bff-auth.service';

/** When an API call returns 401 the BFF session is gone: send the user to sign in again. */
export const sessionExpiredInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(BffAuthService);
  const roots = inject(API_ROOTS);
  return next(req).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !req.url.startsWith(`${roots.bff}/user`) &&
        auth.isAuthenticated()
      ) {
        auth.login();
      }
      return throwError(() => error);
    }),
  );
};
