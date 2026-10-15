import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';

import { TenantFeatureName } from './tenant-settings.model';
import { TenantService } from './tenant.service';

@Directive({
  selector: '[hasFeature]',
})
export class HasFeatureDirective {
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly tenant = inject(TenantService);
  private visible = false;

  readonly hasFeature = input.required<TenantFeatureName>();

  constructor() {
    effect(() => {
      const feature = this.hasFeature();
      // Re-evaluate when settings change.
      this.tenant.settings();
      const allowed = this.tenant.hasFeature(feature);
      if (allowed && !this.visible) {
        this.viewContainer.createEmbeddedView(this.templateRef);
        this.visible = true;
      } else if (!allowed && this.visible) {
        this.viewContainer.clear();
        this.visible = false;
      }
    });
  }
}
