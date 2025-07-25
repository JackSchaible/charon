import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppHeaderComponent } from './components/app-header/app-header';
import { AppFooterComponent } from './components/app-footer/app-footer';

@Component({
  selector: 'ch-root',
  imports: [RouterOutlet, AppHeaderComponent, AppFooterComponent],
  standalone: true,
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppComponent {
  protected readonly title = signal('Steam Wishlist Manager');
}
