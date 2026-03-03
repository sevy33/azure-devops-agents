import {
  Injectable,
  OnDestroy,
  signal,
  inject,
} from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr';
import type {
  AgentLogEvent,
  AgentStatusEvent,
  MessageChunkEvent,
} from '../models/types';

export type SignalRStatus = 'disconnected' | 'connecting' | 'connected' | 'error';

@Injectable({ providedIn: 'root' })
export class SignalRService implements OnDestroy {
  readonly status = signal<SignalRStatus>('disconnected');

  private connection: HubConnection | null = null;
  private currentSessionId: string | null = null;

  // Callbacks registered by consumers
  private onMessageChunk: ((evt: MessageChunkEvent) => void) | null = null;
  private onAgentStatus: ((evt: AgentStatusEvent) => void) | null = null;
  private onAgentLog: ((evt: AgentLogEvent) => void) | null = null;

  async connect(): Promise<void> {
    if (this.connection) return;

    this.status.set('connecting');

    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/agent')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('MessageChunk', (evt: MessageChunkEvent) =>
      this.onMessageChunk?.(evt)
    );
    this.connection.on('AgentStatus', (evt: AgentStatusEvent) =>
      this.onAgentStatus?.(evt)
    );
    this.connection.on('AgentLog', (evt: AgentLogEvent) =>
      this.onAgentLog?.(evt)
    );

    this.connection.onreconnecting(() => this.status.set('connecting'));
    this.connection.onreconnected(() => this.status.set('connected'));
    this.connection.onclose(() => this.status.set('disconnected'));

    try {
      await this.connection.start();
      this.status.set('connected');
    } catch {
      this.status.set('error');
    }
  }

  async joinSession(sessionId: string): Promise<void> {
    if (this.currentSessionId === sessionId) return;

    if (this.currentSessionId) {
      await this.connection?.invoke('LeaveSession', this.currentSessionId).catch(() => {});
    }

    this.currentSessionId = sessionId;
    await this.connection?.invoke('JoinSession', sessionId).catch(() => {});
  }

  async leaveSession(): Promise<void> {
    if (!this.currentSessionId) return;
    await this.connection?.invoke('LeaveSession', this.currentSessionId).catch(() => {});
    this.currentSessionId = null;
  }

  onMessage(cb: (evt: MessageChunkEvent) => void): void {
    this.onMessageChunk = cb;
  }

  onStatus(cb: (evt: AgentStatusEvent) => void): void {
    this.onAgentStatus = cb;
  }

  onLog(cb: (evt: AgentLogEvent) => void): void {
    this.onAgentLog = cb;
  }

  async disconnect(): Promise<void> {
    if (!this.connection) return;
    await this.connection.stop();
    this.connection = null;
    this.status.set('disconnected');
  }

  ngOnDestroy(): void {
    this.disconnect();
  }
}
