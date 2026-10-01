import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { SedesApiService } from '../../../core/data-access/sedes-api.service';
import { apiUrl } from '../../../core/http/api-url';
import { codigoPedido } from '../../../core/ui/codigo-amigable';
import { PedidosDigitalesApiService } from '../../pedidos-digitales/data-access/pedidos-digitales.service';
import { PedidoDigital } from '../../pedidos-digitales/models/pedido-digital.model';
import {
  DespacharEntregaRequest,
  EmpaquetarEntregaRequest,
  Entrega,
  EntregaFila,
  EntregasFiltros,
  EstadoLogistica,
  MetodoEnvio,
  metodoDesdeSnapshot,
  nombreSedeDe,
  remitenteDe,
  snapshotDesdeMetodo,
} from '../models/entrega.model';

/** Rango de fechas opcional para GET /api/entregas y pedidos (`fechaDesde` / `fechaHasta`). */
export interface EntregasRangoFechas {
  desde?: string | null;
  hasta?: string | null;
}

interface EntregaApi {
  id: string;
  pedidoDigitalId: string;
  sedeOrigenId: string;
  metodoEnvio: MetodoEnvio;
  estado: EstadoLogistica;
  destinatarioNombre: string;
  destinatarioTelefono?: string | null;
  direccion?: string | null;
  distrito?: string | null;
  provincia?: string | null;
  departamento?: string | null;
  agencia?: string | null;
  puntoEntrega?: string | null;
  canalContacto?: Entrega['canalContacto'];
  contactoReferencia?: string | null;
  numeroTracking?: string | null;
  costoEnvio: number;
  notasEmpaque?: string | null;
  fechaProgramada?: string | null;
  fechaDespacho?: string | null;
  fechaEntrega?: string | null;
  observacion?: string | null;
}

@Injectable({ providedIn: 'root' })
export class EntregasApiService {
  private readonly http = inject(HttpClient);
  private readonly entregasSignal = signal<Entrega[]>(SEED_ENTREGAS.map(clonar));
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly sedesApi = inject(SedesApiService);

  readonly entregas = this.entregasSignal.asReadonly();
  readonly sedes = this.sedesApi.sedes;

  async refrescar(rango?: EntregasRangoFechas): Promise<void> {
    const desde = rango?.desde?.trim() || null;
    const hasta = rango?.hasta?.trim() || null;

    await Promise.all([
      this.sedesApi.refrescar(),
      this.pedidosApi.refrescar({ desde, hasta }),
      this.refrescarEntregasApi({ desde, hasta }).catch(() => undefined),
    ]);
  }

  listar(filtros: EntregasFiltros): EntregaFila[] {
    this.pedidosApi.pedidos();
    this.sedesApi.sedes();
    this.entregasSignal();
    const query = filtros.busqueda.trim().toLowerCase();
    const desde = filtros.desde ? Date.parse(`${filtros.desde}T00:00:00`) : null;
    const hasta = filtros.hasta ? Date.parse(`${filtros.hasta}T23:59:59.999`) : null;

    return this.pedidosApi
      .listarParaDespacho()
      .map((pedido) => this.toFila(pedido))
      .filter((fila) => {
        if (filtros.metodoEnvio !== 'TODOS' && fila.entrega.metodoEnvio !== filtros.metodoEnvio) {
          return false;
        }
        if (filtros.estado !== 'TODOS' && fila.entrega.estado !== filtros.estado) {
          return false;
        }
        if (filtros.sedeId !== 'TODAS' && fila.entrega.sedeOrigenId !== filtros.sedeId) {
          return false;
        }
        const fechaOperativa = fechaOperativaFila(fila);
        if (desde && fechaOperativa < desde) {
          return false;
        }
        if (hasta && fechaOperativa > hasta) {
          return false;
        }
        if (!query) {
          return true;
        }
        return `${fila.pedido.codigo} ${fila.pedido.id} ${fila.pedido.clienteNombre} ${fila.entrega.numeroTracking ?? ''} ${fila.entrega.agencia ?? ''} ${fila.entrega.puntoEntrega ?? ''} ${fila.entrega.destinatarioTelefono ?? ''} ${fila.entrega.contactoReferencia ?? ''}`
          .toLowerCase()
          .includes(query);
      });
  }

  obtenerPorPedido(pedidoId: string): Entrega | undefined {
    const encontrada = this.entregasSignal().find((item) => item.pedidoDigitalId === pedidoId);
    return encontrada ? clonar(encontrada) : undefined;
  }

