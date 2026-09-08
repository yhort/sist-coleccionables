import { Component, computed, inject, signal } from '@angular/core';

import { AperturasYieldReporteComponent } from '../../components/aperturas-yield-reporte/aperturas-yield-reporte.component';
import { ArqueoConciliacionComponent } from '../../components/arqueo-conciliacion/arqueo-conciliacion.component';
import { ReportesKpisComponent } from '../../components/reportes-kpis/reportes-kpis.component';
import { ReportesPeriodoBarComponent } from '../../components/reportes-periodo-bar/reportes-periodo-bar.component';
import { VentasCanalFranquiciaComponent } from '../../components/ventas-canal-franquicia/ventas-canal-franquicia.component';
import { ReportesApiService } from '../../data-access/reportes.service';
import { ReportesPeriodo, periodoMesActual } from '../../models/reporte.model';

@Component({
  selector: 'app-reportes-page',
  imports: [
    AperturasYieldReporteComponent,
    ArqueoConciliacionComponent,
    ReportesKpisComponent,
    ReportesPeriodoBarComponent,
    VentasCanalFranquiciaComponent,
  ],
  templateUrl: './reportes-page.component.html',
  styleUrl: './reportes-page.component.scss',
})
export class ReportesPageComponent {
  private readonly reportesApi = inject(ReportesApiService);

  readonly periodo = signal<ReportesPeriodo>(periodoMesActual());

  readonly reporte = computed(() => this.reportesApi.construir(this.periodo()));

  actualizarPeriodo(periodo: ReportesPeriodo): void {
    this.periodo.set(periodo);
  }

  limpiarPeriodo(): void {
    this.periodo.set({ desde: '', hasta: '' });
  }

  exportarCsv(): void {
    this.reportesApi.exportarCsv(this.reporte());
  }
}
