import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../http/api-url';
import {
  ModuloPermiso,
  matrizPermisosPorDefecto,
} from '../../features/ecosistema/models/ecosistema.model';
import { APP_NAV_ITEMS } from '../../layout/navigation';

export type NivelPermiso = 'L' | 'E' | 'lectura' | 'escritura';

export interface PermisoModuloSesion {
  modulo: ModuloPermiso | string;
  lectura: boolean;
  escritura: boolean;
}

export interface UsuarioSesion {
  id: string;
  empresaId: string;
  nombre: string;
  email: string;
  rol: 'ADMIN' | 'CAJERO' | 'ALMACEN';
}

export interface EmpresaLogin {
  id: string;
  nombreComercial: string;
  razonSocial: string;
}

interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  usuario: UsuarioSesion;
  permisos?: PermisoModuloSesion[];
}

interface SesionActualResponse {
  usuario: UsuarioSesion;
  permisos: PermisoModuloSesion[];
}

interface EmpresasDisponiblesResponse {
  empresas: EmpresaLogin[];
  unicoTenant: boolean;
}

interface SesionPersistida {
  accessToken: string;
  expiresAt: string;
  usuario: UsuarioSesion;
  permisos: PermisoModuloSesion[];
}

const STORAGE_KEY = 'capitalpos.tcg.session';

@Injectable({ providedIn: 'root' })
export class AuthSessionService {
  private readonly http = inject(HttpClient);
  private readonly sesionSignal = signal<SesionPersistida | null>(leerSesion());

  readonly sesion = this.sesionSignal.asReadonly();
  readonly isAuthenticated = computed(() => {
    const sesion = this.sesionSignal();
    return !!sesion?.accessToken && !expirada(sesion.expiresAt);
  });
  readonly accessToken = computed(() =>
    this.isAuthenticated() ? (this.sesionSignal()?.accessToken ?? null) : null,
  );
  readonly empresaId = computed(() => this.sesionSignal()?.usuario.empresaId ?? null);
  readonly usuario = computed(() => this.sesionSignal()?.usuario ?? null);
  readonly isAdmin = computed(() => this.usuario()?.rol === 'ADMIN');
  readonly permisos = computed(() => this.sesionSignal()?.permisos ?? []);

  readonly menuItemsPermitidos = computed(() =>
    APP_NAV_ITEMS.filter((item) => this.hasPermission(item.modulo, 'L')),
  );

  readonly primeraRutaPermitida = computed(() => {
    const items = this.menuItemsPermitidos();
    return items[0]?.route ?? '/app/dashboard';
  });

  async listarEmpresas(): Promise<{ empresas: EmpresaLogin[]; unicoTenant: boolean }> {
    const respuesta = await firstValueFrom(
      this.http.get<EmpresasDisponiblesResponse>(apiUrl('auth/empresas')),
    );
    return {
      empresas: respuesta.empresas ?? [],
      unicoTenant: respuesta.unicoTenant || (respuesta.empresas?.length ?? 0) === 1,
    };
  }

  async login(email: string, password: string, empresaId?: string | null): Promise<UsuarioSesion> {
    const cuerpo: { email: string; password: string; empresaId?: string } = {
      email: email.trim(),
      password,
    };
    const tenant = empresaId?.trim();
    if (tenant) {
      cuerpo.empresaId = tenant;
    }

    const respuesta = await firstValueFrom(
      this.http.post<LoginResponse>(apiUrl('auth/login'), cuerpo),
    );
    const persistida: SesionPersistida = {
      accessToken: respuesta.accessToken,
      expiresAt: respuesta.expiresAt,
      usuario: respuesta.usuario,
      permisos: normalizarPermisos(respuesta.permisos, respuesta.usuario.rol),
    };
    escribirSesion(persistida);
    this.sesionSignal.set(persistida);
    // Login → Bearer → /auth/me (permisos/menú frescos). Si falla, se mantienen los del login.
    await this.refrescarPermisos();
    return this.sesionSignal()?.usuario ?? persistida.usuario;
  }

  async refrescarPermisos(): Promise<void> {
    if (!this.isAuthenticated()) {
      return;
    }
    try {
      const respuesta = await firstValueFrom(
        this.http.get<SesionActualResponse>(apiUrl('auth/me')),
      );
      const actual = this.sesionSignal();
      if (!actual) {
        return;
      }
      const actualizada: SesionPersistida = {
        ...actual,
        usuario: respuesta.usuario,
        permisos: normalizarPermisos(respuesta.permisos, respuesta.usuario.rol),
      };
      escribirSesion(actualizada);
      this.sesionSignal.set(actualizada);
    } catch {
      // Si falla el refresh, se mantiene la sesión local.
    }
  }

  /** Actualiza la matriz del rol actual en sesión (p.ej. tras guardar en Ecosistema). */
  aplicarPermisosSesion(permisos: PermisoModuloSesion[]): void {
    const actual = this.sesionSignal();
    if (!actual) {
      return;
    }
    const actualizada: SesionPersistida = {
      ...actual,
      permisos: normalizarPermisos(permisos, actual.usuario.rol),
    };
    escribirSesion(actualizada);
    this.sesionSignal.set(actualizada);
  }

  hasPermission(modulo: string | null | undefined, nivel: NivelPermiso = 'L'): boolean {
    if (!modulo) {
      return false;
    }
    const permiso = this.permisos().find((item) => item.modulo === modulo);
    if (!permiso) {
      return false;
    }
    const exigeEscritura = nivel === 'E' || nivel === 'escritura';
    if (exigeEscritura) {
      return !!permiso.escritura;
    }
    return !!permiso.lectura || !!permiso.escritura;
  }

  canRead(modulo: string): boolean {
    return this.hasPermission(modulo, 'L');
  }

  canWrite(modulo: string): boolean {
    return this.hasPermission(modulo, 'E');
  }

  logout(): void {
    this.clear();
  }

  clear(): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.sesionSignal.set(null);
  }
}

function normalizarPermisos(
  permisos: PermisoModuloSesion[] | undefined,
  rol: UsuarioSesion['rol'],
): PermisoModuloSesion[] {
  if (permisos && permisos.length > 0) {
    return permisos.map((item) => ({
      modulo: item.modulo,
      lectura: !!item.lectura || !!item.escritura,
      escritura: !!item.escritura,
    }));
  }
  return matrizPermisosPorDefecto()[rol].map((item) => ({ ...item }));
}

function leerSesion(): SesionPersistida | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    const parsed = JSON.parse(raw) as SesionPersistida;
    if (!parsed?.accessToken || !parsed.usuario?.empresaId) {
      return null;
    }
    if (expirada(parsed.expiresAt)) {
      sessionStorage.removeItem(STORAGE_KEY);
      return null;
    }
    return {
      ...parsed,
      permisos: normalizarPermisos(parsed.permisos, parsed.usuario.rol),
    };
  } catch {
    return null;
  }
}

function escribirSesion(sesion: SesionPersistida): void {
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(sesion));
}

function expirada(expiresAt: string): boolean {
  const limite = Date.parse(expiresAt);
  return Number.isFinite(limite) && limite <= Date.now();
}
