import {
  CondicionTcg,
  IdiomaTcg,
  RarezaTcg,
  TipoCartaTcg,
} from '../../productos-tcg/models/producto-tcg.model';

export interface TcgSerie {
  id: string;
  juego: string;
  codigo: string;
  nombre: string;
  activa: boolean;
}

export interface TcgSet {
  id: string;
  serieId: string;
  serieCodigo: string;
  serieNombre: string;
  codigo: string;
  nombre: string;
  nombreEn: string | null;
  codigoImpresion: string | null;
  totalCartas: number;
  fechaLanzamiento: string | null;
  cartasCount?: number;
  skusCount?: number;
  codigosBloqueados?: boolean;
}

export interface ActualizarTcgSetRequest {
  nombreSerie: string;
  nombreSet: string;
  nombreEn?: string | null;
  codigoSerie?: string | null;
  codigoSet?: string | null;
}

export interface TcgCarta {
  id: string;
  setId: string;
  setCodigo: string;
  setNombre: string;
  numero: string;
  nombre: string;
  tipoCarta: TipoCartaTcg;
  rareza: RarezaTcg;
  artista: string | null;
  imagenOficialUrl: string | null;
}

export interface UpsertTcgSerieRequest {
  juego: string;
  codigo: string;
  nombre: string;
  activa: boolean;
}

export interface UpsertTcgSetRequest {
  serieId: string;
  codigo: string;
  nombre: string;
  nombreEn?: string | null;
  codigoImpresion?: string | null;
  totalCartas: number;
  fechaLanzamiento?: string | null;
}

export interface ImportarTcgSetRequest {
  codigo: string;
  nombre: string;
  nombreEn?: string | null;
  codigoImpresion?: string | null;
  totalCartas: number;
  fechaLanzamiento?: string | null;
}

export interface UpsertTcgCartaRequest {
  numero: string;
  nombre: string;
  tipoCarta: TipoCartaTcg;
  rareza: RarezaTcg;
  artista?: string | null;
  imagenOficialUrl?: string | null;
}

export interface ImportarSetTcgRequest {
  serie: UpsertTcgSerieRequest;
  set: ImportarTcgSetRequest;
  cartas: UpsertTcgCartaRequest[];
}

export interface ImportarSetTcgResponse {
  serie: TcgSerie;
  set: TcgSet;
  cartasCreadas: number;
  cartasActualizadas: number;
  cartas: TcgCarta[];
}

export interface CrearVarianteProductoCartaRequest {
  cartaCatalogoId: string;
  esFoil: boolean;
  condicion: CondicionTcg;
  idioma: IdiomaTcg;
  precioVenta: number;
  costo?: number | null;
  codigoSku?: string | null;
  codigoBarras?: string | null;
  imagenes?: string[];
  sedeId?: string | null;
  stockInicial: number;
  tipoIngresoStock: 'AJUSTE' | 'INGRESO_COMPRA';
}
