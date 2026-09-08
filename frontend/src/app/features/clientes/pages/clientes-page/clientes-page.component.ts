import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { ClienteFormDialogComponent } from '../../components/cliente-form-dialog/cliente-form-dialog.component';
import { ClientesApiService } from '../../data-access/clientes.service';
import { Cliente, etiquetaDocumento } from '../../models/cliente.model';
import { etiquetaCanalContacto } from '../../../../shared/models/contacto-entrega.model';

@Component({
  selector: 'app-clientes-page',
  imports: [FormsModule, ClienteFormDialogComponent],
  templateUrl: './clientes-page.component.html',
  styleUrl: './clientes-page.component.scss',
})
export class ClientesPageComponent {
  private readonly api = inject(ClientesApiService);

  readonly busqueda = signal('');
  readonly error = signal('');
  readonly dialogAbierto = signal(false);
  readonly enCurso = signal<Cliente | null>(null);
  readonly clientes = this.api.clientes;
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

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.error.set('');
    try {
      await this.api.refrescar(this.busqueda());
    } catch (err) {
      this.error.set(readApiError(err));
    }
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
}
