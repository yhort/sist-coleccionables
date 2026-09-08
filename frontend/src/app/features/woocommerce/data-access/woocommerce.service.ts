import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Subscription, firstValueFrom, interval } from 'rxjs';

import { SedesApiService } from '../../../core/data-access/sedes-api.service';
import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { PedidosDigitalesApiService } from '../../pedidos-digitales/data-access/pedidos-digitales.service';
import {
  CONFIG_WOO_VACIA,
  ConfiguracionWooCommerce,
  EstadoConexionPanel,
  EstadoConexionWoo,
  EstadoMapeoWoo,
  EventoSyncWoo,
  FilaCsvWoo,
  FilaMapeoWoo,
  ModoRecepcionPedidosWoo,
  PedidoWooPendiente,
  ResultadoEventoSyncWoo,
  ResultadoImportacionPedidos,
  ResultadoSyncCatalogo,
  ResultadoSyncStock,
  TipoEventoSyncWoo,
  WooKpis,
} from '../models/woocommerce.model';

const INTERVALO_POLLING_MS = 7000;

interface WooConfigApi {
  id: string;
  urlTienda: string;
  consumerKeyEnmascarada: string;
  tieneSecret: boolean;
  sedeOrigenId: string;
  sedeOrigenNombre: string;
  modoSincronizacion?: 'MANUAL' | 'PROGRAMADA';
  modoRecepcionPedidos: ModoRecepcionPedidosWoo;
  estadoConexion: EstadoConexionWoo;
  mensajeConexion?: string | null;
  ultimoIntentoConexion?: string | null;
  activa: boolean;
}

interface WooMapeoApi {
  productoId: string;
  nombre: string;
  sku: string;
  precioLocal: number;
  stockLocal: number;
  wooProductId?: number | null;
  wooVariationId?: number | null;
  precioNormalWoo: number;
  precioRebajadoWoo?: number | null;
  stockWoo?: number | null;
  estadoMapeo: EstadoMapeoWoo;
  mensaje?: string | null;
  ultimaSincronizacion?: string | null;
}

interface WooLogApi {
  id: string;
  tipo: 'STOCK_OUT' | 'STOCK_IN' | 'PRODUCTO_OUT' | 'PEDIDO_IN';
  estado: 'OK' | 'ERROR' | 'REINTENTO';
  payloadResumen: string;
  mensajeError?: string | null;
  intentos?: number;
  proximoReintento?: string | null;
  fecha: string;
}

interface WooImportApi {
  importados: number;
  omitidos: number;
  errores: number;
  referencias: string[];
}

interface WooSyncCatalogoApi {
  enviados: number;
  omitidos: number;
  errores: number;
}

interface WooSyncStockApi {
  publicados: number;
  omitidos: number;
  errores: number;
}

@Injectable({ providedIn: 'root' })
export class WooCommerceApiService {
  private readonly http = inject(HttpClient);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly sedesApi = inject(SedesApiService);

  private readonly configSignal = signal<ConfiguracionWooCommerce>({ ...CONFIG_WOO_VACIA });
  private readonly conexionSignal = signal<EstadoConexionPanel>({
    estado: 'DESCONECTADO',
    mensaje: 'Aún no se ha probado la REST API.',
    ultimoIntento: null,
  });
  private readonly logsSignal = signal<EventoSyncWoo[]>([]);
  private readonly mapeosSignal = signal<FilaMapeoWoo[]>([]);
  private readonly colaSignal = signal<PedidoWooPendiente[]>([]);
  private readonly sincronizandoSignal = signal(false);
  private readonly importandoSignal = signal(false);
  private readonly modoRecepcionSignal = signal<ModoRecepcionPedidosWoo>('WEBHOOK');
  private readonly pollingActivoSignal = signal(false);
  private pollingSub: Subscription | null = null;

  readonly config = this.configSignal.asReadonly();
  readonly conexion = this.conexionSignal.asReadonly();
  readonly logs = this.logsSignal.asReadonly();
  readonly mapeos = this.mapeosSignal.asReadonly();
  readonly colaPedidos = this.colaSignal.asReadonly();
  readonly sincronizando = this.sincronizandoSignal.asReadonly();
  readonly importando = this.importandoSignal.asReadonly();
  readonly modoRecepcion = this.modoRecepcionSignal.asReadonly();
  readonly pollingActivo = this.pollingActivoSignal.asReadonly();
  readonly sedes = this.sedesApi.sedes;

  readonly kpis = computed<WooKpis>(() => {
    const filas = this.mapeosSignal();
    return {
      conectado: this.conexionSignal().estado === 'CONECTADO',
      sincronizados: filas.filter((fila) => fila.estadoMapeo === 'SINCRONIZADO').length,
      desfasados: filas.filter((fila) => fila.estadoMapeo === 'DESFASADO').length,
      pendientesSubida: filas.filter((fila) => fila.estadoMapeo === 'PENDIENTE_SUBIDA').length,
      noMapeados: filas.filter((fila) => fila.estadoMapeo === 'NO_MAPEADO').length,
      colaPedidos: this.colaSignal().length,
    };
  });

