import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AppStore } from '../store/app.store';
import { ApiService } from '../services/api.service';
import { AddConnectionDialogComponent } from '../add-connection-dialog/add-connection-dialog.component';
import type { RepoInfo } from '../models/types';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [FormsModule, AddConnectionDialogComponent],
  template: `
    <div class="sidebar">
      <div class="sidebar__header">
        <span class="sidebar__title">ADO Agents</span>
        <button class="btn-icon" title="Add Connection" (click)="openAddConnection()">＋</button>
      </div>

      @if (store.loadingConnections()) {
        <div class="sidebar__spinner">Loading…</div>
      }

      @for (conn of store.connections(); track conn.id) {
        <div class="sidebar__connection" [class.active]="conn.id === store.selectedConnectionId()">
          <button class="sidebar__connection-btn" (click)="selectConnection(conn.id)">
            <div class="sidebar__conn-name">{{ conn.displayName }}</div>
            <div class="sidebar__conn-sub">{{ conn.organizationUrl }}</div>
          </button>

          <div class="sidebar__conn-actions">
            @if (!conn.isAuthenticated) {
              <button class="btn-link" (click)="authenticate(conn.id)">Authenticate</button>
            }
            <button class="btn-link" (click)="newSession(conn.id)">New Chat</button>
          </div>

          @if (conn.id === store.selectedConnectionId()) {
            <!-- Sessions -->
            @for (session of store.sessions(); track session.id) {
              <button
                class="sidebar__session"
                [class.active]="session.id === store.activeSessionId()"
                (click)="store.selectSession(session.id)"
              >
                {{ sessionLabel(session.createdAt) }}
              </button>
            }

            <!-- Repos -->
            <div class="sidebar__section-label">Repositories</div>
            @for (repo of getRepos(conn.id); track repo.id) {
              <div class="sidebar__repo">
                <span class="sidebar__repo-name">{{ repo.repoName }}</span>
                <span class="sidebar__repo-badge" [attr.data-status]="repo.cloneStatus">
                  {{ repo.cloneStatus }}
                </span>
                @if (repo.cloneStatus === 'Pending' || repo.cloneStatus === 'Failed') {
                  <button class="btn-link btn-link--sm" (click)="cloneRepo(conn.id, repo.repoName)">Clone</button>
                }
              </div>
            }
          }
        </div>
      }

      @if (store.connections().length === 0 && !store.loadingConnections()) {
        <div class="sidebar__empty">No connections yet.<br>Click ＋ to add one.</div>
      }
    </div>

    @if (store.isAddConnectionOpen()) {
      <app-add-connection-dialog (close)="onDialogClose($event)" />
    }
  `,
  styles: [`
    .sidebar {
      display: flex;
      flex-direction: column;
      height: 100%;
      overflow-y: auto;
      font-size: 0.875rem;
    }

    .sidebar__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 1rem;
      border-bottom: 1px solid var(--border);
      position: sticky;
      top: 0;
      background: var(--bg-sidebar);
      z-index: 1;
    }

    .sidebar__title {
      font-weight: 700;
      font-size: 1rem;
      color: var(--text-primary);
    }

    .sidebar__spinner {
      padding: 1rem;
      color: var(--text-muted);
      font-size: 0.8rem;
    }

    .sidebar__connection {
      border-bottom: 1px solid var(--border);
      padding: 0.25rem 0;
    }

    .sidebar__connection-btn {
      all: unset;
      display: block;
      width: 100%;
      padding: 0.5rem 1rem;
      cursor: pointer;
      border-radius: 0;
      transition: background 0.1s;
    }

    .sidebar__connection-btn:hover {
      background: var(--hover);
    }

    .sidebar__connection.active > .sidebar__connection-btn {
      background: var(--active);
    }

    .sidebar__conn-name {
      font-weight: 600;
      color: var(--text-primary);
    }

    .sidebar__conn-sub {
      font-size: 0.75rem;
      color: var(--text-muted);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .sidebar__conn-actions {
      display: flex;
      gap: 0.5rem;
      padding: 0 1rem 0.25rem;
    }

    .sidebar__session {
      all: unset;
      display: block;
      width: 100%;
      padding: 0.35rem 1.5rem;
      cursor: pointer;
      color: var(--text-secondary);
      font-size: 0.8rem;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    .sidebar__session:hover { background: var(--hover); }
    .sidebar__session.active {
      background: var(--active);
      color: var(--accent);
    }

    .sidebar__section-label {
      padding: 0.5rem 1rem 0.25rem;
      font-size: 0.7rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: var(--text-muted);
    }

    .sidebar__repo {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.25rem 1rem;
      font-size: 0.8rem;
    }

    .sidebar__repo-name {
      flex: 1;
      color: var(--text-secondary);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .sidebar__repo-badge {
      font-size: 0.7rem;
      padding: 0.1rem 0.4rem;
      border-radius: 9999px;
      background: var(--border);
      color: var(--text-muted);
    }

    .sidebar__repo-badge[data-status='Ready'] {
      background: var(--success-bg);
      color: var(--success);
    }

    .sidebar__repo-badge[data-status='Cloning'] {
      background: var(--warn-bg);
      color: var(--warn);
    }

    .sidebar__repo-badge[data-status='Failed'] {
      background: var(--error-bg);
      color: var(--error);
    }

    .sidebar__empty {
      padding: 2rem 1rem;
      text-align: center;
      color: var(--text-muted);
      font-size: 0.85rem;
      line-height: 1.5;
    }

    .btn-icon {
      all: unset;
      cursor: pointer;
      font-size: 1.2rem;
      color: var(--text-muted);
      width: 28px;
      height: 28px;
      display: flex;
      align-items: center;
      justify-content: center;
      border-radius: 4px;
    }

    .btn-icon:hover { background: var(--hover); color: var(--text-primary); }

    .btn-link {
      all: unset;
      cursor: pointer;
      color: var(--accent);
      font-size: 0.75rem;
    }

    .btn-link:hover { text-decoration: underline; }

    .btn-link--sm { font-size: 0.7rem; }
  `]
})
export class SidebarComponent {
  protected readonly store = inject(AppStore);
  private readonly api = inject(ApiService);

  selectConnection(id: string): void {
    this.store.selectConnection(id);
  }

  newSession(connectionId: string): void {
    this.store.createSession(connectionId);
  }

  authenticate(connectionId: string): void {
    window.location.href = this.api.getOAuthLoginUrl(connectionId);
  }

  cloneRepo(connectionId: string, repoName: string): void {
    this.api.cloneRepo(connectionId, repoName).subscribe();
  }

  getRepos(connectionId: string): RepoInfo[] {
    return this.store.reposByConnection()[connectionId] ?? [];
  }

  sessionLabel(createdAt: string): string {
    const d = new Date(createdAt);
    return d.toLocaleDateString() + ' ' + d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  openAddConnection(): void {
    this.store.isAddConnectionOpen.set(true);
  }

  onDialogClose(created: boolean): void {
    this.store.isAddConnectionOpen.set(false);
    if (created) {
      this.store.loadConnections();
    }
  }
}
