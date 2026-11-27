import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CSRF_HEADER, csrfInterceptor } from './csrf.interceptor';

describe('csrfInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([csrfInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
  });

  it.each(['POST', 'PUT', 'PATCH', 'DELETE'])(
    'adds the anti-forgery header to same-origin %s',
    (method) => {
      http.request(method, '/api/v1/members', { body: {} }).subscribe();
      expect(backend.expectOne('/api/v1/members').request.headers.get(CSRF_HEADER)).toBe('1');
    },
  );

  it('leaves safe methods alone', () => {
    http.get('/api/v1/members').subscribe();
    expect(backend.expectOne('/api/v1/members').request.headers.has(CSRF_HEADER)).toBe(false);
  });

  it('never sends the header to another origin', () => {
    http.post('https://evil.example/api', {}).subscribe();
    expect(backend.expectOne('https://evil.example/api').request.headers.has(CSRF_HEADER)).toBe(
      false,
    );
  });
});
