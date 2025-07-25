import { Injectable } from '@angular/core';
import { environment } from '../../environments/environment';

export interface Config {
  API_BASE_URL: string;
  AUTH_URL: string;
}

// Extend the Window interface to include our custom env property
declare global {
  interface Window {
    env?: {
      API_BASE_URL?: string;
      AUTH_URL?: string;
    };
  }
}

@Injectable({
  providedIn: 'root',
})
export class ConfigService {
  private config: Config;

  constructor() {
    // Priority: runtime config (window.env) -> environment file -> defaults
    this.config = {
      API_BASE_URL:
        window.env?.API_BASE_URL ||
        environment.API_BASE_URL ||
        'http://localhost:3000',
      AUTH_URL:
        window.env?.AUTH_URL || environment.AUTH_URL || 'http://localhost:3001',
    };
  }

  get API_BASE_URL(): string {
    return this.config.API_BASE_URL;
  }

  get AUTH_URL(): string {
    return this.config.AUTH_URL;
  }

  // Utility method to get the full config object
  getConfig(): Config {
    return { ...this.config };
  }
}
