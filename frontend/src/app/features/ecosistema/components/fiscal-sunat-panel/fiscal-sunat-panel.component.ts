import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, output, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';

import { EcosistemaApiService } from '../../data-access/ecosistema.service';
import {
  CODIGOS_SUNAT,
  ESTADOS_EMISION,
  ETIQUETAS_COMPROBANTE,
  ETIQUETAS_ESTADO_EMISION,
  ETIQUETAS_FILTRO_CONSOLIDADA,
  EmisionSimulada,
  EstadoEmisionSunat,
  FiltroBoletaConsolidada,
  NotaVentaPendiente,
  SerieComprobante,
  TAMANOS_PAGINA_COMPROBANTES,
  TIPOS_COMPROBANTE,
  TipoComprobanteSunat,
  UMBRAL_VENTAS_MENORES,
  numeroComprobante,
  puedeDescargarCdr,
  puedeDescargarXml,
  puedeAbrirPdf,
} from '../../models/ecosistema.model';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

type VistaFiscal = 'config' | 'notas';

@Component({
  selector: 'app-fiscal-sunat-panel',
  imports: [SolesPipe, DatePipe, FormsModule, ReactiveFormsModule],
  templateUrl: './fiscal-sunat-panel.component.html',
  styleUrl: './fiscal-sunat-panel.component.scss',
})
export class FiscalSunatPanelComponent {
  private readonly fb = inject(FormBuilder);
  readonly api = inject(EcosistemaApiService);
  readonly simular = output<void>();

  readonly tipos = TIPOS_COMPROBANTE;
  readonly estadosEmision = ESTADOS_EMISION;
  readonly etiquetas = ETIQUETAS_COMPROBANTE;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_EMISION;
  readonly etiquetasFiltro = ETIQUETAS_FILTRO_CONSOLIDADA;
  readonly umbral = UMBRAL_VENTAS_MENORES;
  readonly codigos = CODIGOS_SUNAT;
  readonly tamanosPagina = TAMANOS_PAGINA_COMPROBANTES;
  readonly vista = signal<VistaFiscal>('config');
  readonly aviso = signal('');
  readonly error = signal('');
  readonly generando = signal(false);
  readonly filtro = signal<FiltroBoletaConsolidada>('VENTAS_MENORES');
  readonly fecha = signal(fechaHoy());
  readonly seleccion = signal<ReadonlySet<string>>(new Set());
  readonly busquedaComprobantes = signal('');
  readonly tipoFiltro = signal<TipoComprobanteSunat | 'TODOS'>('TODOS');
  readonly estadoFiltro = signal<EstadoEmisionSunat | 'TODOS'>('TODOS');
  readonly pagina = signal(1);
  readonly pageSize = signal<(typeof TAMANOS_PAGINA_COMPROBANTES)[number]>(10);
  readonly numeroDe = numeroComprobante;
  readonly puedeXml = puedeDescargarXml;
  readonly puedeCdr = puedeDescargarCdr;
  readonly puedePdf = puedeAbrirPdf;

  readonly fiscalForm = this.fb.nonNullable.group({
    ruc: [this.api.fiscal().ruc, [Validators.required, Validators.pattern(/^\d{11}$/)]],
    razonSocial: [this.api.fiscal().razonSocial, Validators.required],
    nombreComercial: [this.api.fiscal().nombreComercial, Validators.required],
    direccionFiscal: [this.api.fiscal().direccionFiscal, Validators.required],
    ubigeo: [this.api.fiscal().ubigeo, [Validators.required, Validators.pattern(/^\d{6}$/)]],
    departamento: [this.api.fiscal().departamento, Validators.required],
    provincia: [this.api.fiscal().provincia, Validators.required],
    distrito: [this.api.fiscal().distrito, Validators.required],
  });

  readonly seriesForm = this.fb.nonNullable.group({
    BOLETA: this.grupoSerie('BOLETA'),
    FACTURA: this.grupoSerie('FACTURA'),
    NOTA_VENTA: this.grupoSerie('NOTA_VENTA'),
    NOTA_CREDITO: this.grupoSerie('NOTA_CREDITO'),
    GUIA_REMISION: this.grupoSerie('GUIA_REMISION'),
  });

