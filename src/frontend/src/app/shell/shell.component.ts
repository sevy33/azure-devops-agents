import { Component, inject, OnInit } from '@angular/core';
import { AppStore } from '../store/app.store';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { ChatComponent } from '../chat/chat.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [SidebarComponent, ChatComponent],
  template: `
    <div class="shell">
      <aside class="shell__sidebar" [class.shell__sidebar--collapsed]="!store.isSidebarExpanded()">
        <app-sidebar />
      </aside>

      <main class="shell__main">
        @if (store.activeSessionId()) {
          <app-chat />
        } @else {
          <div class="shell__empty">
            <div class="shell__empty-inner">
              <div class="shell__logo">⚡</div>
              <h2>Azure DevOps AI Agents</h2>
              <p>Select a connection and start a chat session from the sidebar.</p>
            </div>
          </div>
        }
      </main>
    </div>
  `,
  styles: [`
    .shell {
      display: flex;
      height: 100vh;
      background: var(--bg-base);
      color: var(--text-primary);
      overflow: hidden;
    }

    .shell__sidebar {
      width: 280px;
      flex-shrink: 0;
      background: var(--bg-sidebar);
      border-right: 1px solid var(--border);
      transition: width 0.2s ease;
      overflow: hidden;
    }

    .shell__sidebar--collapsed {
      width: 0;
    }

    .shell__main {
      flex: 1;
      display: flex;
      flex-direction: column;
      min-width: 0;
      min-height: 0;
    }

    .shell__empty {
      flex: 1;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .shell__empty-inner {
      text-align: center;
      color: var(--text-muted);
    }

    .shell__logo {
      font-size: 3rem;
      margin-bottom: 1rem;
    }

    .shell__empty-inner h2 {
      font-size: 1.25rem;
      font-weight: 600;
      color: var(--text-secondary);
      margin-bottom: 0.5rem;
    }

    .shell__empty-inner p {
      font-size: 0.875rem;
    }
  `]
})
export class ShellComponent implements OnInit {
  protected readonly store = inject(AppStore);

  ngOnInit(): void {
    this.store.loadConnections();
  }
}
