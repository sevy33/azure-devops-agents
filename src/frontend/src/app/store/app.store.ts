import { computed, inject, Injectable, signal } from '@angular/core';
import { ApiService } from '../services/api.service';
import type { AgentStatusEvent, ChatMessage, ChatSession, Connection, RepoInfo } from '../models/types';

/**
 * Central application state using Angular signals.
 * No NgZone; all state changes trigger CD automatically via signal graph.
 */
@Injectable({ providedIn: 'root' })
export class AppStore {
  private readonly api = inject(ApiService);

  // ── Connections ───────────────────────────────────────────────────────────
  readonly connections = signal<Connection[]>([]);
  readonly selectedConnectionId = signal<string | null>(null);
  readonly selectedConnection = computed(() =>
    this.connections().find(c => c.id === this.selectedConnectionId()) ?? null
  );

  // ── Repos ─────────────────────────────────────────────────────────────────
  readonly repos = signal<RepoInfo[]>([]);
  readonly reposByConnection = signal<Record<string, RepoInfo[]>>({});

  // ── Sessions ──────────────────────────────────────────────────────────────
  readonly sessions = signal<ChatSession[]>([]);
  readonly activeSessionId = signal<string | null>(null);
  readonly activeSession = computed(() =>
    this.sessions().find(s => s.id === this.activeSessionId()) ?? null
  );

  // ── Messages ──────────────────────────────────────────────────────────────
  readonly messages = signal<ChatMessage[]>([]);
  readonly streamingMessageId = signal<string | null>(null);
  readonly streamingContent = signal<string>('');

  // ── Agent status ──────────────────────────────────────────────────────────
  readonly agentStatus = signal<AgentStatusEvent | null>(null);
  readonly isAgentWorking = computed(
    () => this.agentStatus()?.status === 'Running' || this.agentStatus()?.status === 'Queued'
  );

  // ── UI state ──────────────────────────────────────────────────────────────
  readonly isSidebarExpanded = signal(true);
  readonly isAddConnectionOpen = signal(false);
  readonly loadingConnections = signal(false);
  readonly loadingMessages = signal(false);
  readonly sendingMessage = signal(false);

  // ── Actions ───────────────────────────────────────────────────────────────

  loadConnections(): void {
    this.loadingConnections.set(true);
    this.api.getConnections().subscribe({
      next: conns => {
        this.connections.set(conns);
        this.loadingConnections.set(false);
      },
      error: () => this.loadingConnections.set(false)
    });
  }

  loadRepos(connectionId: string): void {
    this.api.getRepos(connectionId).subscribe({
      next: repos => {
        this.repos.set(repos);
        this.reposByConnection.update(r => ({ ...r, [connectionId]: repos }));
      }
    });
  }

  selectConnection(id: string): void {
    this.selectedConnectionId.set(id);
    this.loadRepos(id);
    this.loadSessions(id);
  }

  loadSessions(connectionId: string): void {
    this.api.getSessions(connectionId).subscribe({
      next: sessions => {
        this.sessions.set(sessions);
        if (sessions.length > 0 && !this.activeSessionId()) {
          this.selectSession(sessions[0].id);
        }
      }
    });
  }

  createSession(connectionId: string): void {
    this.api.createSession(connectionId).subscribe({
      next: session => {
        this.sessions.update(s => [session, ...s]);
        this.selectSession(session.id);
      }
    });
  }

  selectSession(id: string): void {
    this.activeSessionId.set(id);
    this.messages.set([]);
    this.streamingContent.set('');
    this.streamingMessageId.set(null);
    this.agentStatus.set(null);
    this.loadMessages(id);
  }

  loadMessages(sessionId: string): void {
    this.loadingMessages.set(true);
    this.api.getMessages(sessionId).subscribe({
      next: msgs => {
        this.messages.set(msgs);
        this.loadingMessages.set(false);
      },
      error: () => this.loadingMessages.set(false)
    });
  }

  // Append a streaming chunk to the in-progress message
  appendChunk(messageId: string, chunk: string, isFinal: boolean): void {
    if (!this.streamingMessageId()) {
      this.streamingMessageId.set(messageId);
    }
    if (!isFinal) {
      this.streamingContent.update(c => c + chunk);
    } else {
      // Finalize: push to messages list and clear streaming state
      const finalContent = this.streamingContent();
      this.messages.update(msgs => [
        ...msgs,
        {
          id: messageId,
          role: 'Assistant',
          content: finalContent,
          createdAt: new Date().toISOString()
        }
      ]);
      this.streamingContent.set('');
      this.streamingMessageId.set(null);
      this.sendingMessage.set(false);
    }
  }

  updateAgentStatus(evt: AgentStatusEvent): void {
    this.agentStatus.set(evt);
    // Auto-clear after success/failure
    if (evt.status === 'Succeeded' || evt.status === 'Failed' || evt.status === 'Cancelled') {
      setTimeout(() => {
        if (this.agentStatus()?.jobId === evt.jobId) {
          // keep visible for 5s then fade
        }
      }, 5000);
    }
  }
}
