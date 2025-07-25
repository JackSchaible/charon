import {
  Component,
  inject,
  ChangeDetectionStrategy,
  OnInit,
} from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { SteamAuthService } from '../../services/steam-auth.service';

@Component({
  selector: 'ch-steam-callback',
  imports: [CommonModule],
  templateUrl: './steam-callback.html',
  styleUrl: './steam-callback.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SteamCallbackComponent implements OnInit {
  private authService = inject(SteamAuthService);
  private router = inject(Router);

  protected user$ = this.authService.user$;
  protected isLoading$ = this.authService.isLoading$;
  protected error$ = this.authService.error$;

  ngOnInit(): void {
    // Watch for successful authentication and redirect to processing
    this.user$.subscribe((user) => {
      if (user) {
        // Small delay to show success message
        setTimeout(() => {
          this.router.navigate(['/processing']);
        }, 1500);
      }
    });
  }

  goToLogin(): void {
    this.router.navigate(['/']);
  }
}
