import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AppStore } from '../store/app.store';

@Component({
  selector: 'app-auth-callback',
  standalone: true,
  imports: [],
  template: `
    <div class="callback">
      <div class="callback__spinner"></div>
      <p>Completing authentication…</p>
    </div>
  `,
  styles: [`
    .callback {
      height: 100vh;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 1rem;
      color: var(--text-secondary);
    }

    .callback__spinner {
      width: 32px;
      height: 32px;
      border: 3px solid var(--border);
      border-top-color: var(--accent);
      border-radius: 50%;
      animation: spin 0.7s linear infinite;
    }

    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class AuthCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly store = inject(AppStore);

  ngOnInit(): void {
    const connectionId = this.route.snapshot.queryParamMap.get('connectionId');
    this.store.loadConnections();
    if (connectionId) {
      setTimeout(() => {
        this.store.selectConnection(connectionId);
        this.router.navigate(['/dashboard']);
      }, 600);
    } else {
      this.router.navigate(['/dashboard']);
    }
  }
}
