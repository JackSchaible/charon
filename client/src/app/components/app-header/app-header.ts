import { Component, inject, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SteamAuthService } from '../../services/steam-auth.service';

@Component({
  selector: 'ch-app-header',
  imports: [CommonModule, RouterLink],
  templateUrl: './app-header.html',
  styleUrl: './app-header.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppHeaderComponent {
  private authService = inject(SteamAuthService);

  protected user$ = this.authService.user$;

  logout(): void {
    this.authService.logout();
  }
}
