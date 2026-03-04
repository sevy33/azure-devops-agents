import { Component, EventEmitter, inject, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../services/api.service';

@Component({
  selector: 'app-add-connection-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="overlay" (click)="onBackdropClick($event)">
      <div class="dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title">
        <div class="dialog__header">
          <h3 id="dialog-title">Add Azure DevOps Connection</h3>
          <button class="btn-icon" (click)="cancel()">✕</button>
        </div>

        <form class="dialog__body" (ngSubmit)="submit()">
          <div class="field">
            <label for="displayName">Display Name</label>
            <input id="displayName" type="text" [(ngModel)]="displayName" name="displayName"
              placeholder="My ADO Org" required />
          </div>

          <div class="field">
            <label for="orgUrl">Organization URL</label>
            <input id="orgUrl" type="url" [(ngModel)]="organizationUrl" name="organizationUrl"
              placeholder="https://dev.azure.com/yourorg" required />
          </div>

          <div class="field">
            <label for="project">Project Name</label>
            <input id="project" type="text" [(ngModel)]="projectName" name="projectName"
              placeholder="YourProject" required />
          </div>

          <div class="field">
            <label for="pat">Personal Access Token</label>
            <input id="pat" type="password" [(ngModel)]="pat" name="pat"
              placeholder="Paste your PAT here" required />
          </div>

          @if (errorMessage()) {
            <div class="dialog__error">{{ errorMessage() }}</div>
          }

          <div class="dialog__footer">
            <button type="button" class="btn btn--secondary" (click)="cancel()">Cancel</button>
            <button type="submit" class="btn btn--primary" [disabled]="saving()">
              {{ saving() ? 'Saving…' : 'Add Connection' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  `,
  styles: [`
    .overlay {
      position: fixed;
      inset: 0;
      background: rgba(0,0,0,0.6);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 100;
    }

    .dialog {
      background: var(--bg-dialog);
      border: 1px solid var(--border);
      border-radius: 8px;
      width: 420px;
      max-width: 95vw;
      box-shadow: 0 8px 32px rgba(0,0,0,0.4);
    }

    .dialog__header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 1.25rem 1.25rem 0;
    }

    .dialog__header h3 {
      font-size: 1rem;
      font-weight: 600;
      color: var(--text-primary);
    }

    .dialog__body {
      padding: 1.25rem;
      display: flex;
      flex-direction: column;
      gap: 1rem;
    }

    .field {
      display: flex;
      flex-direction: column;
      gap: 0.4rem;
    }

    label {
      font-size: 0.8rem;
      color: var(--text-secondary);
    }

    input {
      background: var(--bg-input);
      border: 1px solid var(--border);
      border-radius: 4px;
      padding: 0.5rem 0.75rem;
      color: var(--text-primary);
      font-size: 0.875rem;
      transition: border-color 0.15s;
    }

    input:focus {
      outline: none;
      border-color: var(--accent);
    }

    .dialog__error {
      color: var(--error);
      font-size: 0.8rem;
      background: var(--error-bg);
      border-radius: 4px;
      padding: 0.5rem 0.75rem;
    }

    .dialog__footer {
      display: flex;
      gap: 0.75rem;
      justify-content: flex-end;
    }

    .btn {
      border: none;
      border-radius: 4px;
      padding: 0.5rem 1.25rem;
      font-size: 0.875rem;
      font-weight: 500;
      cursor: pointer;
      transition: opacity 0.1s;
    }

    .btn:disabled { opacity: 0.5; cursor: not-allowed; }

    .btn--primary { background: var(--accent); color: #fff; }
    .btn--primary:hover:not(:disabled) { opacity: 0.85; }

    .btn--secondary { background: var(--border); color: var(--text-primary); }
    .btn--secondary:hover:not(:disabled) { opacity: 0.8; }

    .btn-icon {
      all: unset;
      cursor: pointer;
      color: var(--text-muted);
      font-size: 0.85rem;
    }
  `]
})
export class AddConnectionDialogComponent {
  @Output() close = new EventEmitter<boolean>();

  private readonly api = inject(ApiService);

  displayName = '';
  organizationUrl = '';
  projectName = '';
  pat = '';
  saving = signal(false);
  errorMessage = signal('');

  submit(): void {
    if (!this.displayName || !this.organizationUrl || !this.projectName || !this.pat) return;
    this.saving.set(true);
    this.errorMessage.set('');

    this.api.createConnection({
      displayName: this.displayName,
      organizationUrl: this.organizationUrl,
      projectName: this.projectName,
      pat: this.pat
    }).subscribe({
      next: () => { this.saving.set(false); this.close.emit(true); },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Failed to create connection.');
      }
    });
  }

  cancel(): void {
    this.close.emit(false);
  }

  onBackdropClick(e: MouseEvent): void {
    if ((e.target as HTMLElement).classList.contains('overlay')) {
      this.cancel();
    }
  }
}
