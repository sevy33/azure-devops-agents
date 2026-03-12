import {
  AfterViewChecked,
  Component,
  computed,
  ElementRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
  ViewChild
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { marked } from 'marked';
import { AppStore } from '../store/app.store';
import { SignalRService } from '../services/signalr.service';
import { ApiService } from '../services/api.service';
import { AgentStatusComponent } from '../agent-status/agent-status.component';
import type { MessageChunkEvent, AgentStatusEvent } from '../models/types';

// Configure marked for safe, GitHub-flavored markdown
marked.setOptions({ async: false, gfm: true, breaks: true });

function renderMd(content: string): string {
  return marked(content) as string;
}

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [FormsModule, AgentStatusComponent],
  template: `
    <div class="chat">
      <!-- Agent status bar (hidden unless agent is active) -->
      <app-agent-status />

      <!-- Message thread -->
      <div class="chat__messages" #scrollContainer>
        @for (msg of store.messages(); track msg.id) {
          <div class="chat__msg" [attr.data-role]="msg.role.toLowerCase()">
            <div class="chat__msg-avatar">
              {{ msg.role === 'User' ? '👤' : '🤖' }}
            </div>
            <div class="chat__msg-bubble">
              @if (msg.role === 'Assistant') {
                <div class="chat__md" [innerHTML]="safeHtml(msg.content)"></div>
              } @else {
                <div class="chat__user-text">{{ msg.content }}</div>
              }
            </div>
          </div>
        }

        <!-- Streaming message in progress -->
        @if (store.streamingMessageId()) {
          <div class="chat__msg" data-role="assistant">
            <div class="chat__msg-avatar">🤖</div>
            <div class="chat__msg-bubble">
              <div class="chat__md" [innerHTML]="safeHtml(store.streamingContent())"></div>
              <span class="chat__cursor"></span>
            </div>
          </div>
        }

        <!-- Initial loading -->
        @if (store.loadingMessages()) {
          <div class="chat__loading">Loading messages…</div>
        }

        <!-- Empty state -->
        @if (!store.loadingMessages() && store.messages().length === 0 && !store.streamingMessageId()) {
          <div class="chat__empty">
            <span>Start by typing a message below.</span>
          </div>
        }
      </div>

      <!-- Input bar -->
      <div class="chat__input-bar">
        <textarea
          #inputEl
          class="chat__textarea"
          rows="1"
          placeholder="Ask the assistant… (Enter to send, Shift+Enter for newline)"
          [ngModel]="inputText()" (ngModelChange)="inputText.set($event)"
          [disabled]="isSending()"
          (keydown)="onKeyDown($event)"
          (input)="autoResize()"
        ></textarea>
        <button
          class="chat__send-btn"
          [disabled]="!canSend()"
          (click)="sendMessage()"
          title="Send (Enter)"
        >
          {{ isSending() ? '…' : '↑' }}
        </button>
      </div>
    </div>
  `,
  styles: [`
    :host {
      display: flex;
      flex: 1;
      min-height: 0;
    }

    .chat {
      display: flex;
      flex-direction: column;
      flex: 1;
      min-height: 0;
      overflow: hidden;
    }

    /* ── Messages ─────────────────────────────────────────────── */
    .chat__messages {
      flex: 1;
      min-height: 0;
      overflow-y: auto;
      padding: 1.5rem 1rem;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .chat__msg {
      display: flex;
      gap: 0.75rem;
      align-items: flex-start;
      max-width: 800px;
    }

    .chat__msg[data-role='user'] {
      flex-direction: row-reverse;
      align-self: flex-end;
    }

    .chat__msg-avatar {
      font-size: 1.1rem;
      flex-shrink: 0;
      margin-top: 2px;
    }

    .chat__msg-bubble {
      position: relative;
      padding: 0.75rem 1rem;
      border-radius: 8px;
      max-width: 70%;
      line-height: 1.6;
      font-size: 0.9rem;
    }

    .chat__msg[data-role='assistant'] .chat__msg-bubble {
      background: var(--bg-bubble-ai);
      color: var(--text-primary);
    }

    .chat__msg[data-role='user'] .chat__msg-bubble {
      background: var(--accent);
      color: #fff;
    }

    /* Markdown styles */
    .chat__md :global(p)        { margin: 0 0 0.5em; }
    .chat__md :global(p:last-child) { margin: 0; }
    .chat__md :global(pre)      { background: var(--code-bg); border-radius: 6px; padding: 0.75rem; overflow-x: auto; margin: 0.5em 0; font-size: 0.8rem; }
    .chat__md :global(code)     { font-family: 'Fira Code', monospace; font-size: 0.85em; background: var(--code-bg); border-radius: 3px; padding: 0.1em 0.35em; }
    .chat__md :global(pre code) { background: none; padding: 0; }
    .chat__md :global(ul), .chat__md :global(ol) { padding-left: 1.5em; margin: 0.4em 0; }
    .chat__md :global(li) { margin-bottom: 0.2em; }
    .chat__md :global(a)  { color: var(--accent); }
    .chat__md :global(h1), .chat__md :global(h2), .chat__md :global(h3) { margin: 0.6em 0 0.3em; font-weight: 600; }
    .chat__md :global(blockquote) { border-left: 3px solid var(--border); margin: 0.5em 0; padding: 0.25em 1em; color: var(--text-muted); }
    .chat__md :global(table) { border-collapse: collapse; margin: 0.5em 0; width: 100%; }
    .chat__md :global(th), .chat__md :global(td) { border: 1px solid var(--border); padding: 0.4em 0.75em; text-align: left; }
    .chat__md :global(th) { background: var(--bg-sidebar); }

    .chat__user-text { white-space: pre-wrap; word-break: break-word; }

    /* Blinking cursor for streaming */
    .chat__cursor {
      display: inline-block;
      width: 2px;
      height: 1em;
      background: var(--accent);
      margin-left: 2px;
      vertical-align: text-bottom;
      animation: blink 0.9s step-end infinite;
    }
    @keyframes blink { 50% { opacity: 0; } }

    .chat__loading, .chat__empty {
      text-align: center;
      color: var(--text-muted);
      font-size: 0.85rem;
      padding: 2rem;
    }

    /* ── Input bar ────────────────────────────────────────────── */
    .chat__input-bar {
      display: flex;
      align-items: flex-end;
      gap: 0.5rem;
      padding: 0.75rem 1rem;
      border-top: 1px solid var(--border);
      background: var(--bg-base);
    }

    .chat__textarea {
      flex: 1;
      background: var(--bg-input);
      border: 1px solid var(--border);
      border-radius: 8px;
      padding: 0.6rem 0.875rem;
      color: var(--text-primary);
      font-size: 0.9rem;
      resize: none;
      overflow-y: hidden;
      max-height: 200px;
      line-height: 1.5;
      transition: border-color 0.15s;
    }

    .chat__textarea:focus {
      outline: none;
      border-color: var(--accent);
    }

    .chat__textarea:disabled { opacity: 0.5; }

    .chat__send-btn {
      background: var(--accent);
      color: #fff;
      border: none;
      border-radius: 8px;
      width: 40px;
      height: 40px;
      font-size: 1.1rem;
      cursor: pointer;
      flex-shrink: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: opacity 0.1s;
    }

    .chat__send-btn:disabled { opacity: 0.4; cursor: not-allowed; }
    .chat__send-btn:hover:not(:disabled) { opacity: 0.85; }
  `]
})
export class ChatComponent implements OnInit, OnDestroy, AfterViewChecked {
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef<HTMLDivElement>;
  @ViewChild('inputEl') private inputEl!: ElementRef<HTMLTextAreaElement>;

