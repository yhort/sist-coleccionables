import { DatePipe } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { SolesPipe } from '../../../../shared/pipes/soles.pipe';
import {
  ESTADOS_TABLERO_SUBASTA,
  ETIQUETAS_CANAL_SUBASTA,
  ETIQUETAS_ESTADO_SUBASTA,
  EstadoSubastaTcg,
  SubastaTcg,
  esEventoIndividuales,
  etiquetaLoteSubasta,
  nombreVisibleLinea,
  pujaGanadoraActual,
  ultimaPuja,
} from '../../models/subasta-tcg.model';
import { SubastaTableroItem } from '../subasta-card/subasta-card.component';

export type SubastasTablaSort = 'fecha' | 'codigo' | 'producto' | 'precioBase' | 'oferta' | 'estado';
export type SubastasTablaSortDir = 'asc' | 'desc';

export interface PedidoVinculoSubasta {
  id: string;
  codigo: string;
}

const PAGE_SIZE = 40;

@Component({
  selector: 'app-subastas-tabla',
  imports: [SolesPipe, DatePipe, RouterLink],
  templateUrl: './subastas-tabla.component.html',
  styleUrl: './subastas-tabla.component.scss',
})
export class SubastasTablaComponent {
  readonly items = input.required<SubastaTableroItem[]>();
  readonly ver = output<SubastaTcg>();
  readonly adjudicar = output<SubastaTcg>();
  readonly desierta = output<SubastaTcg>();
  readonly activar = output<SubastaTcg>();
  readonly editar = output<SubastaTcg>();
  readonly abrirSala = output<SubastaTcg>();
  readonly irPedido = output<PedidoVinculoSubasta>();

  readonly etiquetasEstado = ETIQUETAS_ESTADO_SUBASTA;
  readonly etiquetasCanal = ETIQUETAS_CANAL_SUBASTA;
  readonly estadosTab: ReadonlyArray<EstadoSubastaTcg | 'TODOS'> = [
    'TODOS',
    ...ESTADOS_TABLERO_SUBASTA,
  ];

  readonly estadoTab = signal<EstadoSubastaTcg | 'TODOS'>('TODOS');
  readonly sortKey = signal<SubastasTablaSort>('fecha');
  readonly sortDir = signal<SubastasTablaSortDir>('desc');
  readonly page = signal(1);

  readonly ultima = ultimaPuja;
  readonly ganadora = pujaGanadoraActual;

  readonly conteoPorEstado = computed(() => {
    const items = this.items();
    const mapa: Record<string, number> = { TODOS: items.length };
    for (const estado of ESTADOS_TABLERO_SUBASTA) {
      mapa[estado] = items.filter((item) => item.subasta.estado === estado).length;
    }
    return mapa;
  });

