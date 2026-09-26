import { Component, inject, signal } from '@angular/core';

import { readApiError } from '../../../../core/http/api-error';
import { EcosistemaApiService } from '../../data-access/ecosistema.service';
import { ETIQUETAS_TIPO_SEDE, SedeEmpresa } from '../../models/ecosistema.model';
import { SedeFormDialogComponent } from '../sede-form-dialog/sede-form-dialog.component';

@Component({
  selector: 'app-sedes-panel',
  imports: [SedeFormDialogComponent],
  templateUrl: './sedes-panel.component.html',
  styleUrl: './sedes-panel.component.scss',
})
export class SedesPanelComponent {
  readonly api = inject(EcosistemaApiService);
  readonly etiquetasTipo = ETIQUETAS_TIPO_SEDE;
  readonly dialogAbierto = signal(false);
  readonly sedeEnCurso = signal<SedeEmpresa | null>(null);
  readonly error = signal('');
  readonly mensaje = signal('');
  readonly accionId = signal<string | null>(null);

  abrir(sede: SedeEmpresa | null = null): void {
    this.error.set('');
    this.mensaje.set('');
    this.sedeEnCurso.set(sede);
    this.dialogAbierto.set(true);
  }

  cerrar(): void {
    this.dialogAbierto.set(false);
    this.sedeEnCurso.set(null);
  }

  onGuardado(): void {
    this.cerrar();
    this.mensaje.set('Sede/almacén guardado correctamente.');
  }

  async eliminarODesactivar(sede: SedeEmpresa): Promise<void> {
    const etiqueta = sede.tipo === 'ALMACEN' ? 'almacén' : 'sede';
    const aviso = sede.tieneDependencias
      ? `«${sede.nombre}» tiene historial (stock, movimientos, ventas o cajas).\n\nSe DESACTIVARÁ (soft delete) para conservar la trazabilidad. ¿Continuar?`
      : `«${sede.nombre}» no tiene dependencias.\n\nSe ELIMINARÁ de forma permanente. ¿Continuar?`;
    if (!window.confirm(aviso)) {
      return;
    }

    this.error.set('');
    this.mensaje.set('');
    this.accionId.set(sede.id);
    try {
      const resultado = await this.api.eliminarODesactivarSede(sede.id);
      this.mensaje.set(
        resultado.motivo ??
          (resultado.accion === 'DESACTIVADA'
            ? `El ${etiqueta} se desactivó porque tiene historial asociado.`
            : `El ${etiqueta} se eliminó permanentemente.`),
      );
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.accionId.set(null);
    }
  }

  async reactivar(sede: SedeEmpresa): Promise<void> {
    this.error.set('');
    this.mensaje.set('');
    this.accionId.set(sede.id);
    try {
      await this.api.reactivarSede(sede.id);
      this.mensaje.set(`«${sede.nombre}» reactivada. Volverá a aparecer en los selectores.`);
    } catch (err) {
      this.error.set(readApiError(err));
    } finally {
      this.accionId.set(null);
    }
  }
}
