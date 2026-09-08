import { Component, computed, input, output, signal } from '@angular/core';
import { Router } from '@angular/router';

import { BreadcrumbComponent, BreadcrumbItem } from '../breadcrumb/breadcrumb.component';
import { APP_NAV_ITEMS, AppNavItem } from '../navigation';
import { NavIconComponent } from '../nav-icon/nav-icon.component';

@Component({
  selector: 'app-topbar',
  imports: [BreadcrumbComponent, NavIconComponent],
  templateUrl: './topbar.component.html',
  styleUrl: './topbar.component.scss',
})
export class TopbarComponent {
  private readonly router: Router;

  readonly pageTitle = input('Dashboard');
  readonly breadcrumbItems = input<readonly BreadcrumbItem[]>([]);
  readonly userName = input('Equipo Trunqi');
  readonly sedeLabel = input('Sede principal');
  readonly menuExpanded = input(false);
  readonly menuControls = input('main-sidebar');

  readonly menuToggle = output<void>();
  readonly logout = output<void>();

  readonly searchQuery = signal('');
  readonly searchOpen = signal(false);

  readonly searchResults = computed(() => {
    const query = this.searchQuery().trim().toLowerCase();
    if (!query) {
      return APP_NAV_ITEMS;
    }

    return APP_NAV_ITEMS.filter((item) => this.matchesQuery(item, query));
  });

  constructor(router: Router) {
    this.router = router;
  }

  onMenuToggle(): void {
    this.menuToggle.emit();
  }

  onSearchInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.searchQuery.set(value);
    this.searchOpen.set(true);
  }

  onSearchFocus(): void {
    this.searchOpen.set(true);
  }

  onSearchBlur(): void {
    globalThis.setTimeout(() => this.searchOpen.set(false), 120);
  }

  goToModule(item: AppNavItem): void {
    this.searchQuery.set('');
    this.searchOpen.set(false);
    void this.router.navigateByUrl(item.route);
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.searchOpen.set(false);
      (event.target as HTMLInputElement).blur();
      return;
    }

    if (event.key === 'Enter') {
      const first = this.searchResults()[0];
      if (first) {
        this.goToModule(first);
      }
    }
  }

  private matchesQuery(item: AppNavItem, query: string): boolean {
    const haystack = [item.label, ...item.keywords].join(' ').toLowerCase();
    return haystack.includes(query);
  }
}
