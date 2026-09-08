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

export function etiquetaDocumento(cliente: Pick<Cliente, 'tipoDocumento' | 'numeroDocumento' | 'esPublicoGeneral'>): string {
  if (cliente.esPublicoGeneral) {
    return 'Público general';
  }
  if (cliente.tipoDocumento === 'SIN_DOCUMENTO' || !cliente.numeroDocumento) {
    return 'Sin documento';
  }
  return `${cliente.tipoDocumento} ${cliente.numeroDocumento}`;
}

export function validarDocumento(tipo: TipoDocumentoIdentidad, numero: string, esPublicoGeneral: boolean): string | null {
  if (esPublicoGeneral || tipo === 'SIN_DOCUMENTO') {
    return null;
  }
  const digitos = numero.replace(/\D/g, '');
  if (tipo === 'DNI' && digitos.length !== 8) {
    return 'El DNI debe tener 8 dígitos.';
  }
  if (tipo === 'RUC' && digitos.length !== 11) {
    return 'El RUC debe tener 11 dígitos.';
  }
  return null;
}
