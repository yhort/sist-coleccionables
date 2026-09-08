import { Component, inject, signal } from '@angular/core';

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

  abrir(sede: SedeEmpresa | null = null): void {
    this.sedeEnCurso.set(sede);
    this.dialogAbierto.set(true);
  }

  cerrar(): void {
    this.dialogAbierto.set(false);
    this.sedeEnCurso.set(null);
  }
}
