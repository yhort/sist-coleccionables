import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { EntregasApiService } from '../../entregas/data-access/entregas.service';
import { FILTROS_ENTREGAS_VACIOS } from '../../entregas/models/entrega.model';
import { PedidosDigitalesApiService } from '../../pedidos-digitales/data-access/pedidos-digitales.service';
import { FILTROS_PEDIDOS_VACIOS } from '../../pedidos-digitales/models/pedido-digital.model';
import {
  BoletaConsolidadaResultado,
  ConfiguracionFiscalEmpresa,
  DocumentoEmitible,
  EmisionSimulada,
  EmitirPruebaRequest,
  EventoWebhook,
  FiltroBoletaConsolidada,
  GuardarSedeRequest,
  GuardarUsuarioRequest,
  MatrizPermisos,
  ModuloPermiso,
  NotaVentaPendiente,
  SERIES_POR_DEFECTO,
  SedeEmpresa,
  SerieComprobante,
  TIPOS_COMPROBANTE,
  TipoComprobanteSunat,
  UsuarioEmpresa,
  WebhookLog,
  WebhookSalida,
  matrizPermisosPorDefecto,
  numeroComprobante,
  prefijoSerieValido,
  rucValido,
  ubigeoValido,
} from '../models/ecosistema.model';

interface ConfiguracionFiscalApi {
  ruc: string;
  razonSocial: string;
  nombreComercial: string;
  direccionFiscal: string;
  ubigeo: string;
  departamento: string;
  provincia: string;
  distrito: string;
}

interface SerieComprobanteApi {
  tipo: TipoComprobanteSunat;
  serie: string;
  correlativo: number;
  activa: boolean;
}

interface UsuarioApi {
  id: string;
  dni: string;
  nombres: string;
  apellidos: string;
  nombre: string;
  email: string;
  rol: UsuarioEmpresa['rol'];
  activo: boolean;
  fechaCreacion: string;
}

interface WebhookApi {
  id: string;
  nombre: string;
  url: string;
  token: string;
  canal: 'WHATSAPP';
  eventos: EventoWebhook[];
  activo: boolean;
  logs: WebhookLogApi[];
}

interface WebhookLogApi {
  id: string;
  webhookId: string;
  evento: EventoWebhook;
  destino: string;
  payloadResumen: string;
  estado: WebhookLog['estado'];
  fecha: string;
}

interface ComprobanteApi {
  id: string;
  ventaId: string;
  tipo: TipoComprobanteSunat;
  serie: string;
  correlativo: number;
  estado: EmisionSimulada['estado'];
  mensaje?: string | null;
  fechaEmision: string;
  clienteNombre?: string | null;
  total?: number | null;
  hashFirma?: string | null;
  tieneXml?: boolean;
  tieneCdr?: boolean;
  tienePdf?: boolean;
  boletaConsolidadaId?: string | null;
  codigoMotivo?: string | null;
  descripcionMotivo?: string | null;
  documentoReferencia?: string | null;
}

interface NotaVentaPendienteApi {
  id: string;
  ventaId: string;
  serie: string;
  correlativo: number;
  estado: EmisionSimulada['estado'];
  clienteNombre: string;
  total: number;
  fechaEmision: string;
  esVentaMenor: boolean;
}

interface BoletaConsolidadaApi {
  id: string;
  filtro: FiltroBoletaConsolidada;
  fechaOperacion: string;
  cantidadNotas: number;
  total: number;
  comprobante: ComprobanteApi;
  notas: NotaVentaPendienteApi[];
}

@Injectable({ providedIn: 'root' })
export class EcosistemaApiService {
  private readonly http = inject(HttpClient);
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly entregasApi = inject(EntregasApiService);

