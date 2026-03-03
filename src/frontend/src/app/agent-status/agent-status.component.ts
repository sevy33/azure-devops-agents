import { Component, inject, signal } from '@angular/core';
import { AppStore } from '../store/app.store';
import type { AgentStatusEvent } from '../models/types';

@Component({
  selector: 'app-agent-status',
  standalone: true,
  imports: [],
  template: `
    @if (current(); as s) {
      <div class="agent-bar" [attr.data-status]="s.status">
        @if (s.status === 'Running' || s.status === 'Queued') {
          <span class="agent-bar__spinner"></span>
        }
        <span class="agent-bar__agent">{{ s.agentType }}</span>
        <span class="agent-bar__text">{{ statusText(s) }}</span>
        @if (s.detail) {
          <span class="agent-bar__detail">{{ s.detail }}</span>
        }
        @if (prLink()) {
          <a class="agent-bar__link" [href]="prLink()" target="_blank" rel="noopener">
            View PR ↗
          </a>
        }
        @if (s.status === 'Succeeded' || s.status === 'Failed' || s.status === 'Cancelled') {
          <button class="agent-bar__dismiss" (click)="dismiss()">✕</button>
        }
      </div>
    }
  `,
  styles: [`
    :host { display: block; }

    .agent-bar {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.45rem 1rem;
      font-size: 0.8rem;
      border-bottom: 1px solid var(--border);
      background: var(--bg-bar);
      color: var(--text-secondary);
      transition: background 0.2s;
    }

    .agent-bar[data-status='Queued'],
    .agent-bar[data-status='Running'] {
      background: var(--warn-bg);
      color: var(--warn);
    }

    .agent-bar[data-status='Succeeded'] {
      background: var(--success-bg);
      color: var(--success);
    }

    .agent-bar[data-status='Failed'],
    .agent-bar[data-status='Cancelled'] {
      background: var(--error-bg);
      color: var(--error);
    }

    .agent-bar__spinner {
      width: 12px;
      height: 12px;
      border: 2px solid currentColor;
      border-top-color: transparent;
      border-radius: 50%;
      animation: spin 0.7s linear infinite;
      flex-shrink: 0;
    }

    @keyframes spin { to { transform: rotate(360deg); } }

    .agent-bar__agent {
      font-weight: 600;
    }

    .agent-bar__detail {
      color: inherit;
      opacity: 0.8;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      max-width: 300px;
    }

    .agent-bar__link {
      color: inherit;
      font-weight: 600;
      text-decoration: underline;
    }

    .agent-bar__dismiss {
      all: unset;
      cursor: pointer;
      margin-left: auto;
      opacity: 0.6;
      font-size: 0.75rem;
    }

    .agent-bar__dismiss:hover { opacity: 1; }
  `]
})
export class AgentStatusComponent {
  protected readonly store = inject(AppStore);

  protected current = this.store.agentStatus;

  prLink = signal<string | null>(null);

  statusText(s: AgentStatusEvent): string {
    switch (s.status) {
      case 'Queued': return 'Queued…';
      case 'Running': return s.detail ?? 'Working…';
      case 'Succeeded': return 'Completed';
      case 'Failed': return 'Failed';
      case 'Cancelled': return 'Cancelled';
      default: return s.status;
    }
  }

  dismiss(): void {
    this.store.agentStatus.set(null);
    this.prLink.set(null);
  }
}
