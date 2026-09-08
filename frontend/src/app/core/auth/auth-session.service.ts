import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../http/api-url';

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
}

interface EmpresasDisponiblesResponse {
  empresas: EmpresaLogin[];
  unicoTenant: boolean;
}

interface SesionPersistida {
  accessToken: string;
  expiresAt: string;
  usuario: UsuarioSesion;
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
    };
    escribirSesion(persistida);
    this.sesionSignal.set(persistida);
    return persistida.usuario;
  }

  logout(): void {
    this.clear();
  }

  clear(): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.sesionSignal.set(null);
  }
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
    return parsed;
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
