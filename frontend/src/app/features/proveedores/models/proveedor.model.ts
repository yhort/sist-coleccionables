export interface Proveedor {
  id: string;
  ruc: string;
  razonSocial: string;
  nombreComercial: string | null;
  telefono: string | null;
  activo: boolean;
  fechaCreacion: string;
}

export interface UpsertProveedorRequest {
  ruc: string;
  razonSocial: string;
  nombreComercial?: string | null;
  telefono?: string | null;
  activo: boolean;
}

/** Filtro de maestros: activos (operativo), inactivos (reactivación) o todos. */
export type FiltroActivoMaestro = 'activos' | 'inactivos' | 'todos';

export const FILTROS_ACTIVO_MAESTRO: readonly FiltroActivoMaestro[] = [
  'activos',
  'inactivos',
  'todos',
];

export const ETIQUETAS_FILTRO_ACTIVO: Record<FiltroActivoMaestro, string> = {
  activos: 'Activos',
  inactivos: 'Inactivos',
  todos: 'Todos',
};

export function etiquetaProveedor(proveedor: Pick<Proveedor, 'ruc' | 'razonSocial' | 'nombreComercial'>): string {
  const nombre = proveedor.nombreComercial && proveedor.nombreComercial !== proveedor.razonSocial
    ? `${proveedor.nombreComercial} · ${proveedor.razonSocial}`
    : proveedor.razonSocial;
  return `${nombre} · ${proveedor.ruc}`;
}

export function validarRuc(ruc: string): string | null {
  return ruc.replace(/\D/g, '').length === 11 ? null : 'El RUC debe tener 11 dígitos.';
}
