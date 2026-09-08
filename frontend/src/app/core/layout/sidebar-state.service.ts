import { Injectable, signal } from '@angular/core';

const STORAGE_KEY = 'trunqi.sidebar.collapsed';

@Injectable({ providedIn: 'root' })
export class SidebarStateService {
  readonly collapsed = signal(this.readStoredCollapsed());

  toggle(): void {
    this.collapsed.update((collapsed) => {
      const next = !collapsed;
      this.persist(next);
      return next;
    });
  }

  private readStoredCollapsed(): boolean {
    try {
      return globalThis.localStorage?.getItem(STORAGE_KEY) === 'true';
    } catch {
      return false;
    }
  }

  private persist(collapsed: boolean): void {
    try {
      globalThis.localStorage?.setItem(STORAGE_KEY, String(collapsed));
    } catch {
      // ignore quota / private mode
    }
  }
}
