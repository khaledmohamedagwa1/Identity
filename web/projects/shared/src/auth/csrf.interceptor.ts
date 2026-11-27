import type { HttpInterceptorFn } from '@angular/common/http';

/** Header the BFF requires on state-changing calls (W-01). A custom header cannot be sent cross-site without CORS. */
export const CSRF_HEADER = 'X-CSRF';
const SAFE_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

/** Adds the anti-forgery header to same-origin, state-changing requests only. */
export const csrfInterceptor: HttpInterceptorFn = (req, next) => {
  const isRelative = !/^[a-z][a-z\d+\-.]*:\/\//i.test(req.url) && !req.url.startsWith('//');
  if (SAFE_METHODS.has(req.method) || !isRelative || req.headers.has(CSRF_HEADER)) {
    return next(req);
  }
  return next(req.clone({ setHeaders: { [CSRF_HEADER]: '1' } }));
};
