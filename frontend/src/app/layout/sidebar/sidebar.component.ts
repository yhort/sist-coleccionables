import { Component, computed, inject, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

import { TenantService } from '../../core/tenant/tenant.service';
import { APP_NAV_ITEMS } from '../navigation';
import { NavIconComponent } from '../nav-icon/nav-icon.component';

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, NavIconComponent],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
})
export class SidebarComponent {
  private readonly tenant = inject(TenantService);

  readonly routeSelected = output<void>();
  readonly menuItems = computed(() => {
    // Depend on settings signal so the menu re-filters after login/refresh.
    this.tenant.settings();
    return APP_NAV_ITEMS.filter((item) => {
      if (item.featuresAny?.length) {
        return item.featuresAny.some((feature) => this.tenant.hasFeature(feature));
      }
      return !item.feature || this.tenant.hasFeature(item.feature);
    });
  });
  readonly brandName = this.tenant.nombreComercial;

  onRouteSelected(): void {
    this.routeSelected.emit();
  }
}
