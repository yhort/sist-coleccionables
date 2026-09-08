import { Component, inject, signal } from '@angular/core';

import { EmitirCpeDialogComponent } from '../../components/emitir-cpe-dialog/emitir-cpe-dialog.component';
import { FiscalSunatPanelComponent } from '../../components/fiscal-sunat-panel/fiscal-sunat-panel.component';
import { IzipayConfigPanelComponent } from '../../components/izipay-config-panel/izipay-config-panel.component';
import { SedesPanelComponent } from '../../components/sedes-panel/sedes-panel.component';
import { UsuariosPanelComponent } from '../../components/usuarios-panel/usuarios-panel.component';
import { WebhooksPanelComponent } from '../../components/webhooks-panel/webhooks-panel.component';
import { EcosistemaApiService } from '../../data-access/ecosistema.service';

type TabEcosistema = 'fiscal' | 'sedes' | 'usuarios' | 'integraciones';

@Component({
  selector: 'app-ecosistema-page',
  imports: [
    EmitirCpeDialogComponent,
    FiscalSunatPanelComponent,
    IzipayConfigPanelComponent,
    SedesPanelComponent,
    UsuariosPanelComponent,
    WebhooksPanelComponent,
  ],
  templateUrl: './ecosistema-page.component.html',
  styleUrl: './ecosistema-page.component.scss',
})
export class EcosistemaPageComponent {
  readonly api = inject(EcosistemaApiService);
  readonly tab = signal<TabEcosistema>('fiscal');
  readonly dialogAbierto = signal(false);
  readonly error = signal('');

  constructor() {
    void this.api.cargar().catch((err) => {
      this.error.set(err instanceof Error ? err.message : 'No se pudo cargar Ecosistema.');
    });
  }

  abrirSimulacion(): void {
    this.dialogAbierto.set(true);
  }

  cerrarSimulacion(): void {
    this.dialogAbierto.set(false);
  }
}