  private readonly fiscalSignal = signal<ConfiguracionFiscalEmpresa>(SEED_FISCAL);
  private readonly seriesSignal = signal<SerieComprobante[]>(SEED_SERIES.map(clonarSerie));
  private readonly sedesSignal = signal<SedeEmpresa[]>(SEED_SEDES.map(clonarSede));
  private readonly usuariosSignal = signal<UsuarioEmpresa[]>(SEED_USUARIOS.map(clonarUsuario));
  private readonly permisosSignal = signal<MatrizPermisos>(clonarMatriz(matrizPermisosPorDefecto()));
  private readonly webhooksSignal = signal<WebhookSalida[]>(SEED_WEBHOOKS.map(clonarWebhook));
  private readonly webhookLogsSignal = signal<WebhookLog[]>([]);
  private readonly emisionesSignal = signal<EmisionSimulada[]>([]);
  private readonly notasPendientesSignal = signal<NotaVentaPendiente[]>([]);

  readonly fiscal = this.fiscalSignal.asReadonly();
  readonly series = this.seriesSignal.asReadonly();
  readonly sedes = this.sedesSignal.asReadonly();
  readonly usuarios = this.usuariosSignal.asReadonly();
  readonly permisos = this.permisosSignal.asReadonly();
  readonly webhooks = this.webhooksSignal.asReadonly();
  readonly webhookLogs = this.webhookLogsSignal.asReadonly();
  readonly emisiones = this.emisionesSignal.asReadonly();
  readonly notasPendientes = this.notasPendientesSignal.asReadonly();

  readonly kpis = computed(() => {
    const sedes = this.sedesSignal();
    return {
      ruc: this.fiscalSignal().ruc,
      seriesActivas: this.seriesSignal().filter((item) => item.activa).length,
      sedesActivas: sedes.filter((sede) => sede.activa).length,
      almacenPrincipal: sedes.find((sede) => sede.esAlmacenPrincipal)?.nombre ?? 'Sin asignar',
      usuariosActivos: this.usuariosSignal().filter((usuario) => usuario.activo).length,
      webhooksActivos: this.webhooksSignal().filter((hook) => hook.activo).length,
      emisiones: this.emisionesSignal().length,
    };
  });

