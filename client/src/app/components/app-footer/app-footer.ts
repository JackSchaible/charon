import { Component, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'ch-app-footer',
  templateUrl: './app-footer.html',
  styleUrl: './app-footer.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AppFooterComponent {
  protected currentYear = new Date().getFullYear();
}
