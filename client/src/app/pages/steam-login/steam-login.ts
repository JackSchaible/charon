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
  selector: 'ch-steam-login',
  imports: [CommonModule],
  standalone: true,
  templateUrl: './steam-login.html',
  styleUrl: './steam-login.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SteamLoginComponent implements OnInit {
  private authService = inject(SteamAuthService);
  private router = inject(Router);

  protected user$ = this.authService.user$;
  protected isLoading$ = this.authService.isLoading$;
  protected error$ = this.authService.error$;

  ngOnInit(): void {
    // Redirect to wishlist if user is already logged in
    this.user$.subscribe((user) => {
      if (user) {
        this.router.navigate(['/wishlist']);
      }
    });
  }

  login(): void {
    this.authService.login();
  }
}
