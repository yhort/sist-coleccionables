import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { codigoPedido } from '../../../core/ui/codigo-amigable';
import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { PedidosDigitalesApiService } from '../../pedidos-digitales/data-access/pedidos-digitales.service';
import { PedidoDigital } from '../../pedidos-digitales/models/pedido-digital.model';
import {
  EstadoPago,
  ORIGENES_PAGO,
  OrigenPago,
  Pago,
  PagosFiltros,
  PagosKpis,
  RegistrarPagoLoteRequest,
  RegistrarPagoRequest,
  esOrigenDigital,
  normalizarCodigoOperacion,
  round2,
} from '../models/pago.model';

const USUARIO_PAGOS = 'Caja';

interface PagoApi {
  id: string;
  origen: OrigenPago;
  estado: EstadoPago;
  monto: number;
  codigoOperacion?: string | null;
  referenciaExterna?: string | null;
  pedidoDigitalId?: string | null;
  pedidoCodigo?: string | null;
  ventaId?: string | null;
  clienteNombre?: string | null;
  fechaNotificacion: string;
  fechaConfirmacion?: string | null;
  usuarioAsocioNombre?: string | null;
  observacion?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PagosApiService {
  private readonly pagosSignal = signal<Pago[]>(SEED_PAGOS.map(clonar));
  private readonly pedidosApi = inject(PedidosDigitalesApiService);
  private readonly http = inject(HttpClient);

  readonly pagos = this.pagosSignal.asReadonly();

  /** Pagos asociados a un pedido digital (API real). */
  async listarPorPedido(pedidoDigitalId: string): Promise<Pago[]> {
    try {
      const items = await firstValueFrom(
        this.http.get<PagoApi[]>(apiUrl('pagos'), {
          params: { pedidoDigitalId },
        }),
      );
      return items.map(mapPagoApi);
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  listar(filtros: PagosFiltros): Pago[] {
    const query = filtros.busqueda.trim().toLowerCase();

    return this.pagosSignal()
      .filter((pago) => {
        if (filtros.origen !== 'TODOS' && pago.origen !== filtros.origen) {
          return false;
        }
        if (filtros.estado !== 'TODOS' && pago.estado !== filtros.estado) {
          return false;
        }
        if (filtros.montoMin !== null && pago.monto < filtros.montoMin) {
          return false;
        }
        if (filtros.montoMax !== null && pago.monto > filtros.montoMax) {
          return false;
        }
        if (!query) {
          return true;
        }
        const cliente = this.clienteDe(pago).toLowerCase();
        const codigo = (pago.codigoOperacion ?? '').toLowerCase();
        return cliente.includes(query) || codigo.includes(query);
      })
      .map(clonar)
      .sort(
        (a, b) =>
          new Date(b.fechaNotificacion).getTime() - new Date(a.fechaNotificacion).getTime(),
      );
  }

  obtener(id: string): Pago | undefined {
    const encontrado = this.pagosSignal().find((item) => item.id === id);
    return encontrado ? clonar(encontrado) : undefined;
  }

  codigoOperacionDuplicado(codigo: string, excluirId?: string): boolean {
    const normalizado = normalizarCodigoOperacion(codigo);
    if (!normalizado) {
      return false;
    }
    return this.pagosSignal().some(
      (pago) =>
        pago.id !== excluirId &&
        pago.estado !== 'RECHAZADO' &&
        normalizarCodigoOperacion(pago.codigoOperacion) === normalizado,
    );
  }

  clienteDe(pago: Pago): string {
    if (pago.pedidoDigitalId) {
      const pedido = this.pedidosApi.obtener(pago.pedidoDigitalId);
      if (pedido) {
        return pedido.clienteNombre;
      }
    }
    return pago.clienteNombre?.trim() || 'Sin cliente';
  }

  pedidoDe(pago: Pago): PedidoDigital | undefined {
    return pago.pedidoDigitalId ? this.pedidosApi.obtener(pago.pedidoDigitalId) : undefined;
  }

  pedidosPendientes(busqueda = ''): PedidoDigital[] {
    const query = busqueda.trim().toLowerCase();
    return this.pedidosApi.listarPendientePago().filter((pedido) => {
      if (!query) {
        return true;
      }
      return `${pedido.clienteNombre} ${pedido.codigo} ${pedido.id} ${pedido.referenciaExterna ?? ''}`
        .toLowerCase()
        .includes(query);
    });
  }

  montoCubierto(pedidoId: string, estados: readonly EstadoPago[] = ['ASOCIADO', 'CONFIRMADO']): number {
    return round2(
      this.pagosSignal()
        .filter(
          (pago) =>
            pago.pedidoDigitalId === pedidoId && estados.includes(pago.estado),
        )
        .reduce((sum, pago) => sum + pago.monto, 0),
    );
  }

  saldoPendiente(pedido: PedidoDigital): number {
    return round2(Math.max(0, pedido.total - this.montoCubierto(pedido.id)));
  }

  kpis(): PagosKpis {
    const pagos = this.pagosSignal();
    const desglose = Object.fromEntries(ORIGENES_PAGO.map((origen) => [origen, 0])) as Record<
      OrigenPago,
      number
    >;
    let recaudadoHoy = 0;
    let cantidadHoy = 0;
    let pendientesConciliar = 0;
    let montoPendienteConciliar = 0;

    for (const pago of pagos) {
      if (pago.estado === 'CONFIRMADO') {
        desglose[pago.origen] = round2(desglose[pago.origen] + pago.monto);
        if (esMismoDiaLocal(pago.fechaConfirmacion ?? pago.fechaNotificacion)) {
          recaudadoHoy = round2(recaudadoHoy + pago.monto);
          cantidadHoy += 1;
        }
      }
      if (esOrigenDigital(pago.origen) && !pago.pedidoDigitalId && pago.estado !== 'RECHAZADO') {
        pendientesConciliar += 1;
        montoPendienteConciliar = round2(montoPendienteConciliar + pago.monto);
      }
    }

    return { recaudadoHoy, cantidadHoy, pendientesConciliar, montoPendienteConciliar, desglose };
  }

  async registrarLote(request: RegistrarPagoLoteRequest): Promise<Pago[]> {
    const ids = [...new Set(request.pedidoDigitalIds.filter((id) => id.trim().length > 0))];
    if (ids.length === 0) {
      throw new Error('Selecciona al menos un pedido pendiente de pago.');
    }

    try {
      const items = await firstValueFrom(
        this.http.post<PagoApi[]>(apiUrl('pagos/lote'), {
          origen: request.origen,
          monto: round2(Number(request.monto)),
          codigoOperacion: textoOpcional(request.codigoOperacion, 80),
          referenciaExterna: textoOpcional(request.referenciaExterna, 120),
          observacion: textoOpcional(request.observacion, 500),
          confirmar: request.confirmar ?? true,
          pedidoDigitalIds: ids,
        }),
      );
      const pagos = items.map(mapPagoApi);
      this.pagosSignal.update((actuales) => [
        ...pagos,
        ...actuales.filter((pago) => pagos.every((creado) => creado.id !== pago.id)),
      ]);
      await this.pedidosApi.refrescar().catch(() => undefined);
      return pagos;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  registrar(request: RegistrarPagoRequest): Pago {
    const monto = round2(Number(request.monto));
    if (!Number.isFinite(monto) || monto <= 0) {
      throw new Error('El monto debe ser mayor que cero.');
    }

    const codigo = textoOpcional(request.codigoOperacion, 80);
    if (esOrigenDigital(request.origen) && !codigo) {
      throw new Error('Indica el código de operación Yape o Izipay.');
    }
    this.asegurarCodigoLibre(codigo);

    let clienteNombre = textoOpcional(request.clienteNombre, 160);
    let pedidoDigitalId: string | null = null;
    let estado: EstadoPago = 'NOTIFICADO';

    if (request.pedidoDigitalId) {
      const pedido = this.requierePedidoPendiente(request.pedidoDigitalId);
      this.asegurarNoExcedeTotal(pedido, monto);
      pedidoDigitalId = pedido.id;
      clienteNombre = pedido.clienteNombre;
      estado = 'ASOCIADO';
    }

    const ahora = new Date().toISOString();
    const pago: Pago = {
      id: globalThis.crypto.randomUUID(),
      origen: request.origen,
      estado,
      monto,
      codigoOperacion: codigo,
      referenciaExterna: textoOpcional(request.referenciaExterna, 120),
      pedidoDigitalId,
      pedidoCodigo: pedidoDigitalId ? codigoPedido(pedidoDigitalId) : null,
      ventaId: null,
      clienteNombre,
      fechaNotificacion: ahora,
      fechaConfirmacion: null,
      usuarioAsocioNombre: pedidoDigitalId ? USUARIO_PAGOS : null,
      observacion: textoOpcional(request.observacion, 500),
    };

    this.pagosSignal.update((items) => [pago, ...items]);

    if (request.confirmar) {
      return this.confirmar(pago.id);
    }
    return clonar(pago);
  }

  asociar(id: string, pedidoDigitalId: string): Pago {
    const actual = this.requiere(id);
    if (actual.estado === 'RECHAZADO') {
      throw new Error('No se puede asociar un pago rechazado.');
    }
    if (actual.estado === 'CONFIRMADO') {
      throw new Error('El pago ya está confirmado.');
    }

    const pedido = this.requierePedidoPendiente(pedidoDigitalId);
    this.asegurarNoExcedeTotal(pedido, actual.monto, actual.id);

    const siguiente: Pago = {
      ...actual,
      estado: 'ASOCIADO',
      pedidoDigitalId: pedido.id,
      pedidoCodigo: pedido.codigo,
      clienteNombre: pedido.clienteNombre,
      usuarioAsocioNombre: USUARIO_PAGOS,
    };
    this.reemplazar(siguiente);
    return clonar(siguiente);
  }

  confirmar(id: string): Pago {
    const actual = this.requiere(id);
    if (actual.estado === 'CONFIRMADO') {
      return clonar(actual);
    }
    if (actual.estado === 'RECHAZADO') {
      throw new Error('No se puede confirmar un pago rechazado.');
    }
    if (actual.pedidoDigitalId && actual.estado !== 'ASOCIADO') {
      throw new Error('Asocia el pago a un pedido antes de confirmarlo.');
    }

    if (actual.pedidoDigitalId) {
      const pedido = this.pedidosApi.obtener(actual.pedidoDigitalId);
      if (!pedido) {
        throw new Error('No se encontró el pedido digital asociado.');
      }
      this.asegurarNoExcedeTotal(pedido, actual.monto, actual.id, ['CONFIRMADO']);
    }

    const siguiente: Pago = {
      ...actual,
      estado: 'CONFIRMADO',
      fechaConfirmacion: new Date().toISOString(),
    };
    this.reemplazar(siguiente);
    this.marcarPedidoPagadoSiCubre(siguiente);
    return clonar(siguiente);
  }

  rechazar(id: string, observacion?: string): Pago {
    const actual = this.requiere(id);
    if (actual.estado === 'CONFIRMADO') {
      throw new Error('No se puede rechazar un pago ya confirmado.');
    }
    if (actual.estado === 'RECHAZADO') {
      return clonar(actual);
    }

    const siguiente: Pago = {
      ...actual,
      estado: 'RECHAZADO',
      observacion: textoOpcional(observacion, 500) ?? actual.observacion,
    };
    this.reemplazar(siguiente);
    return clonar(siguiente);
  }

  private marcarPedidoPagadoSiCubre(pago: Pago): void {
    if (!pago.pedidoDigitalId) {
      return;
    }
    const pedido = this.pedidosApi.obtener(pago.pedidoDigitalId);
    if (!pedido || pedido.estado !== 'PendientePago') {
      return;
    }
    const cubierto = this.montoCubierto(pedido.id, ['CONFIRMADO']);
    if (cubierto + 1e-9 < pedido.total) {
      return;
    }
    void this.pedidosApi.cambiarEstado(
      pedido.id,
      'Pagado',
      `Pago ${pago.origen} confirmado (${pago.codigoOperacion ?? pago.id}).`,
    );
  }

  private asegurarCodigoLibre(codigo: string | null, excluirId?: string): void {
    if (codigo && this.codigoOperacionDuplicado(codigo, excluirId)) {
      throw new Error(`Ya existe un pago con el código de operación ${codigo}.`);
    }
  }

  private asegurarNoExcedeTotal(
    pedido: PedidoDigital,
    montoNuevo: number,
    excluirPagoId?: string,
    estados: readonly EstadoPago[] = ['ASOCIADO', 'CONFIRMADO'],
  ): void {
    const cubierto = round2(
      this.pagosSignal()
        .filter(
          (pago) =>
            pago.pedidoDigitalId === pedido.id &&
            pago.id !== excluirPagoId &&
            estados.includes(pago.estado),
        )
        .reduce((sum, pago) => sum + pago.monto, 0),
    );
    if (round2(cubierto + montoNuevo) - pedido.total > 0.009) {
      throw new Error(
        `El pago excede el total del pedido (S/ ${pedido.total.toFixed(2)}). Saldo: S/ ${round2(pedido.total - cubierto).toFixed(2)}.`,
      );
    }
  }

  private requierePedidoPendiente(id: string): PedidoDigital {
    const pedido = this.pedidosApi.obtener(id);
    if (!pedido) {
      throw new Error('Selecciona un pedido digital pendiente.');
    }
    if (pedido.estado !== 'PendientePago') {
      throw new Error('Solo se asocian pagos a pedidos en Pendiente de pago.');
    }
    return pedido;
  }

  private requiere(id: string): Pago {
    const encontrado = this.pagosSignal().find((item) => item.id === id);
    if (!encontrado) {
      throw new Error('No se encontró el pago.');
    }
    return clonar(encontrado);
  }

  private reemplazar(pago: Pago): void {
    this.pagosSignal.update((items) => items.map((item) => (item.id === pago.id ? pago : item)));
  }
}

function clonar(pago: Pago): Pago {
  return { ...pago };
}

function mapPagoApi(dto: PagoApi): Pago {
  return {
    id: dto.id,
    origen: dto.origen,
    estado: dto.estado,
    monto: round2(Number(dto.monto)),
    codigoOperacion: dto.codigoOperacion ?? null,
    referenciaExterna: dto.referenciaExterna ?? null,
    pedidoDigitalId: dto.pedidoDigitalId ?? null,
    pedidoCodigo: dto.pedidoCodigo ?? (dto.pedidoDigitalId ? codigoPedido(dto.pedidoDigitalId) : null),
    ventaId: dto.ventaId ?? null,
    clienteNombre: dto.clienteNombre ?? null,
    fechaNotificacion: dto.fechaNotificacion,
    fechaConfirmacion: dto.fechaConfirmacion ?? null,
    usuarioAsocioNombre: dto.usuarioAsocioNombre ?? null,
    observacion: dto.observacion ?? null,
  };
}

function textoOpcional(valor: string | null | undefined, max: number): string | null {
  const texto = valor?.trim() ?? '';
  return texto.length === 0 ? null : texto.slice(0, max);
}

function esMismoDiaLocal(iso: string): boolean {
  const fecha = new Date(iso);
  const hoy = new Date();
  return (
    fecha.getFullYear() === hoy.getFullYear() &&
    fecha.getMonth() === hoy.getMonth() &&
    fecha.getDate() === hoy.getDate()
  );
}

const SEED_PAGOS: Pago[] = [
  pago({
    id: 'pag-101',
    origen: 'YAPE',
    estado: 'NOTIFICADO',
    monto: 80,
    codigoOperacion: '084512',
    referenciaExterna: 'Captura Yape 22:14',
    pedidoDigitalId: null,
    clienteNombre: null,
    fechaNotificacion: isoHoy(10, 14),
    observacion: 'Voucher Yape sin pedido asociado.',
  }),
  pago({
    id: 'pag-102',
    origen: 'IZIPAY',
    estado: 'NOTIFICADO',
    monto: 150,
    codigoOperacion: 'IZP-99821',
    referenciaExterna: 'Webhook Izipay autorizado',
    pedidoDigitalId: null,
    clienteNombre: null,
    fechaNotificacion: isoHoy(11, 5),
    observacion: 'Notificación Izipay pendiente de conciliar.',
  }),
  pago({
    id: 'pag-103',
    origen: 'YAPE',
    estado: 'ASOCIADO',
    monto: 220,
    codigoOperacion: '077331',
    referenciaExterna: 'Yape Luis F. Charizard',
    pedidoDigitalId: 'ped-018',
    clienteNombre: 'Luis F.',
    fechaNotificacion: '2026-08-18T21:40:00-05:00',
    usuarioAsocioNombre: 'Caja',
    observacion: 'Asociado a subasta Facebook. Pendiente de confirmar.',
  }),
  pago({
    id: 'pag-104',
    origen: 'IZIPAY',
    estado: 'CONFIRMADO',
    monto: 249.9,
    codigoOperacion: 'IZP-4412',
    referenciaExterna: 'Izipay woo-4412',
    pedidoDigitalId: 'ped-050',
    clienteNombre: 'Ana Paredes',
    fechaNotificacion: '2026-08-20T09:50:00-05:00',
    fechaConfirmacion: '2026-08-20T10:02:00-05:00',
    usuarioAsocioNombre: 'Caja',
    observacion: 'Pago web WooCommerce.',
  }),
  pago({
    id: 'pag-105',
    origen: 'YAPE',
    estado: 'CONFIRMADO',
    monto: 12.5,
    codigoOperacion: '065902',
    referenciaExterna: 'Yape Mario R.',
    pedidoDigitalId: 'ped-051',
    clienteNombre: 'Mario R.',
    fechaNotificacion: '2026-08-20T16:55:00-05:00',
    fechaConfirmacion: '2026-08-20T17:05:00-05:00',
    usuarioAsocioNombre: 'Caja',
  }),
  pago({
    id: 'pag-106',
    origen: 'EFECTIVO',
    estado: 'CONFIRMADO',
    monto: 54.9,
    codigoOperacion: null,
    referenciaExterna: null,
    pedidoDigitalId: 'ped-052',
    clienteNombre: 'Diego Soto',
    fechaNotificacion: '2026-08-21T12:12:00-05:00',
    fechaConfirmacion: '2026-08-21T12:12:00-05:00',
    usuarioAsocioNombre: 'Caja',
    observacion: 'Pago en mostrador.',
  }),
  pago({
    id: 'pag-107',
    origen: 'IZIPAY',
    estado: 'CONFIRMADO',
    monto: 54.9,
    codigoOperacion: 'IZP-4301',
    referenciaExterna: 'Izipay woo-4301',
    pedidoDigitalId: 'ped-060',
    clienteNombre: 'Valeria Núñez',
    fechaNotificacion: '2026-08-17T18:10:00-05:00',
    fechaConfirmacion: '2026-08-17T18:22:00-05:00',
    usuarioAsocioNombre: 'Caja',
  }),
  pago({
    id: 'pag-108',
    origen: 'TRANSFERENCIA',
    estado: 'CONFIRMADO',
    monto: 189.9,
    codigoOperacion: 'BCP-552190',
    referenciaExterna: 'Constancia BCP PDF',
    pedidoDigitalId: null,
    clienteNombre: 'Carlos M.',
    fechaNotificacion: isoHoy(9, 20),
    fechaConfirmacion: isoHoy(9, 28),
    usuarioAsocioNombre: 'Caja',
    observacion: 'Transferencia de mostrador confirmada hoy.',
  }),
  pago({
    id: 'pag-109',
    origen: 'YAPE',
    estado: 'RECHAZADO',
    monto: 12.5,
    codigoOperacion: '011004',
    referenciaExterna: 'Captura ilegible',
    pedidoDigitalId: null,
    clienteNombre: null,
    fechaNotificacion: '2026-08-16T11:40:00-05:00',
    observacion: 'Voucher no corresponde a Trunqi.',
  }),
];

function pago(
  item: Omit<Pago, 'ventaId' | 'fechaConfirmacion' | 'usuarioAsocioNombre' | 'observacion' | 'pedidoCodigo'> &
    Partial<Pick<Pago, 'ventaId' | 'fechaConfirmacion' | 'usuarioAsocioNombre' | 'observacion' | 'pedidoCodigo'>>,
): Pago {
  return {
    ventaId: null,
    fechaConfirmacion: null,
    usuarioAsocioNombre: null,
    observacion: null,
    ...item,
    pedidoCodigo: item.pedidoCodigo ?? (item.pedidoDigitalId ? codigoPedido(item.pedidoDigitalId) : null),
  };
}

function isoHoy(hora: number, minuto: number): string {
  const fecha = new Date();
  fecha.setHours(hora, minuto, 0, 0);
  return fecha.toISOString();
}
