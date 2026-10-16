import { Component, computed, inject, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { AuthSessionService } from '../../core/auth/auth-session.service';
import { NavIconComponent } from '../nav-icon/nav-icon.component';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, NavIconComponent],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
})
export class SidebarComponent {
  private readonly auth = inject(AuthSessionService);
  readonly routeSelected = output<void>();
  readonly menuItems = computed(() => this.auth.menuItemsPermitidos());

  onRouteSelected(): void {
    this.routeSelected.emit();
  }
}
