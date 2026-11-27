import { provideHttpClient, withInterceptors, withNoXsrfProtection } from '@angular/common/http';
import type { EnvironmentProviders } from '@angular/core';
import { csrfInterceptor } from './csrf.interceptor';
import { sessionExpiredInterceptor } from './session-expired.interceptor';

/** HttpClient set up for the BFF: anti-forgery header and session-expiry handling. */
export function provideBffHttp(): EnvironmentProviders {
  return provideHttpClient(
    // The BFF checks a custom header (csrfInterceptor) instead of a double-submit cookie.
    withNoXsrfProtection(),
    withInterceptors([csrfInterceptor, sessionExpiredInterceptor]),
  );
}
