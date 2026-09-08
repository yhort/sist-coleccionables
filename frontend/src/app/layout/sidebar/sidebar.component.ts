import { Component, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { APP_NAV_ITEMS } from '../navigation';
import { NavIconComponent } from '../nav-icon/nav-icon.component';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, NavIconComponent],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
})
export class SidebarComponent {
  readonly routeSelected = output<void>();
  readonly menuItems = APP_NAV_ITEMS;

  onRouteSelected(): void {
    this.routeSelected.emit();
  }
}
