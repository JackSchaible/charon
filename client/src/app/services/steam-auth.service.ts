import { Injectable, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { ConfigService } from './config.service';

export interface User {
  Id: number;
  SteamId: string;
  Username: string;
  AvatarUrl: string;
  ProfileUrl: string;
  CreatedAt: string;
}

@Injectable({
  providedIn: 'root',
})
export class SteamAuthService {
  private configService = inject(ConfigService);

  private userSubject = new BehaviorSubject<User | null>(null);
  private isLoadingSubject = new BehaviorSubject<boolean>(false);
  private errorSubject = new BehaviorSubject<string | null>(null);

  public user$ = this.userSubject.asObservable();
  public isLoading$ = this.isLoadingSubject.asObservable();
  public error$ = this.errorSubject.asObservable();

  constructor() {
    this.loadUserFromStorage();
  }

  get user(): User | null {
    return this.userSubject.value;
  }

  get isLoading(): boolean {
    return this.isLoadingSubject.value;
  }

  get error(): string | null {
    return this.errorSubject.value;
  }

  login(): void {
    this.isLoadingSubject.next(true);
    this.errorSubject.next(null);

    const returnUrl = window.location.origin;
    window.location.href = `${
      this.configService.AUTH_URL
    }/login?returnUrl=${encodeURIComponent(returnUrl)}`;
  }

  logout(): void {
    this.userSubject.next(null);
    localStorage.removeItem('steam_user');
    localStorage.removeItem('jwt_token');

    // Clear URL parameters
    const url = new URL(window.location.href);
    url.searchParams.delete('token');
    url.searchParams.delete('error');
    window.history.replaceState({}, '', url.toString());
  }

  handleAuthCallback(): void {
    const urlParams = new URLSearchParams(window.location.search);
    const tokenParam = urlParams.get('token');
    const errorParam = urlParams.get('error');

    if (errorParam) {
      this.errorSubject.next(decodeURIComponent(errorParam));
      this.isLoadingSubject.next(false);
      return;
    }

    if (tokenParam) {
      try {
        this.isLoadingSubject.next(true);

        // Store the JWT token
        const jwtToken = decodeURIComponent(tokenParam);
        localStorage.setItem('jwt_token', jwtToken);

        // Decode the JWT to get user info (basic decode, not verification)
        const base64Url = jwtToken.split('.')[1];
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(
          atob(base64)
            .split('')
            .map(function (c) {
              return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
            })
            .join('')
        );

        const payload = JSON.parse(jsonPayload);

        // Create user object from JWT payload
        const userData: User = {
          Id: parseInt(payload.nameid || payload.sub),
          SteamId: payload.steam_id,
          Username: payload.unique_name || payload.name,
          AvatarUrl: '', // Not stored in JWT, will need to fetch if needed
          ProfileUrl: '', // Not stored in JWT, will need to fetch if needed
          CreatedAt: new Date().toISOString(),
        };

        this.userSubject.next(userData);
        localStorage.setItem('steam_user', JSON.stringify(userData));

        // Clear URL parameters
        const url = new URL(window.location.href);
        url.searchParams.delete('token');
        url.searchParams.delete('error');
        window.history.replaceState({}, '', url.toString());

        this.isLoadingSubject.next(false);
      } catch (err) {
        this.errorSubject.next(
          err instanceof Error
            ? err.message
            : 'Failed to process authentication token'
        );
        this.isLoadingSubject.next(false);
      }
    }
  }

  private loadUserFromStorage(): void {
    const storedUser = localStorage.getItem('steam_user');
    if (storedUser) {
      try {
        this.userSubject.next(JSON.parse(storedUser));
      } catch {
        localStorage.removeItem('steam_user');
      }
    }
  }

  getAuthToken(): string | null {
    return localStorage.getItem('jwt_token');
  }

  getAuthHeaders(): Record<string, string> {
    const token = this.getAuthToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }
    return headers;
  }
}
