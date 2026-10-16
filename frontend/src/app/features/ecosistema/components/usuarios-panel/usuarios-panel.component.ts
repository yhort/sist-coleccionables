import { Component, inject, signal } from '@angular/core';

import { AuthSessionService } from '../../../../core/auth/auth-session.service';
import { EcosistemaApiService } from '../../data-access/ecosistema.service';
import {
  ETIQUETAS_MODULO,
  ETIQUETAS_ROL,
  MODULOS_PERMISO,
  ROLES_USUARIO,
  UsuarioEmpresa,
} from '../../models/ecosistema.model';
import { UsuarioFormDialogComponent } from '../usuario-form-dialog/usuario-form-dialog.component';

@Component({
  selector: 'app-usuarios-panel',
  imports: [UsuarioFormDialogComponent],
  templateUrl: './usuarios-panel.component.html',
  styleUrl: './usuarios-panel.component.scss',
})
export class UsuariosPanelComponent {
  private readonly auth = inject(AuthSessionService);
  readonly api = inject(EcosistemaApiService);
  readonly isAdmin = this.auth.isAdmin;
  readonly roles = ROLES_USUARIO;
  readonly modulos = MODULOS_PERMISO;
  readonly etiquetasRol = ETIQUETAS_ROL;
  readonly etiquetasModulo = ETIQUETAS_MODULO;
  readonly dialogAbierto = signal(false);
  readonly usuarioEnCurso = signal<UsuarioEmpresa | null>(null);

  abrir(usuario: UsuarioEmpresa | null = null): void {
    if (!this.isAdmin()) {
      return;
    }
    this.usuarioEnCurso.set(usuario);
    this.dialogAbierto.set(true);
  }

  async cerrar(): Promise<void> {
    this.dialogAbierto.set(false);
    this.usuarioEnCurso.set(null);
    await this.api.refrescarUsuarios().catch(() => undefined);
  }

  permisoDe(rol: UsuarioEmpresa['rol'], modulo: (typeof MODULOS_PERMISO)[number]) {
    return this.api.permisos()[rol].find((item) => item.modulo === modulo);
  }
}