  remitente(sedeId: string) {
    return remitenteDe(sedeId);
  }

  /** KPIs sobre el conjunto ya filtrado. */
  kpis(filas: readonly EntregaFila[]) {
    return {
      porEmpaquetar: filas.filter((fila) => fila.pedido.estado === 'Pagado').length,
      porDespachar: filas.filter((fila) => fila.pedido.estado === 'Empaquetado').length,
      enTransito: filas.filter((fila) => fila.pedido.estado === 'PendienteEntrega').length,
    };
  }

  private async refrescarEntregasApi(rango: EntregasRangoFechas): Promise<void> {
    let params = new HttpParams();
    const desde = rango.desde?.trim();
    const hasta = rango.hasta?.trim();
    if (desde) {
      params = params.set('fechaDesde', desde);
    }
    if (hasta) {
      params = params.set('fechaHasta', hasta);
    }
    const items = await firstValueFrom(
      this.http.get<EntregaApi[]>(apiUrl('entregas'), { params }),
    );
    this.entregasSignal.set(items.map(mapEntregaApi));
  }

  empaquetar(pedidoId: string, request: EmpaquetarEntregaRequest): Entrega {
    const pedido = this.requierePedido(pedidoId);
    if (pedido.estado !== 'Pagado' && pedido.estado !== 'Empaquetado') {
      throw new Error('Solo se empaqueta un pedido Pagado (o se actualizan notas en Empaquetado).');
    }

    const notas = request.notasEmpaque.trim();
    if (notas.length < 3) {
      throw new Error('Registra una nota de empaque (mínimo 3 caracteres).');
    }

    const entrega = this.asegurar(pedido);
    if (pedido.estado === 'Pagado') {
      void this.pedidosApi.cambiarEstado(pedido.id, 'Empaquetado', `Empaque: ${notas}`);
    }

    const siguiente: Entrega = {
      ...entrega,
      estado: entrega.estado === 'PROGRAMADA' ? 'PROGRAMADA' : entrega.estado,
      notasEmpaque: notas,
      fechaProgramada: entrega.fechaProgramada ?? new Date().toISOString(),
      observacion: notas,
    };
    this.persistir(siguiente);
    this.pedidosApi.actualizarSnapshotEntrega(pedido.id, { notasEmpaque: notas });
    return clonar(siguiente);
  }

  despachar(pedidoId: string, request: DespacharEntregaRequest): Entrega {
    const pedido = this.requierePedido(pedidoId);
    if (pedido.estado !== 'Empaquetado' && pedido.estado !== 'PendienteEntrega') {
      throw new Error('Despacha desde Empaquetado o actualiza el tracking en Pendiente de entrega.');
    }

    const costo = Number(request.costoEnvio);
    if (!Number.isFinite(costo) || costo < 0) {
      throw new Error('El costo de envío no puede ser negativo.');
    }

    const tracking = textoOpcional(request.numeroTracking, 80);
    if (request.metodoEnvio !== 'RECOJO_TIENDA' && !tracking) {
      throw new Error('Indica el N° de tracking o la clave de recojo de la agencia.');
    }

    const entrega = this.asegurar(pedido);
    if (pedido.estado === 'Empaquetado') {
      void this.pedidosApi.cambiarEstado(
        pedido.id,
        'PendienteEntrega',
        request.metodoEnvio === 'RECOJO_TIENDA'
          ? 'Listo para recojo en tienda.'
          : `Despachado · ${request.metodoEnvio}${tracking ? ` · ${tracking}` : ''}`,
      );
    }

    const ahora = new Date().toISOString();
    const siguiente: Entrega = {
      ...entrega,
      ...this.destinoDesdePedido(pedido),
      metodoEnvio: request.metodoEnvio,
      numeroTracking: tracking ?? (request.metodoEnvio === 'RECOJO_TIENDA' ? `RECOJO-${codigoPedido(pedido)}` : null),
      agencia: textoOpcional(request.agencia, 80),
      costoEnvio: costo,
      estado: request.metodoEnvio === 'RECOJO_TIENDA' ? 'PROGRAMADA' : 'DESPACHADA',
      fechaDespacho: ahora,
      observacion: textoOpcional(request.observacion, 500) ?? entrega.observacion,
    };
    this.persistir(siguiente);
    this.pedidosApi.actualizarSnapshotEntrega(pedido.id, {
      ...snapshotDesdeMetodo(request.metodoEnvio),
      numeroTracking: siguiente.numeroTracking,
      costoEnvio: siguiente.costoEnvio,
      agencia: siguiente.agencia,
    });
    return clonar(siguiente);
  }

