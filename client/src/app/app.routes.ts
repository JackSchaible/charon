import { Routes } from '@angular/router';
import { SteamLoginComponent } from './pages/steam-login/steam-login';
import { WishlistPageComponent } from './pages/wishlist-page/wishlist-page';
import { SteamCallbackComponent } from './pages/steam-callback/steam-callback';
import { WishlistProcessingComponent } from './pages/wishlist-processing/wishlist-processing';

export const routes: Routes = [
  {
    path: '',
    component: SteamLoginComponent,
  },
  {
    path: 'wishlist',
    component: WishlistPageComponent,
  },
  {
    path: 'callback',
    component: SteamCallbackComponent,
  },
  {
    path: 'processing',
    component: WishlistProcessingComponent,
  },
];
