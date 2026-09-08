import { Component, input } from '@angular/core';

import { NavIconName } from '../navigation';

@Component({
  selector: 'app-nav-icon',
  templateUrl: './nav-icon.component.html',
  styleUrl: './nav-icon.component.scss',
})
export class NavIconComponent {
  readonly name = input.required<NavIconName>();
}