  protected readonly store = inject(AppStore);
  private readonly signalR = inject(SignalRService);
  private readonly api = inject(ApiService);
  private readonly sanitizer = inject(DomSanitizer);

  inputText = signal('');
  private shouldScrollToBottom = false;

  isSending = this.store.sendingMessage;
  canSend = computed(() => this.inputText().trim().length > 0 && !this.isSending());

  private markdownCache = new Map<string, SafeHtml>();

  safeHtml(content: string): SafeHtml {
    if (!content) return '';
    if (this.markdownCache.has(content)) return this.markdownCache.get(content)!;
    const html = renderMd(content);
    const safe = this.sanitizer.bypassSecurityTrustHtml(html);
    this.markdownCache.set(content, safe);
    return safe;
  }

  ngOnInit(): void {
    const sessionId = this.store.activeSessionId();
    if (!sessionId) return;

    this.setupSignalR(sessionId);
  }

  private async setupSignalR(sessionId: string): Promise<void> {
    if (this.signalR.status() === 'disconnected') {
      await this.signalR.connect();
    }
    await this.signalR.joinSession(sessionId);

    this.signalR.onMessage((evt: MessageChunkEvent) => {
      if (evt.sessionId !== this.store.activeSessionId()) return;
      this.store.appendChunk(evt.messageId, evt.chunk, evt.isFinal);
      this.shouldScrollToBottom = true;
    });

    this.signalR.onStatus((evt: AgentStatusEvent) => {
      if (evt.sessionId !== this.store.activeSessionId()) return;
      this.store.updateAgentStatus(evt);
    });
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) {
      this.scrollToBottom();
      this.shouldScrollToBottom = false;
    }
  }

  ngOnDestroy(): void {
    this.signalR.leaveSession().catch(() => {});
    this.markdownCache.clear();
  }

  async sendMessage(): Promise<void> {
    const content = this.inputText().trim();
    if (!content) return;
    const sessionId = this.store.activeSessionId();
    if (!sessionId) return;

    // Optimistically add user message
    this.store.messages.update(msgs => [
      ...msgs,
      {
        id: crypto.randomUUID(),
        role: 'User' as const,
        content,
        createdAt: new Date().toISOString()
      }
    ]);

    this.inputText.set('');
    this.isSending.set(true);
    this.shouldScrollToBottom = true;
    this.resetTextarea();

    // Ensure we're joined to the active session group before sending.
    await this.signalR.connect();
    const joined = await this.signalR.joinSession(sessionId);
    if (!joined) {
      this.isSending.set(false);
      return;
    }

    this.api.sendMessage(sessionId, content).subscribe({
      error: () => {
        this.isSending.set(false);
      }
      // Success: response arrives via SignalR chunks
    });
  }

  onKeyDown(e: KeyboardEvent): void {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      if (this.canSend()) this.sendMessage();
    }
  }

  autoResize(): void {
    const el = this.inputEl?.nativeElement;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = Math.min(el.scrollHeight, 200) + 'px';
  }

  private resetTextarea(): void {
    const el = this.inputEl?.nativeElement;
    if (el) { el.style.height = 'auto'; }
  }

  private scrollToBottom(): void {
    const el = this.scrollContainer?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  }
}
