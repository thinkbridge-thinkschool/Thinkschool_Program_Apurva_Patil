import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthService {
  // Token lives only in memory — never written to localStorage or sessionStorage.
  // XSS cannot read it via document.cookie or storage APIs.
  // Tradeoff: user must log in again after a page refresh.
  private token: string | null = null;

  setToken(token: string): void {
    this.token = token;
  }

  clearToken(): void {
    this.token = null;
  }

  getToken(): string | null {
    return this.token;
  }

  isLoggedIn(): boolean {
    if (!this.token) return false;
    try {
      // Decode the JWT payload (middle segment) and check the exp claim.
      // We cannot verify the signature on the frontend (symmetric key is secret),
      // but checking expiry prevents the auth guard from passing stale tokens.
      const payload = JSON.parse(atob(this.token.split('.')[1]));
      return typeof payload.exp === 'number' && payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }
}