  readonly notasVisibles = computed(() => {
    const notas = this.api.notasPendientes();
    return this.filtro() === 'VENTAS_MENORES' ? notas.filter((nota) => nota.esVentaMenor) : notas;
  });

  readonly idsAConsolidar = computed(() => {
    const visibles = this.notasVisibles();
    if (this.filtro() === 'VENTAS_MENORES') {
      return visibles.map((nota) => nota.id);
    }
    const seleccion = this.seleccion();
    return visibles.filter((nota) => seleccion.has(nota.id)).map((nota) => nota.id);
  });

  readonly totalSeleccionado = computed(() => {
    const ids = new Set(this.idsAConsolidar());
    return this.notasVisibles()
      .filter((nota) => ids.has(nota.id))
      .reduce((suma, nota) => suma + nota.total, 0);
  });

  readonly emisionesFiltradas = computed(() => {
    const query = this.busquedaComprobantes().trim().toLowerCase();
    const tipo = this.tipoFiltro();
    const estado = this.estadoFiltro();
    return this.api
      .emisiones()
      .filter((emision) => {
        if (tipo !== 'TODOS' && emision.tipo !== tipo) {
          return false;
        }
        if (estado !== 'TODOS' && emision.estado !== estado) {
          return false;
        }
        if (!query) {
          return true;
        }
        const numero = numeroComprobante(emision).toLowerCase();
        const correlativo = String(emision.correlativo);
        const cliente = (emision.clienteNombre || '').toLowerCase();
        return (
          numero.includes(query) ||
          correlativo.includes(query) ||
          emision.serie.toLowerCase().includes(query) ||
          cliente.includes(query)
        );
      })
      .slice()
      .sort((a, b) => new Date(b.fecha).getTime() - new Date(a.fecha).getTime());
  });

  readonly totalPaginas = computed(() =>
    Math.max(1, Math.ceil(this.emisionesFiltradas().length / this.pageSize())),
  );

  readonly paginaActual = computed(() => Math.min(this.pagina(), this.totalPaginas()));

  readonly emisionesPagina = computed(() => {
    const size = this.pageSize();
    const inicio = (this.paginaActual() - 1) * size;
    return this.emisionesFiltradas().slice(inicio, inicio + size);
  });

  readonly rangoPagina = computed(() => {
    const total = this.emisionesFiltradas().length;
    if (total === 0) {
      return { desde: 0, hasta: 0, total };
    }
    const size = this.pageSize();
    const desde = (this.paginaActual() - 1) * size + 1;
    const hasta = Math.min(this.paginaActual() * size, total);
    return { desde, hasta, total };
  });

  constructor() {
    effect(() => {
      const fiscal = this.api.fiscal();
      this.fiscalForm.patchValue(fiscal, { emitEvent: false });
    });
    effect(() => {
      for (const item of this.api.series()) {
        const control = this.seriesForm.controls[item.tipo];
        if (!control) {
          continue;
        }
        control.patchValue(
          {
            serie: item.serie,
            correlativo: item.correlativo,
            activa: item.activa,
          },
          { emitEvent: false },
        );
      }
    });
  }

  async mostrarNotas(): Promise<void> {
    this.vista.set('notas');
    await this.cargarNotas();
  }

  async cargarNotas(): Promise<void> {
    this.error.set('');
    try {
      await this.api.refrescarNotasPendientes(this.fecha());
      this.seleccion.set(new Set());
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudieron cargar las notas de venta.');
    }
  }

  cambiarFiltro(filtro: FiltroBoletaConsolidada): void {
    this.filtro.set(filtro);
    this.seleccion.set(new Set());
  }

  toggleNota(id: string): void {
    this.seleccion.update((actual) => {
      const siguiente = new Set(actual);
      if (siguiente.has(id)) {
        siguiente.delete(id);
      } else {
        siguiente.add(id);
      }
      return siguiente;
    });
  }

  estaSeleccionada(nota: NotaVentaPendiente): boolean {
    return this.filtro() === 'VENTAS_MENORES' || this.seleccion().has(nota.id);
  }

