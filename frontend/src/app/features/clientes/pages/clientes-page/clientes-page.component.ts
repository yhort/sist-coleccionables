import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { ClienteFormDialogComponent } from '../../components/cliente-form-dialog/cliente-form-dialog.component';
import { ClientesApiService } from '../../data-access/clientes.service';
import {
  Cliente,
  ETIQUETAS_FILTRO_ACTIVO,
  FILTROS_ACTIVO_MAESTRO,
  FiltroActivoMaestro,
  etiquetaDocumento,
} from '../../models/cliente.model';
import { etiquetaCanalContacto } from '../../../../shared/models/contacto-entrega.model';

const PAGE_SIZE = 40;

@Component({
  selector: 'app-clientes-page',
  imports: [FormsModule, ClienteFormDialogComponent],
  templateUrl: './clientes-page.component.html',
  styleUrl: './clientes-page.component.scss',
})
export class ClientesPageComponent {
  private readonly api = inject(ClientesApiService);

  readonly busqueda = signal('');
  readonly filtroActivo = signal<FiltroActivoMaestro>('activos');
  readonly error = signal('');
  readonly dialogAbierto = signal(false);
  readonly enCurso = signal<Cliente | null>(null);
  readonly accionId = signal<string | null>(null);
  readonly page = signal(1);
  readonly clientes = this.api.clientes;
  readonly filtrosActivo = FILTROS_ACTIVO_MAESTRO;
  readonly etiquetasFiltro = ETIQUETAS_FILTRO_ACTIVO;
  readonly etiquetaDocumento = etiquetaDocumento;
  readonly etiquetaCanal = etiquetaCanalContacto;

  readonly filtrados = computed(() => {
    const q = this.busqueda().trim().toLowerCase();
    const items = this.clientes();
    if (!q) {
      return items;
    }
    return items.filter(
      (cliente) =>
        cliente.nombre.toLowerCase().includes(q) ||
        (cliente.numeroDocumento ?? '').includes(q) ||
        (cliente.telefono ?? '').includes(q),
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

  abrir(cliente: Cliente | null = null): void {
    this.enCurso.set(cliente);
    this.dialogAbierto.set(true);
  }

  onGuardado(_cliente: Cliente): void {
    this.dialogAbierto.set(false);
    this.enCurso.set(null);
    void this.cargar();
  }

  cerrar(): void {
    this.dialogAbierto.set(false);
    this.enCurso.set(null);
  }

  async desactivar(cliente: Cliente): Promise<void> {
    if (cliente.esPublicoGeneral) {
      this.error.set('No se puede desactivar el cliente varios / público general.');
      return;
    }
    const ok = window.confirm(
      `¿Desactivar a «${cliente.nombre}»?\nSeguirá visible en el historial de pedidos y pagos, pero no en listados activos.`,
    );
    if (!ok) {
      return;
    }
    this.error.set('');
    this.accionId.set(cliente.id);
    try {
      await this.api.desactivar(cliente.id);
      await this.cargar();
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.accionId.set(null);
    }
  }

  async activar(cliente: Cliente): Promise<void> {
    this.error.set('');
    this.accionId.set(cliente.id);
    try {
      await this.api.reactivar(cliente.id);
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
