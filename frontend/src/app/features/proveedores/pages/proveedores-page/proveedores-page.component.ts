import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import { ProveedorFormDialogComponent } from '../../components/proveedor-form-dialog/proveedor-form-dialog.component';
import { ProveedoresApiService } from '../../data-access/proveedores.service';
import { Proveedor } from '../../models/proveedor.model';

@Component({
  selector: 'app-proveedores-page',
  imports: [FormsModule, ProveedorFormDialogComponent],
  templateUrl: './proveedores-page.component.html',
  styleUrl: './proveedores-page.component.scss',
})
export class ProveedoresPageComponent {
  private readonly api = inject(ProveedoresApiService);

  readonly busqueda = signal('');
  readonly error = signal('');
  readonly dialogAbierto = signal(false);
  readonly enCurso = signal<Proveedor | null>(null);
  readonly proveedores = this.api.proveedores;

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

  constructor() {
    void this.cargar();
  }

  async cargar(): Promise<void> {
    this.error.set('');
    try {
      await this.api.refrescar(this.busqueda(), false);
    } catch (err) {
      this.error.set(readApiError(err));
    }
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
}
