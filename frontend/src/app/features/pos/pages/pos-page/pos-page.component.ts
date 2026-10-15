import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { CajaApiService } from '../../../caja/data-access/caja.service';
import { EcosistemaApiService } from '../../../ecosistema/data-access/ecosistema.service';
import {
  ETIQUETAS_COMPROBANTE,
  ETIQUETAS_ESTADO_EMISION,
  EmisionSimulada,
  EstadoEmisionSunat,
  numeroComprobante,
  puedeAbrirPdf,
  puedeDescargarCdr,
  puedeDescargarXml,
} from '../../../ecosistema/models/ecosistema.model';
import { PedidoDigital } from '../../../pedidos-digitales/models/pedido-digital.model';
import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import { ProductoTcg } from '../../../productos-tcg/models/producto-tcg.model';
import { validarDocumentoParaComprobante } from '../../../clientes/models/cliente.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';
import { PosApiService } from '../../data-access/pos.service';
import {
  CobrarYEmitirPosResponse,
  MEDIOS_PAGO_POS,
  OrigenPagoPos,
  PosLineaCarrito,
  TIPOS_CPE_POS,
  TipoComprobantePos,
  round2,
} from '../../models/pos.model';

@Component({
  selector: 'app-pos-page',
  imports: [FormsModule, SolesPipe, DecimalPipe],
  templateUrl: './pos-page.component.html',
  styleUrl: './pos-page.component.scss',
})
export class PosPageComponent {
  private readonly posApi = inject(PosApiService);
  private readonly productosApi = inject(ProductosTcgApiService);
  private readonly sedesApi = inject(SedesApiService);
  private readonly cajaApi = inject(CajaApiService);
  private readonly ecosistemaApi = inject(EcosistemaApiService);

  readonly sedes = this.sedesApi.sedes;
  readonly mediosPago = MEDIOS_PAGO_POS;
  readonly tiposCpe = TIPOS_CPE_POS;
  readonly etiquetasCpe = ETIQUETAS_COMPROBANTE;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_EMISION;
  readonly numeroDe = numeroComprobante;
  readonly puedeXml = puedeDescargarXml;
  readonly puedeCdr = puedeDescargarCdr;
  readonly puedePdf = puedeAbrirPdf;

  readonly sedeId = signal('');
  readonly busquedaProducto = signal('');
  readonly busquedaPedido = signal('');
  readonly productos = signal<ProductoTcg[]>([]);
  readonly pedidosPendientes = signal<PedidoDigital[]>([]);
  readonly carrito = signal<PosLineaCarrito[]>([]);
  readonly cargandoProductos = signal(false);
  readonly cargandoPedidos = signal(false);
  readonly cobrando = signal(false);
  readonly error = signal('');
  readonly avisoCaja = signal('');

  readonly origenPago = signal<OrigenPagoPos>('EFECTIVO');
  readonly tipoComprobante = signal<TipoComprobantePos>('BOLETA');
  readonly clienteNombre = signal('');
  readonly numeroDocumento = signal('');
  readonly codigoOperacion = signal('');
  readonly montoRecibido = signal<number | null>(null);
  readonly resultado = signal<CobrarYEmitirPosResponse | null>(null);

  readonly totalItems = computed(() =>
    this.carrito().reduce((sum, linea) => sum + linea.cantidad, 0),
  );
  readonly subtotal = computed(() =>
    round2(this.carrito().reduce((sum, linea) => sum + linea.precioUnitario * linea.cantidad, 0)),
  );
  readonly cajaAbierta = computed(() => {
    const sede = this.sedeId();
    return sede ? this.cajaApi.estaAbierta(sede) : false;
  });
  readonly requiereCodigo = computed(() => {
    const origen = this.origenPago();
    return origen === 'YAPE' || origen === 'PLIN' || origen === 'TARJETA';
  });
  readonly vuelto = computed(() => {
    const recibido = this.montoRecibido();
    if (this.origenPago() !== 'EFECTIVO' || recibido == null) {
      return null;
    }
    return round2(Math.max(0, recibido - this.subtotal()));
  });
  readonly emisionVista = computed<EmisionSimulada | null>(() => {
    const r = this.resultado();
    if (!r) {
      return null;
    }
    const c = r.comprobante;
    return {
      id: c.id,
      tipo: c.tipo,
      serie: c.serie,
      correlativo: c.correlativo,
      estado: c.estado as EstadoEmisionSunat,
      pedidoId: r.pedidos[0]?.id ?? '',
      ventaId: r.ventaId,
      clienteNombre: c.clienteNombre ?? r.pedidos[0]?.clienteNombre ?? '',
      total: c.total ?? r.totalCobrado,
      mensaje: r.mensaje ?? c.mensaje ?? '',
      fecha: c.fechaEmision,
      hashFirma: c.hashFirma,
      tieneXml: c.tieneXml,
      tieneCdr: c.tieneCdr,
      tienePdf: c.tienePdf,
    };
  });