  async cargar(): Promise<void> {
    if (this.sedesApi.sedes().length === 0) {
      await this.sedesApi.refrescar();
    }
    await Promise.all([this.cargarConfig(), this.cargarMapeos(), this.cargarLogs()]);
  }

  async guardarConfig(config: ConfiguracionWooCommerce): Promise<ConfiguracionWooCommerce> {
    try {
      const dto = await firstValueFrom(
        this.http.put<WooConfigApi>(apiUrl('woocommerce/config'), {
          urlTienda: config.urlTienda.trim().replace(/\/$/, ''),
          consumerKey: secretoEditable(config.consumerKey),
          consumerSecret: secretoEditable(config.consumerSecret),
          sedeOrigenId: config.sedeOrigenId,
          modoSincronizacion: 'MANUAL',
          modoRecepcionPedidos: this.modoRecepcionSignal(),
          activa: true,
        }),
      );
      this.aplicarConfig(dto, config.consumerKey, config.consumerSecret);
      return this.configSignal();
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async probarConexion(): Promise<EstadoConexionPanel> {
    try {
      const dto = await firstValueFrom(
        this.http.post<WooConfigApi>(apiUrl('woocommerce/probar'), {}),
      );
      this.aplicarConfig(dto);
      return this.conexionSignal();
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async sincronizarCatalogo(): Promise<ResultadoSyncCatalogo> {
    this.sincronizandoSignal.set(true);
    try {
      const catalogo = await firstValueFrom(
        this.http.post<WooSyncCatalogoApi>(apiUrl('woocommerce/sync/catalogo'), {}),
      );
      await Promise.all([this.cargarMapeos(), this.cargarLogs()]);
      return {
        enviadas: catalogo.enviados,
        omitidas: catalogo.omitidos,
        errores: catalogo.errores,
        filasCsv: this.filasCsvActuales(),
      };
    } catch (error) {
      throw new Error(readApiError(error));
    } finally {
      this.sincronizandoSignal.set(false);
    }
  }

  async sincronizarStock(): Promise<ResultadoSyncStock> {
    this.sincronizandoSignal.set(true);
    try {
      const stock = await firstValueFrom(
        this.http.post<WooSyncStockApi>(apiUrl('woocommerce/sync/stock'), {}),
      );
      await Promise.all([this.cargarMapeos(), this.cargarLogs()]);
      return {
        publicados: stock.publicados,
        omitidos: stock.omitidos,
        errores: stock.errores,
      };
    } catch (error) {
      throw new Error(readApiError(error));
    } finally {
      this.sincronizandoSignal.set(false);
    }
  }

  async sincronizarStockYPrecios(): Promise<ResultadoSyncCatalogo> {
    this.sincronizandoSignal.set(true);
    try {
      const [catalogo, stock] = await Promise.all([
        firstValueFrom(this.http.post<WooSyncCatalogoApi>(apiUrl('woocommerce/sync/catalogo'), {})),
        firstValueFrom(this.http.post<WooSyncStockApi>(apiUrl('woocommerce/sync/stock'), {})),
      ]);
      await Promise.all([this.cargarMapeos(), this.cargarLogs()]);
      return {
        enviadas: catalogo.enviados + stock.publicados,
        omitidas: catalogo.omitidos + stock.omitidos,
        errores: catalogo.errores + stock.errores,
        filasCsv: this.filasCsvActuales(),
      };
    } catch (error) {
      throw new Error(readApiError(error));
    } finally {
      this.sincronizandoSignal.set(false);
    }
  }

  setModoRecepcion(modo: ModoRecepcionPedidosWoo): void {
    this.modoRecepcionSignal.set(modo);
    if (modo === 'WEBHOOK') {
      this.detenerPolling();
    }
  }

  iniciarPolling(): void {
    this.exigirConexion();
    this.modoRecepcionSignal.set('POLLING');
    if (this.pollingSub) {
      return;
    }
    this.pollingActivoSignal.set(true);
    this.pollingSub = interval(INTERVALO_POLLING_MS).subscribe(() => {
      void this.importarPedidosPendientes();
    });
    void this.importarPedidosPendientes();
  }

  detenerPolling(): void {
    this.pollingSub?.unsubscribe();
    this.pollingSub = null;
    this.pollingActivoSignal.set(false);
  }

  importarPedidosPendientes(): Promise<ResultadoImportacionPedidos> {
    this.exigirConexion();
    this.modoRecepcionSignal.set('POLLING');
    return this.importar(apiUrl('woocommerce/sync/pedidos'));
  }

  simularWebhook(): Promise<ResultadoImportacionPedidos> {
    this.exigirConexion();
    this.modoRecepcionSignal.set('WEBHOOK');
    this.detenerPolling();
    return this.importar(apiUrl('woocommerce/webhooks/simular'));
  }

  reintentarWebhooks(): Promise<ResultadoImportacionPedidos> {
    this.exigirConexion();
    return this.importar(apiUrl('woocommerce/webhooks/reintentar'));
  }

  private async importar(url: string): Promise<ResultadoImportacionPedidos> {
    this.importandoSignal.set(true);
    try {
      const dto = await firstValueFrom(this.http.post<WooImportApi>(url, {}));
      await Promise.all([
        this.cargarLogs(),
        this.pedidosApi.refrescar().catch(() => undefined),
      ]);
      return {
        importados: dto.importados,
        omitidos: dto.omitidos,
        errores: dto.errores,
        referencias: dto.referencias ?? [],
      };
    } catch (error) {
      throw new Error(readApiError(error));
    } finally {
      this.importandoSignal.set(false);
    }
  }

  private filasCsvActuales(): FilaCsvWoo[] {
    return this.mapeosSignal()
      .filter((fila) => fila.estadoMapeo !== 'NO_MAPEADO')
      .map((fila) => ({
        ID: fila.wooCommerceId ?? '',
        SKU: fila.sku,
        'Precio normal': fila.precioNormalWoo || fila.precioLocal,
        'Precio rebajado': fila.precioRebajadoWoo ?? '',
        Inventario: fila.stockLocal,
      }));
  }

  private exigirConexion(): void {
    if (this.conexionSignal().estado !== 'CONECTADO') {
      throw new Error('Conecta la REST API de WooCommerce antes de sincronizar.');
    }
    if (!this.configSignal().sedeOrigenId) {
      throw new Error('Selecciona una sede de origen válida.');
    }
  }

  private async cargarConfig(): Promise<void> {
    try {
      const dto = await firstValueFrom(this.http.get<WooConfigApi>(apiUrl('woocommerce/config')));
      this.aplicarConfig(dto);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        const sedeId = this.sedesApi.sedes()[0]?.id ?? '';
        this.configSignal.set({ ...CONFIG_WOO_VACIA, sedeOrigenId: sedeId });
        return;
      }
      throw new Error(readApiError(error));
    }
  }

  private async cargarMapeos(): Promise<void> {
    try {
      const items = await firstValueFrom(this.http.get<WooMapeoApi[]>(apiUrl('woocommerce/mapeos')));
      this.mapeosSignal.set(items.map(mapMapeo));
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  private async cargarLogs(): Promise<void> {
    try {
      const items = await firstValueFrom(
        this.http.get<WooLogApi[]>(apiUrl('woocommerce/logs'), { params: { take: 50 } }),
      );
      this.logsSignal.set(items.map(mapLog));
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  private aplicarConfig(
    dto: WooConfigApi,
    consumerKey?: string,
    consumerSecret?: string,
  ): void {
    this.configSignal.set({
      urlTienda: dto.urlTienda,
      consumerKey: consumerKey && !consumerKey.includes('*') ? consumerKey : dto.consumerKeyEnmascarada,
      consumerSecret: consumerSecret && !consumerSecret.includes('*') ? consumerSecret : '',
      sedeOrigenId: dto.sedeOrigenId,
      tieneSecret: dto.tieneSecret,
    });
    this.conexionSignal.set({
      estado: dto.estadoConexion,
      mensaje: dto.mensajeConexion ?? '',
      ultimoIntento: dto.ultimoIntentoConexion ?? null,
    });
    this.modoRecepcionSignal.set(dto.modoRecepcionPedidos);
  }
}

function secretoEditable(valor: string): string | undefined {
  const texto = valor.trim();
  if (!texto || texto.includes('*')) {
    return undefined;
  }
  return texto;
}

function mapMapeo(dto: WooMapeoApi): FilaMapeoWoo {
  return {
    productoId: dto.productoId,
    nombre: dto.nombre,
    sku: dto.sku,
    wooCommerceId: dto.wooProductId ?? null,
    wooVariationId: dto.wooVariationId ?? null,
    precioLocal: dto.precioLocal,
    precioNormalWoo: dto.precioNormalWoo,
    precioRebajadoWoo: dto.precioRebajadoWoo ?? null,
    stockLocal: dto.stockLocal,
    stockWoo: dto.stockWoo ?? null,
    estadoMapeo: dto.estadoMapeo,
    mensaje: dto.mensaje ?? null,
    ultimaSincronizacion: dto.ultimaSincronizacion ?? null,
  };
}

function mapLog(dto: WooLogApi): EventoSyncWoo {
  return {
    id: dto.id,
    fechaHora: dto.fecha,
    tipo: tipoEventoDe(dto.tipo),
    itemsAfectados: [],
    resultado: resultadoDe(dto.estado),
    detalle: dto.mensajeError || dto.payloadResumen,
    intentos: dto.intentos ?? 0,
    proximoReintento: dto.proximoReintento ?? null,
  };
}

function tipoEventoDe(tipo: WooLogApi['tipo']): TipoEventoSyncWoo {
  switch (tipo) {
    case 'PEDIDO_IN':
      return 'PEDIDO';
    case 'PRODUCTO_OUT':
      return 'PRECIO';
    default:
      return 'STOCK';
  }
}

function resultadoDe(estado: WooLogApi['estado']): ResultadoEventoSyncWoo {
  if (estado === 'OK') {
    return 'OK';
  }
  return estado === 'REINTENTO' ? 'REINTENTO' : 'ERROR';
}
