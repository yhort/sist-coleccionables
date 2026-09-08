import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ReportesPeriodo } from '../../models/reporte.model';

@Component({
  selector: 'app-reportes-periodo-bar',
  imports: [FormsModule],
  templateUrl: './reportes-periodo-bar.component.html',
  styleUrl: './reportes-periodo-bar.component.scss',
})
export class ReportesPeriodoBarComponent {
  readonly periodo = input.required<ReportesPeriodo>();
  readonly changed = output<ReportesPeriodo>();
  readonly cleared = output<void>();
  readonly exportar = output<void>();

  actualizar(clave: keyof ReportesPeriodo, valor: string): void {
    this.changed.emit({ ...this.periodo(), [clave]: valor });
  }
}
