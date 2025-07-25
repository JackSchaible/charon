import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, interval } from 'rxjs';
import { takeWhile, finalize } from 'rxjs/operators';
import { ConfigService } from './config.service';
import { SteamAuthService } from './steam-auth.service';

export interface WishlistItem {
  UserId: number;
  GameId: string;
  Notes?: string;
  AddedAt: string;
  // Game details will be populated via join
  AppId?: string;
  Title?: string;
  ImageUrl?: string;
  Price?: string;
  ReleaseDate?: string;
  LastFetched?: string;
}

export interface Job {
  Id: number;
  UserId: number;
  Type: string;
  StatusId: number;
  CreatedAt: string;
  UpdatedAt: string;
  Details?: string;
}

@Injectable({
  providedIn: 'root',
})
export class WishlistService {
  private configService = inject(ConfigService);
  private authService = inject(SteamAuthService);

  private wishlistSubject = new BehaviorSubject<WishlistItem[]>([]);
  private isLoadingSubject = new BehaviorSubject<boolean>(false);
  private isSyncingSubject = new BehaviorSubject<boolean>(false);
  private errorSubject = new BehaviorSubject<string | null>(null);
  private syncProgressSubject = new BehaviorSubject<string>('');

  public wishlist$ = this.wishlistSubject.asObservable();
  public isLoading$ = this.isLoadingSubject.asObservable();
  public isSyncing$ = this.isSyncingSubject.asObservable();
  public error$ = this.errorSubject.asObservable();
  public syncProgress$ = this.syncProgressSubject.asObservable();

  get wishlist(): WishlistItem[] {
    return this.wishlistSubject.value;
  }

  get isLoading(): boolean {
    return this.isLoadingSubject.value;
  }

  get isSyncing(): boolean {
    return this.isSyncingSubject.value;
  }

  get error(): string | null {
    return this.errorSubject.value;
  }

  get syncProgress(): string {
    return this.syncProgressSubject.value;
  }

  async fetchWishlist(): Promise<void> {
    try {
      this.isLoadingSubject.next(true);
      this.errorSubject.next(null);

      const response = await fetch(
        `${this.configService.API_BASE_URL}/api/wishlist`,
        {
          headers: this.authService.getAuthHeaders(),
        }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch wishlist: ${response.statusText}`);
      }

      const data: WishlistItem[] = await response.json();
      this.wishlistSubject.next(data);
    } catch (err) {
      const errorMessage =
        err instanceof Error ? err.message : 'Failed to fetch wishlist';
      this.errorSubject.next(errorMessage);
      console.error('Error fetching wishlist:', err);
    } finally {
      this.isLoadingSubject.next(false);
    }
  }

  private async pollJobStatus(jobId: number): Promise<boolean> {
    try {
      const response = await fetch(
        `${this.configService.API_BASE_URL}/api/jobs/${jobId}`,
        {
          headers: this.authService.getAuthHeaders(),
        }
      );

      if (!response.ok) {
        throw new Error(`Failed to fetch job status: ${response.statusText}`);
      }

      const job: Job = await response.json();

      // Update progress display
      if (job.Details) {
        this.syncProgressSubject.next(job.Details);
      }

      // Return true if job is complete (either success or error)
      return job.StatusId === 3 || job.StatusId === 4; // Assuming 3=COMPLETE, 4=ERROR
    } catch (err) {
      console.error('Error polling job status:', err);
      return false; // Continue polling on error
    }
  }

  async syncWishlist(force = false): Promise<void> {
    try {
      this.isSyncingSubject.next(true);
      this.errorSubject.next(null);
      this.syncProgressSubject.next('Starting wishlist synchronization...');

      // Get current user from JWT token
      const token = this.authService.getAuthToken();
      if (!token) {
        throw new Error('Authentication required');
      }

      // Trigger sync via AuthFunction
      const response = await fetch(`${this.configService.AUTH_URL}/sync`, {
        method: 'POST',
        headers: this.authService.getAuthHeaders(),
        body: JSON.stringify({ force }),
      });

      if (!response.ok) {
        throw new Error(`Failed to start sync: ${response.statusText}`);
      }

      const data = await response.json();
      const jobId = data.jobId;

      if (!jobId) {
        throw new Error('No job ID returned from sync request');
      }

      this.syncProgressSubject.next('Sync started, monitoring progress...');

      // Use RxJS interval to poll job status
      interval(2000) // Poll every 2 seconds
        .pipe(
          takeWhile(() => this.isSyncingSubject.value),
          finalize(() => {
            this.isSyncingSubject.next(false);
            this.syncProgressSubject.next('');
          })
        )
        .subscribe(async () => {
          const isComplete = await this.pollJobStatus(jobId);

          if (isComplete) {
            this.syncProgressSubject.next(
              'Sync completed, refreshing wishlist...'
            );

            // Refresh the wishlist
            await this.fetchWishlist();

            this.isSyncingSubject.next(false);
            this.syncProgressSubject.next('');
          }
        });

      // Set a timeout to stop polling after 5 minutes
      setTimeout(() => {
        if (this.isSyncingSubject.value) {
          this.isSyncingSubject.next(false);
          this.errorSubject.next(
            'Sync timeout - please check job status manually'
          );
          this.syncProgressSubject.next('');
        }
      }, 300000); // 5 minutes
    } catch (err) {
      const errorMessage =
        err instanceof Error ? err.message : 'Failed to sync wishlist';
      this.errorSubject.next(errorMessage);
      this.isSyncingSubject.next(false);
      this.syncProgressSubject.next('');
      console.error('Error syncing wishlist:', err);
    }
  }
}