  async generarConsolidada(): Promise<void> {
    this.error.set('');
    this.aviso.set('');
    const ids = this.idsAConsolidar();
    if (ids.length === 0) {
      this.error.set(
        this.filtro() === 'VENTAS_MENORES'
          ? `No hay notas de venta del día menores a S/ ${this.umbral.toFixed(2)}.`
          : 'Selecciona al menos una nota de venta pendiente.',
      );
      return;
    }

    this.generando.set(true);
    try {
      const resultado = await this.api.generarBoletaConsolidada(this.filtro(), this.fecha(), ids);
      this.aviso.set(
        `Boleta consolidada ${this.numeroDe(resultado.comprobante)} aceptada. ${resultado.cantidadNotas} notas pasaron a Facturada / Consolidada.`,
      );
      this.seleccion.set(new Set());
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo generar la boleta consolidada.');
    } finally {
      this.generando.set(false);
    }
  }

  async guardarFiscal(): Promise<void> {
    this.error.set('');
    try {
      await this.api.guardarFiscal(this.fiscalForm.getRawValue());
      this.aviso.set('Datos de empresa guardados.');
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo guardar la ficha fiscal.');
    }
  }

  guardarSeries(): void {
    this.error.set('');
    try {
      const raw = this.seriesForm.getRawValue();
      this.api.guardarSeries(
        TIPOS_COMPROBANTE.map((tipo) => ({
          tipo,
          serie: raw[tipo].serie,
          correlativo: Number(raw[tipo].correlativo),
          activa: raw[tipo].activa,
        })),
      );
      this.aviso.set('Series y correlativos actualizados.');
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudieron guardar las series.');
    }
  }

  actualizarBusqueda(valor: string): void {
    this.busquedaComprobantes.set(valor);
    this.pagina.set(1);
  }

  actualizarTipo(valor: TipoComprobanteSunat | 'TODOS'): void {
    this.tipoFiltro.set(valor);
    this.pagina.set(1);
  }

  actualizarEstado(valor: EstadoEmisionSunat | 'TODOS'): void {
    this.estadoFiltro.set(valor);
    this.pagina.set(1);
  }

  cambiarTamano(valor: string | number): void {
    const size = Number(valor);
    this.pageSize.set(
      this.tamanosPagina.includes(size as (typeof TAMANOS_PAGINA_COMPROBANTES)[number])
        ? (size as (typeof TAMANOS_PAGINA_COMPROBANTES)[number])
        : 10,
    );
    this.pagina.set(1);
  }

  paginaAnterior(): void {
    this.pagina.set(Math.max(1, this.paginaActual() - 1));
  }

  paginaSiguiente(): void {
    this.pagina.set(Math.min(this.totalPaginas(), this.paginaActual() + 1));
  }

  limpiarFiltrosComprobantes(): void {
    this.busquedaComprobantes.set('');
    this.tipoFiltro.set('TODOS');
    this.estadoFiltro.set('TODOS');
    this.pagina.set(1);
  }

  private grupoSerie(tipo: SerieComprobante['tipo']) {
    const actual = this.serieDe(tipo);
    return this.fb.nonNullable.group({
      serie: [actual.serie, [Validators.required, Validators.minLength(4), Validators.maxLength(4)]],
      correlativo: [actual.correlativo, [Validators.required, Validators.min(1)]],
      activa: [actual.activa],
    });
  }

  private serieDe(tipo: SerieComprobante['tipo']): SerieComprobante {
    return (
      this.api.series().find((item) => item.tipo === tipo) ?? {
        tipo,
        serie: '',
        correlativo: 1,
        activa: true,
      }
    );
  }

  async descargarXml(emision: EmisionSimulada): Promise<void> {
    this.error.set('');
    try {
      await this.api.descargarXml(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el XML.');
    }
  }

  async descargarCdr(emision: EmisionSimulada): Promise<void> {
    this.error.set('');
    try {
      await this.api.descargarCdr(emision);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo descargar el CDR.');
    }
  }

  async abrirPdf(emision: EmisionSimulada, formato: 'a4' | 'ticket' = 'a4'): Promise<void> {
    this.error.set('');
    try {
      await this.api.abrirPdf(emision, formato);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo abrir el PDF.');
    }
  }
}

function fechaHoy(): string {
  const ahora = new Date();
  const yyyy = ahora.getFullYear();
  const mm = String(ahora.getMonth() + 1).padStart(2, '0');
  const dd = String(ahora.getDate()).padStart(2, '0');
  return `${yyyy}-${mm}-${dd}`;
}
