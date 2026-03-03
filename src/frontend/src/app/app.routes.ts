import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./shell/shell.component').then(m => m.ShellComponent)
  },
  {
    path: 'auth/success',
    loadComponent: () =>
      import('./auth-callback/auth-callback.component').then(m => m.AuthCallbackComponent)
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