  readonly filtrados = computed(() => {
    const tab = this.estadoTab();
    const key = this.sortKey();
    const dir = this.sortDir();
    const factor = dir === 'asc' ? 1 : -1;

    const base =
      tab === 'TODOS'
        ? [...this.items()]
        : this.items().filter((item) => item.subasta.estado === tab);

    base.sort((a, b) => {
      const sa = a.subasta;
      const sb = b.subasta;
      let cmp = 0;
      switch (key) {
        case 'codigo':
          cmp = sa.codigo.localeCompare(sb.codigo, 'es');
          break;
        case 'producto':
          cmp = (sa.titulo || a.productoNombre).localeCompare(sb.titulo || b.productoNombre, 'es');
          break;
        case 'precioBase':
          cmp = sa.precioBase - sb.precioBase;
          break;
        case 'oferta': {
          const oa = ultimaPuja(sa)?.monto ?? 0;
          const ob = ultimaPuja(sb)?.monto ?? 0;
          cmp = oa - ob;
          break;
        }
        case 'estado':
          cmp = sa.estado.localeCompare(sb.estado);
          break;
        case 'fecha':
        default:
          cmp = new Date(sa.fechaInicio).getTime() - new Date(sb.fechaInicio).getTime();
          break;
      }
      return cmp * factor;
    });

    return base;
  });

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.filtrados().length / PAGE_SIZE)),
  );

  readonly paginaActual = computed(() => Math.min(this.page(), this.totalPages()));

  readonly pageItems = computed(() => {
    const pagina = this.paginaActual();
    const inicio = (pagina - 1) * PAGE_SIZE;
    return this.filtrados().slice(inicio, inicio + PAGE_SIZE);
  });

  readonly rango = computed(() => {
    const total = this.filtrados().length;
    if (total === 0) {
      return { desde: 0, hasta: 0, total: 0 };
    }
    const pagina = this.paginaActual();
    const desde = (pagina - 1) * PAGE_SIZE + 1;
    const hasta = Math.min(pagina * PAGE_SIZE, total);
    return { desde, hasta, total };
  });

  setEstadoTab(estado: EstadoSubastaTcg | 'TODOS'): void {
    this.estadoTab.set(estado);
    this.page.set(1);
  }

  ordenar(clave: SubastasTablaSort): void {
    if (this.sortKey() === clave) {
      this.sortDir.update((dir) => (dir === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortKey.set(clave);
      this.sortDir.set(clave === 'codigo' || clave === 'producto' ? 'asc' : 'desc');
    }
    this.page.set(1);
  }

  sortMark(clave: SubastasTablaSort): string {
    if (this.sortKey() !== clave) {
      return '';
    }
    return this.sortDir() === 'asc' ? ' ↑' : ' ↓';
  }

  paginaAnterior(): void {
    this.page.update((p) => Math.max(1, p - 1));
  }

  paginaSiguiente(): void {
    this.page.update((p) => Math.min(this.totalPages(), p + 1));
  }

  etiquetaEstadoTab(estado: EstadoSubastaTcg | 'TODOS'): string {
    return estado === 'TODOS' ? 'Todas' : this.etiquetasEstado[estado];
  }

  tituloEvento(item: SubastaTableroItem): string {
    return item.subasta.titulo?.trim() || item.productoNombre;
  }

  detalleLote(item: SubastaTableroItem): string {
    const subasta = item.subasta;
    if (subasta.detalles?.length === 1) {
      const linea = subasta.detalles[0];
      const nombre = nombreVisibleLinea(linea);
      const sku = linea.codigoSku || subasta.codigoSku;
      return sku ? `${nombre} · ${sku}` : nombre;
    }
    if (subasta.detalles?.length > 1) {
      return etiquetaLoteSubasta(subasta);
    }
    const sku = subasta.codigoSku;
    const nombre = item.productoNombre;
    if (sku && nombre && nombre !== subasta.titulo) {
      return `${nombre} · ${sku}`;
    }
    return sku || nombre || '—';
  }

  ofertaMostrada(subasta: SubastaTcg): {
    monto: number;
    postor: string;
    totalPujas: number;
  } | null {
    const oferta = this.ganadora(subasta) ?? this.ultima(subasta);
    if (!oferta) {
      return null;
    }
    return {
      monto: oferta.monto,
      postor: oferta.nombrePostor,
      totalPujas: subasta.pujas.length,
    };
  }

  pedidosVinculados(subasta: SubastaTcg): PedidoVinculoSubasta[] {
    const vistos = new Set<string>();
    const result: PedidoVinculoSubasta[] = [];

    const push = (id: string | null | undefined, codigo: string | null | undefined) => {
      if (!id || vistos.has(id)) {
        return;
      }
      vistos.add(id);
      result.push({
        id,
        codigo: codigo?.trim() || `#PED-${id.slice(0, 6).toUpperCase()}`,
      });
    };

    push(subasta.pedidoDigitalId, subasta.pedidoCodigo);
    for (const linea of subasta.detalles ?? []) {
      push(linea.pedidoDigitalId, linea.pedidoCodigo);
    }
    return result;
  }

  etiquetaPujas(n: number): string {
    return n === 1 ? '1 puja' : `${n} pujas`;
  }
}