  async confirmar(pedidoId: string): Promise<Entrega> {
    const pedido = this.requierePedido(pedidoId);
    await this.pedidosApi.convertirVenta(pedido.id, 'Entrega confirmada desde despacho. Venta y kardex VENTA.');

    const entrega = this.asegurar(pedido);
    const siguiente: Entrega = {
      ...entrega,
      estado: 'ENTREGADA',
      fechaEntrega: new Date().toISOString(),
    };
    this.persistir(siguiente);
    return clonar(siguiente);
  }

  private toFila(pedido: PedidoDigital): EntregaFila {
    const entrega = this.obtenerPorPedido(pedido.id) ?? this.derivar(pedido);
    return {
      pedido,
      entrega,
      sedeNombre: nombreSedeDe(pedido, this.sedesApi.sedes()),
    };
  }

  /** Construye fila de ticket/etiqueta a partir de un pedido recién creado. */
  filaDesdePedido(pedido: PedidoDigital): EntregaFila {
    return this.toFila(pedido);
  }

  private asegurar(pedido: PedidoDigital): Entrega {
    const existente = this.obtenerPorPedido(pedido.id);
    if (existente) {
      return existente;
    }
    const creada = this.derivar(pedido);
    this.persistir(creada);
    return creada;
  }

  private derivar(pedido: PedidoDigital): Entrega {
    const snap = pedido.entrega;
    return {
      id: `ent-${pedido.id}`,
      pedidoDigitalId: pedido.id,
      sedeOrigenId: pedido.sedeId,
      metodoEnvio: metodoDesdeSnapshot(snap),
      estado: estadoLogisticaDesdePedido(pedido.estado),
      destinatarioNombre: snap.destinatarioNombre,
      destinatarioTelefono: snap.destinatarioTelefono ?? pedido.clienteTelefono,
      direccion: snap.direccion,
      distrito: snap.distrito,
      provincia: snap.provincia,
      departamento: snap.departamento,
      agencia: snap.agencia ?? snap.puntoEntrega ?? null,
      puntoEntrega: snap.puntoEntrega ?? snap.agencia ?? null,
      canalContacto: snap.canalContacto ?? null,
      contactoReferencia: snap.contactoReferencia ?? null,
      numeroTracking: snap.numeroTracking ?? null,
      costoEnvio: snap.costoEnvio ?? 0,
      notasEmpaque: snap.notasEmpaque ?? null,
      fechaProgramada: null,
      fechaDespacho: null,
      fechaEntrega: null,
      observacion: pedido.observacion,
    };
  }

  private destinoDesdePedido(pedido: PedidoDigital): Pick<
    Entrega,
    | 'destinatarioNombre'
    | 'destinatarioTelefono'
    | 'direccion'
    | 'distrito'
    | 'provincia'
    | 'departamento'
    | 'sedeOrigenId'
  > {
    const snap = pedido.entrega;
    return {
      sedeOrigenId: pedido.sedeId,
      destinatarioNombre: snap.destinatarioNombre,
      destinatarioTelefono: snap.destinatarioTelefono ?? pedido.clienteTelefono,
      direccion: snap.direccion,
      distrito: snap.distrito,
      provincia: snap.provincia,
      departamento: snap.departamento,
    };
  }

  private requierePedido(id: string): PedidoDigital {
    const pedido = this.pedidosApi.obtener(id);
    if (!pedido) {
      throw new Error('No se encontró el pedido digital.');
    }
    return pedido;
  }

  private persistir(entrega: Entrega): void {
    this.entregasSignal.update((items) => {
      const existe = items.some((item) => item.id === entrega.id || item.pedidoDigitalId === entrega.pedidoDigitalId);
      return existe
        ? items.map((item) =>
            item.pedidoDigitalId === entrega.pedidoDigitalId ? entrega : item,
          )
        : [entrega, ...items];
    });
  }
}

function clonar(entrega: Entrega): Entrega {
  return { ...entrega };
}