  async cargar(): Promise<void> {
    try {
      const [fiscal, series, webhook, comprobantes, usuarios] = await Promise.all([
        firstValueFrom(this.http.get<ConfiguracionFiscalApi>(apiUrl('ecosistema/fiscal'))),
        firstValueFrom(this.http.get<SerieComprobanteApi[]>(apiUrl('ecosistema/series'))),
        firstValueFrom(this.http.get<WebhookApi>(apiUrl('ecosistema/webhooks'))),
        firstValueFrom(this.http.get<ComprobanteApi[]>(apiUrl('ecosistema/cpe'))).catch(() => [] as ComprobanteApi[]),
        firstValueFrom(this.http.get<UsuarioApi[]>(apiUrl('ecosistema/usuarios'))).catch(() => null),
        this.pedidosApi.refrescar().catch(() => []),
      ]);
      this.fiscalSignal.set({
        ruc: fiscal.ruc,
        razonSocial: fiscal.razonSocial,
        nombreComercial: fiscal.nombreComercial,
        direccionFiscal: fiscal.direccionFiscal,
        ubigeo: fiscal.ubigeo,
        departamento: fiscal.departamento,
        provincia: fiscal.provincia,
        distrito: fiscal.distrito,
      });
      this.seriesSignal.set(
        series.map((item) => ({
          tipo: item.tipo,
          serie: item.serie,
          correlativo: Math.max(item.correlativo, 1),
          activa: item.activa,
        })),
      );
      this.webhooksSignal.set([mapWebhook(webhook)]);
      this.webhookLogsSignal.set((webhook.logs ?? []).map(mapWebhookLog));
      this.emisionesSignal.set(comprobantes.map((item) => this.mapEmision(item)));
      if (usuarios) {
        this.usuariosSignal.set(usuarios.map(mapUsuarioApi));
      }
      await this.refrescarNotasPendientes().catch(() => undefined);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async refrescarUsuarios(): Promise<UsuarioEmpresa[]> {
    try {
      const items = await firstValueFrom(
        this.http.get<UsuarioApi[]>(apiUrl('ecosistema/usuarios')),
      );
      const usuarios = items.map(mapUsuarioApi);
      this.usuariosSignal.set(usuarios);
      return usuarios.map(clonarUsuario);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async guardarUsuario(id: string | null, request: GuardarUsuarioRequest): Promise<UsuarioEmpresa> {
    const dni = request.dni.trim();
    const nombres = request.nombres.trim();
    const apellidos = request.apellidos.trim();
    const email = request.email.trim().toLowerCase();
    const password = request.password?.trim() ?? '';

    if (!/^\d{8}$/.test(dni)) {
      throw new Error('El DNI debe tener exactamente 8 dígitos.');
    }
    if (nombres.length < 2) {
      throw new Error('Indica los nombres (mínimo 2 caracteres).');
    }
    if (apellidos.length < 2) {
      throw new Error('Indica los apellidos (mínimo 2 caracteres).');
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      throw new Error('Indica un correo / usuario de acceso válido.');
    }
    if (!id && password.length < 8) {
      throw new Error('La contraseña es obligatoria (mínimo 8 caracteres).');
    }
    if (id && password.length > 0 && password.length < 8) {
      throw new Error('Si cambias la contraseña, debe tener al menos 8 caracteres.');
    }

    try {
      const body = {
        dni,
        nombres,
        apellidos,
        email,
        rol: request.rol,
        activo: request.activo,
        ...(password.length > 0 ? { password } : id ? {} : { password }),
      };
      const dto = id
        ? await firstValueFrom(
            this.http.put<UsuarioApi>(apiUrl(`ecosistema/usuarios/${id}`), {
              ...body,
              password: password.length > 0 ? password : null,
            }),
          )
        : await firstValueFrom(
            this.http.post<UsuarioApi>(apiUrl('ecosistema/usuarios'), {
              ...body,
              password,
            }),
          );
      const usuario = mapUsuarioApi(dto);
      this.usuariosSignal.update((items) => {
        const existe = items.some((item) => item.id === usuario.id);
        return existe
          ? items.map((item) => (item.id === usuario.id ? usuario : item))
          : [usuario, ...items];
      });
      return clonarUsuario(usuario);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async guardarFiscal(config: ConfiguracionFiscalEmpresa): Promise<ConfiguracionFiscalEmpresa> {
    const ruc = config.ruc.trim();
    if (!rucValido(ruc)) {
      throw new Error('El RUC debe tener 11 dígitos.');
    }
    if (!config.razonSocial.trim()) {
      throw new Error('Indica la razón social.');
    }
    if (!config.direccionFiscal.trim()) {
      throw new Error('Indica la dirección fiscal.');
    }
    if (!ubigeoValido(config.ubigeo)) {
      throw new Error('El UBIGEO debe tener 6 dígitos.');
    }

    const persistido: ConfiguracionFiscalEmpresa = {
      ruc,
      razonSocial: config.razonSocial.trim().toUpperCase(),
      nombreComercial: config.nombreComercial.trim() || config.razonSocial.trim(),
      direccionFiscal: config.direccionFiscal.trim().toUpperCase(),
      ubigeo: config.ubigeo.trim(),
      departamento: config.departamento.trim().toUpperCase() || 'LIMA',
      provincia: config.provincia.trim().toUpperCase() || 'LIMA',
      distrito: config.distrito.trim().toUpperCase() || 'LIMA',
    };
    try {
      await firstValueFrom(this.http.put(apiUrl('ecosistema/fiscal'), persistido));
    } catch (error) {
      throw new Error(readApiError(error));
    }
    this.fiscalSignal.set(persistido);
    return persistido;
  }

  guardarSeries(series: readonly SerieComprobante[]): SerieComprobante[] {
    const persistidas = TIPOS_COMPROBANTE.map((tipo) => {
      const actual = series.find((item) => item.tipo === tipo);
      const serie = (actual?.serie ?? SERIES_POR_DEFECTO[tipo]).trim().toUpperCase();
      const correlativo = Number(actual?.correlativo ?? 1);
      if (!prefijoSerieValido(tipo, serie)) {
        throw new Error(`La serie ${serie} no es válida para ${tipo}.`);
      }
      if (!Number.isInteger(correlativo) || correlativo < 1) {
        throw new Error(`El correlativo de ${serie} debe ser un entero mayor que cero.`);
      }
      return {
        tipo,
        serie,
        correlativo,
        activa: actual?.activa ?? true,
      };
    });
    this.seriesSignal.set(persistidas);
    return persistidas.map(clonarSerie);
  }

  guardarSede(id: string | null, request: GuardarSedeRequest): SedeEmpresa {
    const nombre = request.nombre.trim();
    if (nombre.length < 3) {
      throw new Error('El nombre de la sede debe tener al menos 3 caracteres.');
    }
    if (!request.direccion.trim()) {
      throw new Error('Indica la dirección de partida o llegada.');
    }
    if (request.ubigeo && !ubigeoValido(request.ubigeo)) {
      throw new Error('El UBIGEO de la sede debe tener 6 dígitos.');
    }

    const sede: SedeEmpresa = {
      id: id ?? `sede-${globalThis.crypto.randomUUID().slice(0, 8)}`,
      nombre,
      tipo: request.tipo,
      direccion: request.direccion.trim(),
      distrito: request.distrito.trim(),
      provincia: request.provincia.trim() || 'Lima',
      departamento: request.departamento.trim() || 'Lima',
      ubigeo: request.ubigeo.trim(),
      esPuntoPartidaGre: request.esPuntoPartidaGre,
      esPuntoLlegadaGre: request.esPuntoLlegadaGre,
      esAlmacenPrincipal: request.esAlmacenPrincipal,
      activa: request.activa,
    };

    this.sedesSignal.update((items) => {
      const sinPrincipal = sede.esAlmacenPrincipal
        ? items.map((item) => ({ ...item, esAlmacenPrincipal: false }))
        : items;
      const existe = sinPrincipal.some((item) => item.id === sede.id);
      return existe
        ? sinPrincipal.map((item) => (item.id === sede.id ? sede : item))
        : [sede, ...sinPrincipal];
    });
    return clonarSede(sede);
  }

  actualizarPermiso(rol: UsuarioEmpresa['rol'], modulo: ModuloPermiso, campo: 'lectura' | 'escritura', valor: boolean): void {
    this.permisosSignal.update((matriz) => ({
      ...matriz,
      [rol]: matriz[rol].map((permiso) => {
        if (permiso.modulo !== modulo) {
          return permiso;
        }
        if (campo === 'escritura') {
          return { ...permiso, escritura: valor, lectura: valor ? true : permiso.lectura };
        }
        return { ...permiso, lectura: valor, escritura: valor ? permiso.escritura : false };
      }),
    }));
  }

  async guardarWebhook(webhook: WebhookSalida): Promise<WebhookSalida> {
    const url = webhook.url.trim();
    if (!url.startsWith('https://') && !url.startsWith('http://')) {
      throw new Error('La URL del webhook debe comenzar con http:// o https://.');
    }
    try {
      const dto = await firstValueFrom(
        this.http.put<WebhookApi>(apiUrl('ecosistema/webhooks'), {
          nombre: webhook.nombre.trim() || 'WhatsApp pedidos y guías',
          url,
          token: webhook.token.trim(),
          eventos: webhook.eventos.length > 0 ? webhook.eventos : ['pedido.estado'],
          activo: webhook.activo,
        }),
      );
      const persistido = mapWebhook(dto);
      this.webhooksSignal.set([persistido]);
      this.webhookLogsSignal.set((dto.logs ?? []).map(mapWebhookLog));
      return clonarWebhook(persistido);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  documentosEmitibles(tipo: TipoComprobanteSunat): DocumentoEmitible[] {
    this.pedidosApi.pedidos();
    this.entregasApi.entregas();

    if (tipo === 'GUIA_REMISION') {
      return this.entregasApi
        .listar({ ...FILTROS_ENTREGAS_VACIOS })
        .filter(
          (fila) =>
            fila.entrega.fechaDespacho != null ||
            fila.entrega.estado === 'DESPACHADA' ||
            fila.entrega.estado === 'EN_TRANSITO' ||
            fila.entrega.estado === 'ENTREGADA',
        )
        .map((fila) => ({
          id: fila.entrega.id,
          origen: 'ENTREGA' as const,
          pedidoId: fila.pedido.id,
          entregaId: fila.entrega.id,
          ventaId: fila.pedido.ventaId,
          clienteNombre: fila.pedido.clienteNombre,
          total: fila.pedido.total,
          etiqueta: `${fila.pedido.codigo} · ${fila.pedido.clienteNombre} · ${fila.entrega.metodoEnvio}`,
        }));
    }

    return this.pedidosApi
      .listar(FILTROS_PEDIDOS_VACIOS)
      .filter((pedido) => {
        if (pedido.estado !== 'Entregado' || !pedido.ventaId) {
          return false;
        }
        if (tipo === 'FACTURA') {
          return pedido.clienteTipoDocumento === 'RUC';
        }
        return true;
      })
      .map((pedido) => ({
        id: pedido.ventaId as string,
        origen: 'PEDIDO' as const,
        pedidoId: pedido.id,
        entregaId: null,
        ventaId: pedido.ventaId,
        clienteNombre: pedido.clienteNombre,
        total: pedido.total,
        etiqueta: `${pedido.codigo} · ${pedido.clienteNombre} · S/ ${pedido.total.toFixed(2)}`,
        clienteTipoDocumento: pedido.clienteTipoDocumento ?? null,
      }));
  }

  async emitirPrueba(request: EmitirPruebaRequest): Promise<EmisionSimulada> {
    if (request.tipo === 'GUIA_REMISION' || request.tipo === 'NOTA_CREDITO') {
      throw new Error('Este sprint emite boleta o factura desde una venta. GRE y NC quedan fuera.');
    }
    if (!request.documentoId) {
      throw new Error('Selecciona un pedido Entregado con venta.');
    }

    try {
      const comprobante = await firstValueFrom(
        this.http.post<ComprobanteApi>(
          apiUrl(`ecosistema/cpe/emitir-desde-venta/${request.documentoId}`),
          {},
          { params: { tipo: request.tipo } },
        ),
      );
      const documentos = this.documentosEmitibles(request.tipo);
      const documento = documentos.find((item) => item.id === request.documentoId);
      const emision = this.mapEmision(comprobante, documento);
      this.emisionesSignal.update((items) => [emision, ...items.filter((item) => item.id !== emision.id)]);
      this.seriesSignal.update((items) =>
        items.map((item) =>
          item.tipo === emision.tipo ? { ...item, correlativo: emision.correlativo + 1 } : item,
        ),
      );
      this.notificar('pedido.estado', emision.clienteNombre, emision.mensaje);
      if (emision.tipo === 'NOTA_VENTA') {
        await this.refrescarNotasPendientes().catch(() => undefined);
      }
      return { ...emision };
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async obtenerPorVenta(ventaId: string): Promise<EmisionSimulada | null> {
    try {
      const comprobante = await firstValueFrom(
        this.http.get<ComprobanteApi>(apiUrl(`ecosistema/cpe/venta/${ventaId}`)),
      );
      const emision = this.mapEmision(comprobante);
      this.emisionesSignal.update((items) => [emision, ...items.filter((item) => item.id !== emision.id)]);
      return { ...emision };
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        return null;
      }
      throw new Error(readApiError(error));
    }
  }

  async emitirNotaCredito(
    ventaId: string,
    request: {
      codigoMotivo?: string;
      descripcionMotivo?: string;
      items?: Array<{ productoId: string; cantidad: number; ventaDetalleId?: string }>;
    } = {},
  ): Promise<EmisionSimulada> {
    if (!ventaId.trim()) {
      throw new Error('La venta es obligatoria para emitir la nota de crédito.');
    }
    try {
      const body: Record<string, unknown> = {
        codigoMotivo: request.codigoMotivo?.trim() || '01',
        descripcionMotivo:
          request.descripcionMotivo?.trim() || 'Anulación de la operación',
      };
      if (request.items && request.items.length > 0) {
        body['items'] = request.items.map((item) => ({
          productoId: item.productoId,
          cantidad: item.cantidad,
          ...(item.ventaDetalleId ? { ventaDetalleId: item.ventaDetalleId } : {}),
        }));
      }
      const comprobante = await firstValueFrom(
        this.http.post<ComprobanteApi>(apiUrl(`ecosistema/cpe/nota-credito/${ventaId}`), body),
      );
      const emision = this.mapEmision(comprobante);
      this.emisionesSignal.update((items) => [emision, ...items.filter((item) => item.id !== emision.id)]);
      this.seriesSignal.update((items) =>
        items.map((item) =>
          item.tipo === emision.tipo ? { ...item, correlativo: emision.correlativo + 1 } : item,
        ),
      );
      this.notificar('pedido.estado', emision.clienteNombre, emision.mensaje);
      return { ...emision };
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async refrescarNotasPendientes(fecha?: string): Promise<NotaVentaPendiente[]> {
    try {
      const items = await firstValueFrom(
        this.http.get<NotaVentaPendienteApi[]>(apiUrl('ecosistema/cpe/notas-venta-pendientes'), {
          params: fecha ? { fecha } : undefined,
        }),
      );
      const notas = items.map(mapNotaPendiente);
      this.notasPendientesSignal.set(notas);
      return notas;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async generarBoletaConsolidada(
    filtro: FiltroBoletaConsolidada,
    fecha: string,
    notaVentaIds: readonly string[],
  ): Promise<BoletaConsolidadaResultado> {
    try {
      const dto = await firstValueFrom(
        this.http.post<BoletaConsolidadaApi>(apiUrl('ecosistema/cpe/boleta-consolidada'), {
          filtro,
          fecha,
          notaVentaIds,
        }),
      );
      const comprobante = this.mapEmision(dto.comprobante);
      this.emisionesSignal.update((items) => [comprobante, ...items.filter((item) => item.id !== comprobante.id)]);
      this.seriesSignal.update((items) =>
        items.map((item) =>
          item.tipo === comprobante.tipo ? { ...item, correlativo: comprobante.correlativo + 1 } : item,
        ),
      );
      await this.refrescarNotasPendientes(fecha).catch(() => undefined);
      return {
        id: dto.id,
        filtro: dto.filtro,
        fechaOperacion: dto.fechaOperacion,
        cantidadNotas: dto.cantidadNotas,
        total: dto.total,
        comprobante,
        notas: dto.notas.map(mapNotaPendiente),
      };
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async descargarXml(emision: EmisionSimulada): Promise<void> {
    await this.descargarArchivo(emision.id, 'xml', `${numeroComprobante(emision)}.xml`);
  }

  async descargarCdr(emision: EmisionSimulada): Promise<void> {
    await this.descargarArchivo(emision.id, 'cdr', `R-${numeroComprobante(emision)}.xml`);
  }

  async abrirPdf(emision: EmisionSimulada, formato: 'a4' | 'ticket' = 'a4'): Promise<void> {
    try {
      const blob = await firstValueFrom(
        this.http.get(apiUrl(`ecosistema/cpe/${emision.id}/pdf`), {
          params: { formato },
          responseType: 'blob',
        }),
      );
      const url = URL.createObjectURL(blob);
      const abierta = window.open(url, '_blank', 'noopener');
      if (!abierta) {
        const enlace = document.createElement('a');
        enlace.href = url;
        enlace.download = `${numeroComprobante(emision)}${formato === 'ticket' ? '-ticket80' : ''}.pdf`;
        enlace.click();
      }
      globalThis.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  private async descargarArchivo(id: string, tipo: 'xml' | 'cdr', nombre: string): Promise<void> {
    try {
      const blob = await firstValueFrom(
        this.http.get(apiUrl(`ecosistema/cpe/${id}/${tipo}`), { responseType: 'blob' }),
      );
      const url = URL.createObjectURL(blob);
      const enlace = document.createElement('a');
      enlace.href = url;
      enlace.download = nombre;
      enlace.click();
      URL.revokeObjectURL(url);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  private mapEmision(comprobante: ComprobanteApi, documento?: DocumentoEmitible): EmisionSimulada {
    return {
      id: comprobante.id,
      tipo: comprobante.tipo,
      serie: comprobante.serie,
      correlativo: comprobante.correlativo,
      estado: comprobante.estado,
      pedidoId: documento?.pedidoId ?? '',
      ventaId: comprobante.ventaId,
      clienteNombre: documento?.clienteNombre ?? comprobante.clienteNombre ?? '',
      total: documento?.total ?? comprobante.total ?? 0,
      mensaje:
        comprobante.mensaje ||
        `${etiquetaCorta(comprobante.tipo)} ${numeroComprobante(comprobante)} · ${comprobante.estado}`,
      fecha: comprobante.fechaEmision,
      hashFirma: comprobante.hashFirma ?? null,
      tieneXml: comprobante.tieneXml === true,
      tieneCdr: comprobante.tieneCdr === true,
      tienePdf: comprobante.tienePdf !== false,
      boletaConsolidadaId: comprobante.boletaConsolidadaId ?? null,
      codigoMotivo: comprobante.codigoMotivo ?? null,
      descripcionMotivo: comprobante.descripcionMotivo ?? null,
      documentoReferencia: comprobante.documentoReferencia ?? null,
    };
  }

  async probarWebhook(evento: EventoWebhook): Promise<WebhookLog> {
    try {
      const dto = await firstValueFrom(
        this.http.post<WebhookLogApi>(apiUrl('ecosistema/webhooks/probar'), { evento }),
      );
      const log = mapWebhookLog(dto);
      this.webhookLogsSignal.update((items) => [log, ...items].slice(0, 20));
      return log;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  private notificar(evento: EventoWebhook, destino: string, resumen: string): WebhookLog {
    const webhook = this.webhooksSignal().find((item) => item.activo);
    const log: WebhookLog = {
      id: globalThis.crypto.randomUUID(),
      webhookId: webhook?.id ?? 'wh-whatsapp',
      evento,
      destino,
      payloadResumen: resumen,
      estado: !webhook
        ? 'OMITIDO'
        : webhook.eventos.includes(evento)
          ? 'ENVIADO'
          : 'OMITIDO',
      fecha: new Date().toISOString(),
    };
    this.webhookLogsSignal.update((items) => [log, ...items].slice(0, 20));
    return log;
  }
}

function mapNotaPendiente(dto: NotaVentaPendienteApi): NotaVentaPendiente {
  return {
    id: dto.id,
    ventaId: dto.ventaId,
    serie: dto.serie,
    correlativo: dto.correlativo,
    estado: dto.estado,
    clienteNombre: dto.clienteNombre,
    total: dto.total,
    fechaEmision: dto.fechaEmision,
    esVentaMenor: dto.esVentaMenor,
  };
}

function mapWebhook(dto: WebhookApi): WebhookSalida {
  return {
    id: dto.id,
    nombre: dto.nombre,
    url: dto.url,
    token: dto.token,
    canal: dto.canal || 'WHATSAPP',
    eventos: dto.eventos?.length ? dto.eventos : ['pedido.estado'],
    activo: dto.activo,
  };
}

function mapUsuarioApi(dto: UsuarioApi): UsuarioEmpresa {
  const nombres = dto.nombres?.trim() || '';
  const apellidos = dto.apellidos?.trim() || '';
  const nombre =
    dto.nombre?.trim() ||
    `${nombres} ${apellidos}`.trim() ||
    dto.email;
  return {
    id: dto.id,
    dni: dto.dni ?? '',
    nombres,
    apellidos,
    nombre,
    email: dto.email,
    rol: dto.rol,
    activo: dto.activo,
    fechaCreacion: dto.fechaCreacion,
  };
}

function mapWebhookLog(dto: WebhookLogApi): WebhookLog {
  return {
    id: dto.id,
    webhookId: dto.webhookId,
    evento: dto.evento,
    destino: dto.destino,
    payloadResumen: dto.payloadResumen,
    estado: dto.estado,
    fecha: dto.fecha,
  };
}

function etiquetaCorta(tipo: TipoComprobanteSunat): string {
  switch (tipo) {
    case 'BOLETA':
      return 'Boleta';
    case 'FACTURA':
      return 'Factura';
    case 'NOTA_CREDITO':
      return 'Nota de crédito';
    case 'GUIA_REMISION':
      return 'GRE remitente';
    case 'NOTA_VENTA':
      return 'Nota de venta';
  }
}

function clonarSerie(item: SerieComprobante): SerieComprobante {
  return { ...item };
}

function clonarSede(item: SedeEmpresa): SedeEmpresa {
  return { ...item };
}

function clonarUsuario(item: UsuarioEmpresa): UsuarioEmpresa {
  return { ...item };
}

function clonarWebhook(item: WebhookSalida): WebhookSalida {
  return { ...item, eventos: [...item.eventos] };
}

function clonarMatriz(matriz: MatrizPermisos): MatrizPermisos {
  return {
    ADMIN: matriz.ADMIN.map((item) => ({ ...item })),
    CAJERO: matriz.CAJERO.map((item) => ({ ...item })),
    ALMACEN: matriz.ALMACEN.map((item) => ({ ...item })),
  };
}

const SEED_FISCAL: ConfiguracionFiscalEmpresa = {
  ruc: '20123456789',
  razonSocial: 'TRUNQI TCG SAC',
  nombreComercial: 'TRUNQI',
  direccionFiscal: 'AV. JOSE LARCO 1230 INT. 4',
  ubigeo: '150122',
  departamento: 'LIMA',
  provincia: 'LIMA',
  distrito: 'MIRAFLORES',
};

const SEED_SERIES: SerieComprobante[] = [
  { tipo: 'BOLETA', serie: 'B001', correlativo: 45821, activa: true },
  { tipo: 'FACTURA', serie: 'F001', correlativo: 1204, activa: true },
  { tipo: 'NOTA_VENTA', serie: 'NV01', correlativo: 1, activa: true },
  { tipo: 'NOTA_CREDITO', serie: 'FC01', correlativo: 18, activa: true },
  { tipo: 'GUIA_REMISION', serie: 'T001', correlativo: 310, activa: true },
];

const SEED_SEDES: SedeEmpresa[] = [
  {
    id: 'sede-mira',
    nombre: 'Tienda Miraflores',
    tipo: 'TIENDA',
    direccion: 'Av. José Larco 1230, interior 4',
    distrito: 'Miraflores',
    provincia: 'Lima',
    departamento: 'Lima',
    ubigeo: '150122',
    esPuntoPartidaGre: true,
    esPuntoLlegadaGre: true,
    esAlmacenPrincipal: false,
    activa: true,
  },
  {
    id: 'sede-surco',
    nombre: 'Almacén Surco',
    tipo: 'ALMACEN',
    direccion: 'Av. El Polo 710, almacén 2',
    distrito: 'Santiago de Surco',
    provincia: 'Lima',
    departamento: 'Lima',
    ubigeo: '150140',
    esPuntoPartidaGre: true,
    esPuntoLlegadaGre: false,
    esAlmacenPrincipal: true,
    activa: true,
  },
];

const SEED_USUARIOS: UsuarioEmpresa[] = [
  {
    id: 'usr-admin',
    dni: '00000001',
    nombres: 'Yhort',
    apellidos: 'Cruz',
    nombre: 'Yhort Cruz',
    email: 'yhort@trunqi.pe',
    rol: 'ADMIN',
    activo: true,
  },
  {
    id: 'usr-caja',
    dni: '00000002',
    nombres: 'Caja',
    apellidos: 'Miraflores',
    nombre: 'Caja Miraflores',
    email: 'caja@trunqi.pe',
    rol: 'CAJERO',
    activo: true,
  },
  {
    id: 'usr-almacen',
    dni: '00000003',
    nombres: 'Logística',
    apellidos: 'Surco',
    nombre: 'Logística Surco',
    email: 'almacen@trunqi.pe',
    rol: 'ALMACEN',
    activo: true,
  },
];

const SEED_WEBHOOKS: WebhookSalida[] = [
  {
    id: 'wh-whatsapp',
    nombre: 'WhatsApp · pedidos y guías',
    url: 'https://hooks.trunqi.pe/whatsapp',
    token: 'whsec_trunqi_demo',
    canal: 'WHATSAPP',
    eventos: ['pedido.estado', 'guia.estado'],
    activo: true,
  },
];