  constructor() {
    void this.inicializar();
  }

  async inicializar(): Promise<void> {
    try {
      await this.sedesApi.refrescar();
      const primera = this.sedes()[0]?.id ?? '';
      this.sedeId.set(primera);
      if (primera) {
        await this.cajaApi.refrescarEstado(primera).catch(() => undefined);
        this.sincronizarAvisoCaja();
      }
      await Promise.all([this.cargarProductos(), this.cargarPedidosPendientes()]);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo iniciar el POS.');
    }
  }

  async onSedeChange(sedeId: string): Promise<void> {
    this.sedeId.set(sedeId);
    await this.cajaApi.refrescarEstado(sedeId).catch(() => undefined);
    this.sincronizarAvisoCaja();
    await Promise.all([this.cargarProductos(), this.cargarPedidosPendientes()]);
  }

  async cargarProductos(): Promise<void> {
    this.cargandoProductos.set(true);
    this.error.set('');
    try {
      const pagina = await this.productosApi.consultarPagina(
        {
          busqueda: this.busquedaProducto(),
          tipoProducto: 'TODOS',
          juego: 'TODOS',
          estadoActivo: 'ACTIVOS',
          estadoStock: 'TODOS',
          sincronizacionWoo: 'TODOS',
        },
        1,
        48,
      );
      this.productos.set(pagina.items);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudieron cargar productos.');
    } finally {
      this.cargandoProductos.set(false);
    }
  }

  async cargarPedidosPendientes(): Promise<void> {
    this.cargandoPedidos.set(true);
    try {
      const items = await this.posApi.pedidosPendientes(
        this.busquedaPedido(),
        this.sedeId() || undefined,
      );
      this.pedidosPendientes.set(items);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudieron cargar pedidos pendientes.');
    } finally {
      this.cargandoPedidos.set(false);
    }
  }

  onBuscarProducto(): void {
    void this.cargarProductos();
  }

  onBuscarPedido(): void {
    void this.cargarPedidosPendientes();
  }

  agregarProducto(producto: ProductoTcg): void {
    this.resultado.set(null);
    this.carrito.update((actual) => {
      const key = `prod:${producto.id}`;
      const existente = actual.find((l) => l.key === key);
      if (existente) {
        return actual.map((l) =>
          l.key === key ? { ...l, cantidad: l.cantidad + 1 } : l,
        );
      }
      return [
        ...actual,
        {
          key,
          productoId: producto.id,
          nombre: producto.nombre,
          imagenUrl: producto.woo.imagenes[0] ?? null,
          precioUnitario: producto.precioVenta,
          cantidad: 1,
        },
      ];
    });
  }

  agregarPedido(pedido: PedidoDigital): void {
    if (this.carrito().some((l) => l.pedidoDigitalId === pedido.id)) {
      return;
    }
    this.resultado.set(null);
    if (!this.clienteNombre().trim()) {
      this.clienteNombre.set(pedido.clienteNombre);
    }
    if (!this.numeroDocumento().trim() && pedido.clienteNumeroDocumento) {
      this.numeroDocumento.set(pedido.clienteNumeroDocumento);
    }

    const lineas: PosLineaCarrito[] = pedido.detalles.map((d) => ({
      key: `pedido:${pedido.id}:${d.id}`,
      productoId: d.productoId,
      nombre: d.descripcion,
      imagenUrl: null,
      precioUnitario: d.precioUnitario,
      cantidad: Math.max(1, Math.round(d.cantidad)),
      pedidoDigitalId: pedido.id,
      pedidoCodigo: pedido.codigo,
    }));
    this.carrito.update((actual) => [...actual, ...lineas]);
  }

  pedidoEnCarrito(pedidoId: string): boolean {
    return this.carrito().some((l) => l.pedidoDigitalId === pedidoId);
  }

  incrementar(key: string): void {
    this.carrito.update((actual) =>
      actual.map((l) => {
        if (l.key !== key || l.pedidoDigitalId) {
          return l;
        }
        return { ...l, cantidad: l.cantidad + 1 };
      }),
    );
  }

  decrementar(key: string): void {
    this.carrito.update((actual) =>
      actual
        .map((l) => {
          if (l.key !== key) {
            return l;
          }
          if (l.pedidoDigitalId) {
            return l;
          }
          return { ...l, cantidad: l.cantidad - 1 };
        })
        .filter((l) => l.cantidad > 0),
    );
  }

