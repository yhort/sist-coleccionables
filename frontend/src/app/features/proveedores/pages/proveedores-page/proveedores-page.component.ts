import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { ProveedorFormDialogComponent } from '../../components/proveedor-form-dialog/proveedor-form-dialog.component';
import { ProveedoresApiService } from '../../data-access/proveedores.service';
import {
  ETIQUETAS_FILTRO_ACTIVO,
  FILTROS_ACTIVO_MAESTRO,
  FiltroActivoMaestro,
  Proveedor,
} from '../../models/proveedor.model';

const PAGE_SIZE = 40;

@Component({
  selector: 'app-proveedores-page',
  imports: [FormsModule, ProveedorFormDialogComponent],
  templateUrl: './proveedores-page.component.html',
  styleUrl: './proveedores-page.component.scss',
})
export class ProveedoresPageComponent {
  private readonly api = inject(ProveedoresApiService);

  readonly busqueda = signal('');
  readonly filtroActivo = signal<FiltroActivoMaestro>('activos');
  readonly error = signal('');
  readonly dialogAbierto = signal(false);
  readonly enCurso = signal<Proveedor | null>(null);
  readonly accionId = signal<string | null>(null);
  readonly page = signal(1);
  readonly proveedores = this.api.proveedores;
  readonly filtrosActivo = FILTROS_ACTIVO_MAESTRO;
  readonly etiquetasFiltro = ETIQUETAS_FILTRO_ACTIVO;

  readonly filtrados = computed(() => {
    const q = this.busqueda().trim().toLowerCase();
    return this.proveedores().filter(
      (item) =>
        !q ||
        item.razonSocial.toLowerCase().includes(q) ||
        (item.nombreComercial ?? '').toLowerCase().includes(q) ||
        item.ruc.includes(q),
    );
  });

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filtrados().length / PAGE_SIZE)),
  );

  readonly paginaActual = computed(() => Math.min(this.page(), this.totalPages()));

  readonly pageItems = computed(() => {
    const pagina = this.paginaActual();
    const inicio = (pagina - 1) * PAGE_SIZE;
    return this.filtrados().slice(inicio, inicio + PAGE_SIZE);
  });

  readonly rango = computed(() => {
    const total = this.filtrados().length;
    if (total === 0) {
      return { desde: 0, hasta: 0, total: 0 };
    }
    const pagina = this.paginaActual();
    const desde = (pagina - 1) * PAGE_SIZE + 1;
    const hasta = Math.min(pagina * PAGE_SIZE, total);
    return { desde, hasta, total };
  });

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.error.set('');
    this.page.set(1);
    try {
      await this.api.refrescar(this.busqueda(), this.filtroActivo());
    } catch (err) {
      this.error.set(readApiError(err));
    }
  }

  setFiltroActivo(filtro: FiltroActivoMaestro): void {
    if (this.filtroActivo() === filtro) {
      return;
    }
    this.filtroActivo.set(filtro);
    void this.cargar();
  }

  abrir(proveedor: Proveedor | null = null): void {
    this.enCurso.set(proveedor);
    this.dialogAbierto.set(true);
  }

  cerrar(): void {
    this.dialogAbierto.set(false);
    this.enCurso.set(null);
    void this.cargar();
  }

  async desactivar(proveedor: Proveedor): Promise<void> {
    const ok = window.confirm(
      `¿Desactivar a «${proveedor.razonSocial}»?\nSeguirá visible en el historial de compras y kardex, pero no en listados activos.`,
    );
    if (!ok) {
      return;
    }
    this.error.set('');
    this.accionId.set(proveedor.id);
    try {
      await this.api.desactivar(proveedor.id);
      await this.cargar();
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.accionId.set(null);
    }
  }

  async activar(proveedor: Proveedor): Promise<void> {
    this.error.set('');
    this.accionId.set(proveedor.id);
    try {
      await this.api.reactivar(proveedor.id);
      await this.cargar();
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.accionId.set(null);
    }
  }

  paginaAnterior(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  paginaSiguiente(): void {
    this.page.update((p) => Math.min(this.totalPages(), p + 1));
  }
}
