import type { CanalContactoCliente } from '../../../shared/models/contacto-entrega.model';

export type { CanalContactoCliente };

export type TipoDocumentoIdentidad = 'DNI' | 'RUC' | 'CE' | 'PASAPORTE' | 'SIN_DOCUMENTO';

export interface Cliente {
  id: string;
  nombre: string;
  telefono: string | null;
  puntoEntregaPreferido: string | null;
  canalContacto: CanalContactoCliente | null;
  contactoReferencia: string | null;
  tipoDocumento: TipoDocumentoIdentidad;
  numeroDocumento: string | null;
  esPublicoGeneral: boolean;
  activo: boolean;
  fechaCreacion: string;
}

export interface UpsertClienteRequest {
  nombre: string;
  telefono?: string | null;
  puntoEntregaPreferido?: string | null;
  canalContacto?: CanalContactoCliente | null;
  contactoReferencia?: string | null;
  tipoDocumento: TipoDocumentoIdentidad;
  numeroDocumento?: string | null;
  esPublicoGeneral: boolean;
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

export const TIPOS_DOCUMENTO: readonly TipoDocumentoIdentidad[] = [
  'DNI',
  'RUC',
  'CE',
  'PASAPORTE',
  'SIN_DOCUMENTO',
];

export const ETIQUETAS_DOCUMENTO: Record<TipoDocumentoIdentidad, string> = {
  DNI: 'DNI (8 dígitos)',
  RUC: 'RUC (11 dígitos)',
  CE: 'Carné de extranjería',
  PASAPORTE: 'Pasaporte',
  SIN_DOCUMENTO: 'Sin documento',
};

/** Teléfono: únicamente dígitos, máximo 9. */
export const TELEFONO_MAX_DIGITOS = 9;
export const TELEFONO_PATTERN = /^\d{0,9}$/;

export function etiquetaDocumento(cliente: Pick<Cliente, 'tipoDocumento' | 'numeroDocumento' | 'esPublicoGeneral'>): string {
  if (cliente.esPublicoGeneral) {
    return 'Público general';
  }
  if (cliente.tipoDocumento === 'SIN_DOCUMENTO' || !cliente.numeroDocumento) {
    return 'Sin documento';
  }
  return `${cliente.tipoDocumento} ${cliente.numeroDocumento}`;
}

/** SUNAT no exige identificar al adquirente en boletas de este importe o menor. */
export const UMBRAL_IDENTIFICACION_BOLETA = 700;

export function clienteIdentificado(
  tipo: TipoDocumentoIdentidad | null | undefined,
  numero: string | null | undefined,
): boolean {
  if (!tipo || tipo === 'SIN_DOCUMENTO') {
    return false;
  }
  const texto = (numero ?? '').trim();
  const digitos = texto.replace(/\D/g, '');
  return !!texto && texto !== '-' && digitos !== '00000000' && digitos !== '0000000';
}

export function validarDocumento(tipo: TipoDocumentoIdentidad, numero: string, esPublicoGeneral: boolean): string | null {
  if (esPublicoGeneral || tipo === 'SIN_DOCUMENTO') {
    return null;
  }
  const texto = (numero ?? '').trim();
  // Vacío permitido (boleta ≤ S/ 700); si hay valor parcial se valida formato.
  if (!texto) {
    return null;
  }
  const digitos = texto.replace(/\D/g, '');
  if (tipo === 'DNI' && digitos.length !== 8) {
    return 'El DNI debe tener 8 dígitos.';
  }
  if (tipo === 'RUC' && digitos.length !== 11) {
    return 'El RUC debe tener 11 dígitos.';
  }
  return null;
}

/** Validación previa a emitir CPE según tipo de comprobante e importe. */
export function validarDocumentoParaComprobante(
  tipoComprobante: 'BOLETA' | 'FACTURA' | 'NOTA_VENTA',
  total: number,
  tipoDocumento: TipoDocumentoIdentidad | null | undefined,
  numeroDocumento: string | null | undefined,
): string | null {
  if (tipoComprobante === 'FACTURA') {
    const digitos = (numeroDocumento ?? '').replace(/\D/g, '');
    if (tipoDocumento !== 'RUC' || digitos.length !== 11) {
      return 'La factura exige un cliente con RUC de 11 dígitos.';
    }
    return null;
  }

  if (
    tipoComprobante === 'BOLETA' &&
    total > UMBRAL_IDENTIFICACION_BOLETA &&
    !clienteIdentificado(tipoDocumento, numeroDocumento)
  ) {
    return 'La boleta mayor a S/ 700 exige DNI u otro documento de identidad del adquirente.';
  }

  return null;
}

/** Vacío permitido; si hay valor: solo dígitos y máximo 9. */
export function validarTelefono(telefono: string | null | undefined): string | null {
  const texto = (telefono ?? '').trim();
  if (!texto) {
    return null;
  }
  if (!TELEFONO_PATTERN.test(texto)) {
    return 'El teléfono solo admite números (dígitos) y un máximo de 9 caracteres.';
  }
  return null;
}

/** Filtra entrada en tiempo real: solo dígitos, máx. 9. */
export function sanitizarTelefono(valor: string): string {
  return valor.replace(/\D/g, '').slice(0, TELEFONO_MAX_DIGITOS);
}

export function documentoDuplicadoEnLista(
  clientes: readonly Cliente[],
  numeroDocumento: string | null | undefined,
  excluirId?: string | null,
): Cliente | null {
  const numero = (numeroDocumento ?? '').replace(/\D/g, '');
  if (!numero || numero === '00000000' || numero === '0000000') {
    return null;
  }
  return (
    clientes.find(
      (cliente) =>
        cliente.activo &&
        cliente.id !== excluirId &&
        (cliente.numeroDocumento ?? '').replace(/\D/g, '') === numero,
    ) ?? null
  );
}