function mapEntregaApi(dto: EntregaApi): Entrega {
  return {
    id: dto.id,
    pedidoDigitalId: dto.pedidoDigitalId,
    sedeOrigenId: dto.sedeOrigenId,
    metodoEnvio: dto.metodoEnvio,
    estado: dto.estado,
    destinatarioNombre: dto.destinatarioNombre,
    destinatarioTelefono: dto.destinatarioTelefono ?? null,
    direccion: dto.direccion ?? null,
    distrito: dto.distrito ?? null,
    provincia: dto.provincia ?? null,
    departamento: dto.departamento ?? null,
    agencia: dto.agencia ?? null,
    puntoEntrega: dto.puntoEntrega ?? null,
    canalContacto: dto.canalContacto ?? null,
    contactoReferencia: dto.contactoReferencia ?? null,
    numeroTracking: dto.numeroTracking ?? null,
    costoEnvio: dto.costoEnvio,
    notasEmpaque: dto.notasEmpaque ?? null,
    fechaProgramada: dto.fechaProgramada ?? null,
    fechaDespacho: dto.fechaDespacho ?? null,
    fechaEntrega: dto.fechaEntrega ?? null,
    observacion: dto.observacion ?? null,
  };
}

/** Fecha operativa: despacho si existe; si no, pedido (creación operativa del día). */
function fechaOperativaFila(fila: EntregaFila): number {
  const iso = fila.entrega.fechaDespacho ?? fila.pedido.fechaPedido;
  return new Date(iso).getTime();
}

function textoOpcional(valor: string | null | undefined, max: number): string | null {
  const texto = valor?.trim() ?? '';
  return texto.length === 0 ? null : texto.slice(0, max);
}

function estadoLogisticaDesdePedido(estado: PedidoDigital['estado']): EstadoLogistica {
  switch (estado) {
    case 'PendienteEntrega':
      return 'DESPACHADA';
    case 'Entregado':
      return 'ENTREGADA';
    default:
      return 'PROGRAMADA';
  }
}

const SEED_ENTREGAS: Entrega[] = [
  {
    id: 'ent-050',
    pedidoDigitalId: 'ped-050',
    sedeOrigenId: 'sede-mira',
    metodoEnvio: 'SHALOM',
    estado: 'PROGRAMADA',
    destinatarioNombre: 'Ana Paredes',
    destinatarioTelefono: '912345678',
    direccion: 'Jr. Schell 365',
    distrito: 'Miraflores',
    provincia: 'Lima',
    departamento: 'Lima',
    agencia: 'Shalom Miraflores',
    puntoEntrega: 'Papurris',
    canalContacto: 'WHATSAPP',
    contactoReferencia: 'Luis Paredes · 912111222',
    numeroTracking: null,
    costoEnvio: 12,
    notasEmpaque: null,
    fechaProgramada: null,
    fechaDespacho: null,
    fechaEntrega: null,
    observacion: 'Pendiente de empaque · ETB',
  },
  {
    id: 'ent-051',
    pedidoDigitalId: 'ped-051',
    sedeOrigenId: 'sede-mira',
    metodoEnvio: 'OLVA_COURIER',
    estado: 'PROGRAMADA',
    destinatarioNombre: 'Mario R.',
    destinatarioTelefono: '998877665',
    direccion: 'Calle Schell 120, oficina 3',
    distrito: 'Miraflores',
    provincia: 'Lima',
    departamento: 'Lima',
    agencia: null,
    puntoEntrega: 'Dapkris',
    canalContacto: 'FACEBOOK',
    contactoReferencia: null,
    numeroTracking: null,
    costoEnvio: 14,
    notasEmpaque: 'Toploader + sobre burbuja. No doblar.',
    fechaProgramada: '2026-08-21T11:20:00-05:00',
    fechaDespacho: null,
    fechaEntrega: null,
    observacion: 'Empaquetado, falta tracking Olva.',
  },
  {
    id: 'ent-052',
    pedidoDigitalId: 'ped-052',
    sedeOrigenId: 'sede-mira',
    metodoEnvio: 'RECOJO_TIENDA',
    estado: 'PROGRAMADA',
    destinatarioNombre: 'Diego Soto',
    destinatarioTelefono: '955443322',
    direccion: null,
    distrito: null,
    provincia: null,
    departamento: null,
    agencia: 'Tienda Miraflores',
    puntoEntrega: 'TCG House',
    canalContacto: 'MSN',
    contactoReferencia: 'Hermano · Diego Soto',
    numeroTracking: 'RECOJO-052',
    costoEnvio: 0,
    notasEmpaque: 'Bolsa de tienda, listo en mostrador.',
    fechaProgramada: '2026-08-21T12:25:00-05:00',
    fechaDespacho: '2026-08-21T12:26:00-05:00',
    fechaEntrega: null,
    observacion: 'Clave de recojo en caja.',
  },
];
