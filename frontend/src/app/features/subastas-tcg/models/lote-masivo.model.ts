export interface LoteMasivoFilaLocal {
  id: string;
  titulo: string;
  precioBase: number;
  horaFinLocal: string;
  observaciones: string;
  previewUrl: string;
  imagenBase64: string;
  imagenNombreArchivo: string;
  imagenContentType: string;
  textoFacebook?: string;
  subastaId?: string;
  numArticulo?: number;
}

export interface CrearLoteMasivoRequest {
  sedeId: string;
  fechaInicio?: string | null;
  horaFinBase?: string | null;
  intervaloMinutos: number;
  incrementoMinimo: number;
  items: Array<{
    titulo: string;
    precioBase: number;
    horaFin?: string | null;
    observaciones?: string | null;
    imagenBase64?: string | null;
    imagenNombreArchivo?: string | null;
    imagenContentType?: string | null;
  }>;
}

export interface LoteMasivoItemResponse {
  subastaId: string;
  codigoSubasta: string;
  numSubasta: string;
  numArticulo: number;
  titulo: string;
  precioBase: number;
  fechaCierre: string;
  textoFacebook: string;
  imagenUrl?: string | null;
  productoId?: string | null;
}

export interface LoteMasivoResponse {
  codigoLote: string;
  totalItems: number;
  items: LoteMasivoItemResponse[];
}
