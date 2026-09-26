export type TipoComprobanteSunat = 'BOLETA' | 'FACTURA' | 'NOTA_CREDITO' | 'GUIA_REMISION' | 'NOTA_VENTA';

export type EstadoEmisionSunat =
  | 'SIMULADO'
  | 'ACEPTADO'
  | 'RECHAZADO'
  | 'ERROR_VALIDACION'
  | 'ERROR_SUNAT'
  | 'PENDIENTE_CONSOLIDAR'
  | 'CONSOLIDADA';

export const ETIQUETAS_ESTADO_EMISION: Record<EstadoEmisionSunat, string> = {
  SIMULADO: 'Simulado',
  ACEPTADO: 'Aceptado',
  RECHAZADO: 'Rechazado',
  ERROR_VALIDACION: 'Error de validación',
  ERROR_SUNAT: 'Error SUNAT',
  PENDIENTE_CONSOLIDAR: 'Pendiente de consolidar',
  CONSOLIDADA: 'Facturada / Consolidada',
};

export const ESTADOS_EMISION: readonly EstadoEmisionSunat[] = [
  'ACEPTADO',
  'CONSOLIDADA',
  'PENDIENTE_CONSOLIDAR',
  'SIMULADO',
  'RECHAZADO',
  'ERROR_SUNAT',
  'ERROR_VALIDACION',
];

export const TAMANOS_PAGINA_COMPROBANTES = [10, 20, 50] as const;

export type TipoSedeEmpresa = 'TIENDA' | 'ALMACEN';

export type RolUsuario = 'ADMIN' | 'CAJERO' | 'ALMACEN';

export type ModuloPermiso =
  | 'dashboard'
  | 'productos-tcg'
  | 'inventario'
  | 'aperturas-tcg'
  | 'subastas-tcg'
  | 'pedidos-digitales'
  | 'pagos'
  | 'entregas'
  | 'woocommerce'
  | 'reportes'
  | 'ecosistema';

export type EventoWebhook = 'pedido.estado' | 'guia.estado';

export interface ConfiguracionFiscalEmpresa {
  ruc: string;
  razonSocial: string;
  nombreComercial: string;
  direccionFiscal: string;
  ubigeo: string;
  departamento: string;
  provincia: string;
  distrito: string;
}

export interface SerieComprobante {
  tipo: TipoComprobanteSunat;
  serie: string;
  correlativo: number;
  activa: boolean;
}

export interface SedeEmpresa {
  id: string;
  nombre: string;
  tipo: TipoSedeEmpresa;
  direccion: string;
  distrito: string;
  provincia: string;
  departamento: string;
  ubigeo: string;
  esPuntoPartidaGre: boolean;
  esPuntoLlegadaGre: boolean;
  esAlmacenPrincipal: boolean;
  activa: boolean;
  /** True si tiene stock, movimientos, ventas u otros vínculos (solo soft-delete). */
  tieneDependencias?: boolean;
}

export interface UsuarioEmpresa {
  id: string;
  dni: string;
  nombres: string;
  apellidos: string;
  /** Nombre completo de display (auditoría). */
  nombre: string;
  email: string;
  rol: RolUsuario;
  activo: boolean;
  fechaCreacion?: string;
}

export interface GuardarUsuarioRequest {
  dni: string;
  nombres: string;
  apellidos: string;
  email: string;
  /** Obligatoria al crear; opcional al editar (vacía = no cambiar). */
  password?: string;
  rol: RolUsuario;
  activo: boolean;
}

export interface PermisoModulo {
  modulo: ModuloPermiso;
  lectura: boolean;
  escritura: boolean;
}

export type MatrizPermisos = Record<RolUsuario, PermisoModulo[]>;

export interface WebhookSalida {
  id: string;
  nombre: string;
  url: string;
  token: string;
  canal: 'WHATSAPP';
  eventos: EventoWebhook[];
  activo: boolean;
}

export interface WebhookLog {
  id: string;
  webhookId: string;
  evento: EventoWebhook;
  destino: string;
  payloadResumen: string;
  estado: 'ENVIADO' | 'OMITIDO' | 'ERROR';
  fecha: string;
}