  quitarLinea(key: string): void {
    const linea = this.carrito().find((l) => l.key === key);
    if (!linea) {
      return;
    }
    if (linea.pedidoDigitalId) {
      this.carrito.update((actual) =>
        actual.filter((l) => l.pedidoDigitalId !== linea.pedidoDigitalId),
      );
      return;
    }
    this.carrito.update((actual) => actual.filter((l) => l.key !== key));
  }

  vaciarCarrito(): void {
    this.carrito.set([]);
    this.resultado.set(null);
    this.error.set('');
  }

  setOrigen(origen: OrigenPagoPos): void {
    this.origenPago.set(origen);
    if (origen === 'EFECTIVO') {
      this.codigoOperacion.set('');
    }
  }

  setTipoCpe(tipo: TipoComprobantePos): void {
    this.tipoComprobante.set(tipo);
  }

  async cobrarYEmitir(): Promise<void> {
    this.error.set('');
    this.resultado.set(null);
    const sede = this.sedeId();
    if (!sede) {
      this.error.set('Selecciona una sede.');
      return;
    }
    if (!this.cajaAbierta()) {
      this.error.set(this.cajaApi.mensajeCerrada(sede));
      return;
    }
    const lineas = this.carrito();
    if (lineas.length === 0) {
      this.error.set('Agrega productos o un pedido pendiente al carrito.');
      return;
    }

    const pedidoIds = [
      ...new Set(lineas.map((l) => l.pedidoDigitalId).filter((id): id is string => !!id)),
    ];
    const itemsMostrador = lineas
      .filter((l) => !l.pedidoDigitalId)
      .map((l) => ({
        productoId: l.productoId,
        cantidad: l.cantidad,
        precioUnitario: l.precioUnitario,
      }));

    if (this.requiereCodigo() && !this.codigoOperacion().trim()) {
      this.error.set('Indica el código de operación del pago.');
      return;
    }

    const tipo = this.tipoComprobante();
    const docError = validarDocumentoParaComprobante(
      tipo,
      this.subtotal(),
      tipo === 'FACTURA' ? 'RUC' : this.numeroDocumento().trim() ? 'DNI' : 'SIN_DOCUMENTO',
      this.numeroDocumento().trim() || null,
    );
    if (docError) {
      this.error.set(docError);
      return;
    }

    this.cobrando.set(true);
    try {
      const respuesta = await this.posApi.cobrarYEmitir({
        sedeId: sede,
        pedidoDigitalIds: pedidoIds.length ? pedidoIds : undefined,
        items: itemsMostrador.length ? itemsMostrador : undefined,
        clienteNombre: this.clienteNombre().trim() || undefined,
        tipoDocumento:
          tipo === 'FACTURA'
            ? 'RUC'
            : this.numeroDocumento().trim()
              ? 'DNI'
              : 'SIN_DOCUMENTO',
        numeroDocumento: this.numeroDocumento().trim() || null,
        origenPago: this.origenPago(),
        codigoOperacion: this.codigoOperacion().trim() || undefined,
        montoRecibido:
          this.origenPago() === 'EFECTIVO' ? (this.montoRecibido() ?? this.subtotal()) : null,
        tipoComprobante: tipo,
        observacion: 'Cobro POS mostrador',
      });
      this.resultado.set(respuesta);
      this.carrito.set([]);
      this.codigoOperacion.set('');
      this.montoRecibido.set(null);
      await this.cargarPedidosPendientes();
      await this.cajaApi.refrescarEstado(sede).catch(() => undefined);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo cobrar ni emitir el CPE.');
    } finally {
      this.cobrando.set(false);
    }
  }

  async abrirPdf(formato: 'a4' | 'ticket' = 'ticket'): Promise<void> {
    const emision = this.emisionVista();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.abrirPdf(emision, formato);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo abrir el PDF.');
    }
  }

  async descargarXml(): Promise<void> {
    const emision = this.emisionVista();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.descargarXml(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el XML.');
    }
  }

  async descargarCdr(): Promise<void> {
    const emision = this.emisionVista();
    if (!emision) {
      return;
    }
    try {
      await this.ecosistemaApi.descargarCdr(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el CDR.');
    }
  }

  imagenProducto(producto: ProductoTcg): string | null {
    return producto.woo.imagenes[0] ?? null;
  }

  private sincronizarAvisoCaja(): void {
    const sede = this.sedeId();
    if (!sede) {
      this.avisoCaja.set('Selecciona una sede.');
      return;
    }
    this.avisoCaja.set(
      this.cajaApi.estaAbierta(sede)
        ? `Caja abierta · ${this.sedes().find((s) => s.id === sede)?.nombre ?? 'Sede'}`
        : this.cajaApi.mensajeCerrada(sede),
    );
  }
}
