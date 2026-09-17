import { DecimalPipe } from '@angular/common';
import {
  Component,
  HostListener,
  OnInit,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { startWith } from 'rxjs';

import { ETIQUETAS_MOVIMIENTO } from '../../../inventario/models/inventario.model';
import { ProductoTcg, precioVigente } from '../../../productos-tcg/models/producto-tcg.model';
import { ProductosTcgApiService } from '../../../productos-tcg/data-access/productos-tcg.service';
import { AperturasTcgApiService } from '../../data-access/aperturas-tcg.service';
import {
  ESTADOS_CARTA_OBTENIDA,
  ETIQUETAS_ESTADO_CARTA,
  EstadoCartaObtenida,
  buscarCartasPorSetNumero,
  calcularRendimiento,
  costoSelladoDe,
  estadoAperturaDesdeCondicion,
  resolverCartaCatalogo,
  valorEstimadoCartas,
} from '../../models/apertura-tcg.model';
import { AperturaYieldCardComponent } from '../apertura-yield-card/apertura-yield-card.component';
import { SolesPipe } from '../../../../shared/pipes/soles.pipe';

interface LineaWizard {
  id: string;
  productoCartaId: string;
  cantidad: number;
  estado: EstadoCartaObtenida;
  esFoil: boolean;
}

@Component({
  selector: 'app-apertura-wizard',
  imports: [SolesPipe, DecimalPipe, FormsModule, ReactiveFormsModule, AperturaYieldCardComponent],
  templateUrl: './apertura-wizard.component.html',
  styleUrl: './apertura-wizard.component.scss',
})
export class AperturaWizardComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly aperturasApi = inject(AperturasTcgApiService);
  private readonly productosApi = inject(ProductosTcgApiService);

  readonly aperturaId = input<string | null>(null);
  readonly saved = output<void>();
  readonly cancelled = output<void>();

  readonly paso = signal<1 | 2 | 3>(1);
  readonly error = signal('');
  readonly enviando = signal(false);
  readonly setCodigo = signal('');
  readonly numeroCarta = signal('');
  readonly resultados = signal<ProductoTcg[]>([]);
  readonly lineas = signal<LineaWizard[]>([]);
  readonly busco = signal(false);

  readonly sedes = this.aperturasApi.sedes;
  readonly sellados = computed(() => this.aperturasApi.selladosParaApertura());
  readonly estadosCarta = ESTADOS_CARTA_OBTENIDA;
  readonly etiquetasEstado = ETIQUETAS_ESTADO_CARTA;
  readonly etiquetasMovimiento = ETIQUETAS_MOVIMIENTO;

  readonly form = this.fb.nonNullable.group({
    sedeId: ['', Validators.required],
    productoSelladoId: ['', Validators.required],
    cantidadSellados: [1, [Validators.required, Validators.min(1)]],
    observacion: [''],
  });

  readonly cabecera = toSignal(
    this.form.valueChanges.pipe(startWith(this.form.getRawValue())),
    { initialValue: this.form.getRawValue() },
  );

  private idActual: string | null = null;

  readonly selladoSeleccionado = computed(() => {
    const id = this.cabecera().productoSelladoId;
    return this.sellados().find((item) => item.id === id);
  });

  readonly stockLibreSellado = computed(() => {
    const { sedeId, productoSelladoId } = this.cabecera();
    if (!sedeId || !productoSelladoId) {
      return 0;
    }
    return this.aperturasApi.stockLibre(sedeId, productoSelladoId);
  });

  readonly cartasEsperadas = computed(() => {
    const esperadas = this.selladoSeleccionado()?.atributosTcg.cartasEsperadas ?? 0;
    return esperadas * (Number(this.cabecera().cantidadSellados) || 0);
  });

  readonly totalLineas = computed(() =>
    this.lineas().reduce((sum, linea) => sum + Number(linea.cantidad || 0), 0),
  );

  readonly rendimiento = computed(() => {
    const sellado = this.selladoSeleccionado();
    const cantidad = Number(this.cabecera().cantidadSellados) || 0;
    return calcularRendimiento(
      costoSelladoDe(sellado, cantidad),
      valorEstimadoCartas(this.lineas(), (id) => this.productosApi.obtenerPorId(id)),
    );
  });

  ngOnInit(): void {
    const sedeId = this.sedes()[0]?.id;
    if (sedeId && !this.form.controls.sedeId.value) {
      this.form.patchValue({ sedeId });
    }
    const id = this.aperturaId();
    if (!id) {
      return;
    }
    const apertura = this.aperturasApi.obtener(id);
    if (!apertura || apertura.estado !== 'BORRADOR') {
      this.error.set('Solo se puede continuar una apertura en borrador.');
      return;
    }
    this.idActual = apertura.id;
    this.form.patchValue({
      sedeId: apertura.sedeId,
      productoSelladoId: apertura.productoSelladoId,
      cantidadSellados: apertura.cantidadSellados,
      observacion: apertura.observacion ?? '',
    });
    this.lineas.set(
      apertura.detalles.map((detalle) => ({
        id: detalle.id,
        productoCartaId: detalle.productoCartaId,
        cantidad: detalle.cantidad,
        estado: detalle.estado,
        esFoil: detalle.esFoil,
      })),
    );
    this.paso.set(apertura.detalles.length > 0 ? 2 : 1);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  cartaDe(id: string): ProductoTcg | undefined {
    return this.productosApi.obtenerPorId(id);
  }

  valorCarta(linea: LineaWizard): number {
    const carta = this.cartaDe(linea.productoCartaId);
    return carta ? precioVigente(carta) * linea.cantidad : 0;
  }

  buscarPorSetNumero(): void {
    this.error.set('');
    this.busco.set(true);
    const encontradas = buscarCartasPorSetNumero(
      this.aperturasApi.cartasCatalogo(),
      this.setCodigo(),
      this.numeroCarta(),
    );
    this.resultados.set(encontradas);
  }

  agregarCarta(producto: ProductoTcg): void {
    const estado = estadoAperturaDesdeCondicion(producto.atributosTcg.condicion);
    const esFoil = producto.atributosTcg.esFoil;
    this.lineas.update((items) => {
      const existente = items.find(
        (item) =>
          item.productoCartaId === producto.id && item.estado === estado && item.esFoil === esFoil,
      );
      if (existente) {
        return items.map((item) =>
          item.id === existente.id ? { ...item, cantidad: item.cantidad + 1 } : item,
        );
      }
      return [
        ...items,
        {
          id: globalThis.crypto.randomUUID(),
          productoCartaId: producto.id,
          cantidad: 1,
          estado,
          esFoil,
        },
      ];
    });
    this.error.set('');
  }

  actualizarCantidad(id: string, cantidad: number | string): void {
    const valor = Math.max(1, Math.floor(Number(cantidad) || 1));
    this.lineas.update((items) =>
      items.map((item) => (item.id === id ? { ...item, cantidad: valor } : item)),
    );
  }

  actualizarEstado(id: string, estado: EstadoCartaObtenida): void {
    this.lineas.update((items) =>
      items.map((item) => {
        if (item.id !== id) {
          return item;
        }
        return this.reresolverLinea({ ...item, estado });
      }),
    );
  }

  actualizarFoil(id: string, esFoil: boolean): void {
    this.lineas.update((items) =>
      items.map((item) => {
        if (item.id !== id) {
          return item;
        }
        return this.reresolverLinea({ ...item, esFoil });
      }),
    );
  }

  quitarLinea(id: string): void {
    this.lineas.update((items) => items.filter((item) => item.id !== id));
  }

  irAtras(): void {
    this.error.set('');
    this.paso.update((paso) => (paso === 1 ? 1 : ((paso - 1) as 1 | 2 | 3)));
  }
/*
  async continuarPaso1(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue();
    const request = {
      sedeId: raw.sedeId,
      productoSelladoId: raw.productoSelladoId,
      cantidadSellados: Number(raw.cantidadSellados),
      observacion: raw.observacion,
    };
    this.enviando.set(true);
    try {
      const guardada = this.idActual
        ? await this.aperturasApi.actualizarBorrador(this.idActual, request)
        : await this.aperturasApi.crear(request);
      this.idActual = guardada.id;
      this.paso.set(2);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo guardar el borrador.');
    } finally {
      this.enviando.set(false);
    }
  }
    */

  async continuarPaso1(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    if (this.form.invalid) {
      return;
    }
    const raw = this.form.getRawValue();
    const request = {
      sedeId: raw.sedeId,
      productoSelladoId: raw.productoSelladoId,
      cantidadSellados: Number(raw.cantidadSellados),
      observacion: raw.observacion,
    };
    this.enviando.set(true);
    try {
      const guardada = this.idActual
        ? await this.aperturasApi.actualizarBorrador(this.idActual, request)
        : await this.aperturasApi.crear(request);
      this.idActual = guardada.id;

      // Precarga automática del contenido fijo si la lista de cartas está vacía
      if (this.lineas().length === 0) {
        this.cargarContenidoFijo();
      }

      this.paso.set(2);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo guardar el borrador.');
    } finally {
      this.enviando.set(false);
    }
  }

  private cargarContenidoFijo(): void {
    const sellado = this.selladoSeleccionado();
    if (!sellado || !sellado.contenidoFijo || sellado.contenidoFijo.length === 0) {
      return;
    }

    const multiplicador = Number(this.cabecera().cantidadSellados) || 1;

    const lineasFijas: LineaWizard[] = sellado.contenidoFijo
      .map((item) => {
        const productoRef = this.productosApi.obtenerPorId(item.productoId);
        const estado = productoRef
          ? estadoAperturaDesdeCondicion(productoRef.atributosTcg.condicion)
          : 'NM';
        const esFoil = productoRef?.atributosTcg.esFoil ?? false;

        return {
          id: globalThis.crypto.randomUUID(),
          productoCartaId: item.productoId,
          cantidad: item.cantidad * multiplicador,
          estado,
          esFoil,
        };
      })
      .filter((linea) => Boolean(linea.productoCartaId));

    if (lineasFijas.length > 0) {
      this.lineas.set(lineasFijas);
    }
  }

  
  async continuarPaso2(): Promise<void> {
    this.error.set('');
    if (!this.idActual) {
      this.error.set('Primero selecciona el sellado a abrir.');
      return;
    }
    if (this.lineas().length === 0) {
      this.error.set('Agrega al menos una carta obtenida.');
      return;
    }
    this.enviando.set(true);
    try {
      await this.aperturasApi.reemplazarDetalles(
        this.idActual,
        this.lineas().map((linea) => ({
          id: linea.id,
          productoCartaId: linea.productoCartaId,
          cantidad: Number(linea.cantidad),
          estado: linea.estado,
          esFoil: linea.esFoil,
        })),
      );
      this.paso.set(3);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudieron guardar las cartas.');
    } finally {
      this.enviando.set(false);
    }
  }

  async confirmar(): Promise<void> {
    this.error.set('');
    if (!this.idActual) {
      return;
    }
    this.enviando.set(true);
    try {
      await this.aperturasApi.confirmar(this.idActual);
      this.saved.emit();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo confirmar la apertura.');
    } finally {
      this.enviando.set(false);
    }
  }

  private reresolverLinea(linea: LineaWizard): LineaWizard {
    const actual = this.cartaDe(linea.productoCartaId);
    if (!actual) {
      return linea;
    }
    const resuelta = resolverCartaCatalogo(
      this.aperturasApi.cartasCatalogo(),
      actual,
      linea.estado,
      linea.esFoil,
    );
    return resuelta ? { ...linea, productoCartaId: resuelta.id } : linea;
  }
}