export interface DocumentoEmitible {
  id: string;
  origen: 'PEDIDO' | 'ENTREGA';
  pedidoId: string;
  entregaId: string | null;
  ventaId: string | null;
  clienteNombre: string;
  total: number;
  etiqueta: string;
  clienteTipoDocumento?: string | null;
}

export interface EmisionSimulada {
  id: string;
  tipo: TipoComprobanteSunat;
  serie: string;
  correlativo: number;
  estado: EstadoEmisionSunat;
  pedidoId: string;
  ventaId: string | null;
  clienteNombre: string;
  total: number;
  mensaje: string;
  fecha: string;
  hashFirma: string | null;
  tieneXml: boolean;
  tieneCdr: boolean;
  tienePdf: boolean;
  boletaConsolidadaId?: string | null;
  codigoMotivo?: string | null;
  descripcionMotivo?: string | null;
  documentoReferencia?: string | null;
}

export function numeroComprobante(emision: Pick<EmisionSimulada, 'serie' | 'correlativo'>): string {
  return `${emision.serie}-${String(emision.correlativo).padStart(8, '0')}`;
}

export function puedeDescargarXml(emision: Pick<EmisionSimulada, 'tieneXml'>): boolean {
  return emision.tieneXml;
}

export function puedeDescargarCdr(emision: Pick<EmisionSimulada, 'estado' | 'tieneCdr'>): boolean {
  return emision.tieneCdr && emision.estado === 'ACEPTADO';
}

export function puedeAbrirPdf(emision: Pick<EmisionSimulada, 'tienePdf'>): boolean {
  return emision.tienePdf;
}

export interface GuardarSedeRequest {
  nombre: string;
  tipo: TipoSedeEmpresa;
  direccion: string;
  distrito: string;
  provincia: string;
  departamento: string;
  ubigeo: string;
  esPuntoPartidaGre: boolean;
  esPuntoLlegadaGre: boolean;
  esAlmacenPrincipal: boolean;
  activa: boolean;
}

export interface EliminarSedeResultado {
  id: string;
  accion: 'ELIMINADA' | 'DESACTIVADA';
  motivo: string | null;
  sede: SedeEmpresa | null;
}

export type FiltroBoletaConsolidada = 'VENTAS_MENORES' | 'SELECCION_GENERAL';

export const UMBRAL_VENTAS_MENORES = 5;

export interface NotaVentaPendiente {
  id: string;
  ventaId: string;
  serie: string;
  correlativo: number;
  estado: EstadoEmisionSunat;
  clienteNombre: string;
  total: number;
  fechaEmision: string;
  esVentaMenor: boolean;
}

export interface BoletaConsolidadaResultado {
  id: string;
  filtro: FiltroBoletaConsolidada;
  fechaOperacion: string;
  cantidadNotas: number;
  total: number;
  comprobante: EmisionSimulada;
  notas: NotaVentaPendiente[];
}

export interface EmitirPruebaRequest {
  tipo: TipoComprobanteSunat;
  documentoId: string;
}

export const TIPOS_COMPROBANTE: readonly TipoComprobanteSunat[] = [
  'BOLETA',
  'FACTURA',
  'NOTA_VENTA',
  'NOTA_CREDITO',
  'GUIA_REMISION',
];

export const TIPOS_COMPROBANTE_POS: readonly TipoComprobanteSunat[] = ['BOLETA', 'FACTURA', 'NOTA_VENTA'];

export const CODIGOS_SUNAT: Record<TipoComprobanteSunat, string> = {
  BOLETA: '03',
  FACTURA: '01',
  NOTA_CREDITO: '07',
  GUIA_REMISION: '09',
  NOTA_VENTA: 'NV',
};

export const SERIES_POR_DEFECTO: Record<TipoComprobanteSunat, string> = {
  BOLETA: 'B001',
  FACTURA: 'F001',
  NOTA_CREDITO: 'FC01',
  GUIA_REMISION: 'T001',
  NOTA_VENTA: 'NV01',
};

