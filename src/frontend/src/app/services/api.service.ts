import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import type {
  Connection,
  RepoInfo,
  ChatSession,
  ChatMessage,
  AgentJob,
} from '../models/types';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api';

  // ── Connections ───────────────────────────────────────────────────────────

  getConnections(): Observable<Connection[]> {
    return this.http.get<Connection[]>(`${this.base}/connections`);
  }

  createConnection(payload: {
    displayName: string;
    organizationUrl: string;
    projectName: string;
    pat: string;
  }): Observable<Connection> {
    return this.http.post<Connection>(`${this.base}/connections`, payload);
  }

  deleteConnection(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/connections/${id}`);
  }

  // ── Repos ─────────────────────────────────────────────────────────────────

  getRepos(connectionId: string): Observable<RepoInfo[]> {
    return this.http.get<RepoInfo[]>(
      `${this.base}/connections/${connectionId}/repos`
    );
  }

  cloneRepo(connectionId: string, repoName: string): Observable<RepoInfo> {
    return this.http.post<RepoInfo>(
      `${this.base}/connections/${connectionId}/repos/${encodeURIComponent(repoName)}/clone`,
      {}
    );
  }

  getRepoStatus(
    connectionId: string,
    repoName: string
  ): Observable<{ status: string; error: string | null }> {
    return this.http.get<{ status: string; error: string | null }>(
      `${this.base}/connections/${connectionId}/repos/${encodeURIComponent(repoName)}/status`
    );
  }

  // ── Chat sessions ─────────────────────────────────────────────────────────

  getSessions(connectionId: string): Observable<ChatSession[]> {
    return this.http.get<ChatSession[]>(
      `${this.base}/chat/sessions?connectionId=${connectionId}`
    );
  }

  createSession(connectionId: string, title?: string): Observable<ChatSession> {
    return this.http.post<ChatSession>(`${this.base}/chat/sessions`, {
      connectionId,
      title: title ?? null,
    });
  }

  getMessages(sessionId: string): Observable<ChatMessage[]> {
    return this.http.get<ChatMessage[]>(
      `${this.base}/chat/sessions/${sessionId}/messages`
    );
  }

  /** Sends a message; the response arrives via SignalR stream */
  sendMessage(sessionId: string, content: string): Observable<void> {
    return this.http.post<void>(
      `${this.base}/chat/sessions/${sessionId}/messages`,
      { content }
    );
  }

  // ── Agent jobs ────────────────────────────────────────────────────────────

  getJobs(sessionId: string): Observable<AgentJob[]> {
    return this.http.get<AgentJob[]>(
      `${this.base}/chat/sessions/${sessionId}/jobs`
    );
  }
}