export const ETIQUETAS_COMPROBANTE: Record<TipoComprobanteSunat, string> = {
  BOLETA: 'Boleta',
  FACTURA: 'Factura',
  NOTA_CREDITO: 'Nota de crédito',
  GUIA_REMISION: 'Guía de remisión remitente',
  NOTA_VENTA: 'Nota de venta',
};

export const ETIQUETAS_FILTRO_CONSOLIDADA: Record<FiltroBoletaConsolidada, string> = {
  VENTAS_MENORES: 'Ventas menores a S/ 5.00',
  SELECCION_GENERAL: 'Selección general',
};

export const ETIQUETAS_TIPO_SEDE: Record<TipoSedeEmpresa, string> = {
  TIENDA: 'Sede física',
  ALMACEN: 'Almacén',
};

export const ETIQUETAS_ROL: Record<RolUsuario, string> = {
  ADMIN: 'Admin',
  CAJERO: 'Cajero / Vendedor',
  ALMACEN: 'Encargado de almacén / Logística',
};

export const MODULOS_PERMISO: readonly ModuloPermiso[] = [
  'dashboard',
  'productos-tcg',
  'inventario',
  'aperturas-tcg',
  'subastas-tcg',
  'pedidos-digitales',
  'pagos',
  'entregas',
  'woocommerce',
  'reportes',
  'ecosistema',
];

export const ETIQUETAS_MODULO: Record<ModuloPermiso, string> = {
  dashboard: 'Dashboard',
  'productos-tcg': 'Productos TCG',
  inventario: 'Inventario',
  'aperturas-tcg': 'Aperturas TCG',
  'subastas-tcg': 'Subastas TCG',
  'pedidos-digitales': 'Pedidos Digitales',
  pagos: 'Pagos',
  entregas: 'Entregas',
  woocommerce: 'WooCommerce',
  reportes: 'Reportes',
  ecosistema: 'Ecosistema',
};

export const ROLES_USUARIO: readonly RolUsuario[] = ['ADMIN', 'CAJERO', 'ALMACEN'];

export const EVENTOS_WEBHOOK: readonly EventoWebhook[] = ['pedido.estado', 'guia.estado'];

export const ETIQUETAS_EVENTO_WEBHOOK: Record<EventoWebhook, string> = {
  'pedido.estado': 'Cambio de estado de pedido',
  'guia.estado': 'Cambio de estado de guía',
};

export function prefijoSerieValido(tipo: TipoComprobanteSunat, serie: string): boolean {
  const valor = serie.trim().toUpperCase();
  if (valor.length !== 4) {
    return false;
  }
  switch (tipo) {
    case 'BOLETA':
      return valor.startsWith('B');
    case 'FACTURA':
      return valor.startsWith('F') && !valor.startsWith('FC');
    case 'NOTA_CREDITO':
      return valor.startsWith('FC') || valor.startsWith('BC');
    case 'GUIA_REMISION':
      return valor.startsWith('T');
    case 'NOTA_VENTA':
      return valor.startsWith('N');
  }
}

export function rucValido(ruc: string): boolean {
  return /^\d{11}$/.test(ruc.trim());
}

export function ubigeoValido(ubigeo: string): boolean {
  return /^\d{6}$/.test(ubigeo.trim());
}

export function matrizPermisosPorDefecto(): MatrizPermisos {
  return {
    ADMIN: MODULOS_PERMISO.map((modulo) => ({ modulo, lectura: true, escritura: true })),
    CAJERO: MODULOS_PERMISO.map((modulo) => ({
      modulo,
      lectura: true,
      escritura:
        modulo === 'subastas-tcg' ||
        modulo === 'pedidos-digitales' ||
        modulo === 'pagos' ||
        modulo === 'entregas' ||
        modulo === 'reportes',
    })),
    ALMACEN: MODULOS_PERMISO.map((modulo) => ({
      modulo,
      lectura: true,
      escritura:
        modulo === 'productos-tcg' ||
        modulo === 'inventario' ||
        modulo === 'aperturas-tcg' ||
        modulo === 'entregas',
    })),
  };
}
