import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { apiUrl } from '../../../core/http/api-url';
import { readApiError } from '../../../core/http/api-error';
import { RarezaTcg } from '../../productos-tcg/models/producto-tcg.model';
import { ImportarSetTcgRequest, ImportarSetTcgResponse, TcgCarta, TcgSerie, TcgSet, ActualizarTcgSetRequest, UpsertTcgSerieRequest, UpsertTcgSetRequest } from '../models/catalogo-tcg.model';
import { parsearCatalogoOficial, ParseCatalogoResultado } from './parse-catalogo-oficial';

@Injectable({ providedIn: 'root' })
export class CatalogoTcgApiService {
  private readonly http = inject(HttpClient);
  private readonly seriesSignal = signal<TcgSerie[]>([]);
  private readonly setsSignal = signal<TcgSet[]>([]);
  private readonly cartasSignal = signal<TcgCarta[]>([]);

  readonly series = this.seriesSignal.asReadonly();
  readonly sets = this.setsSignal.asReadonly();
  readonly cartas = this.cartasSignal.asReadonly();

  async listarSeries(): Promise<TcgSerie[]> {
    try {
      const items = await firstValueFrom(this.http.get<TcgSerie[]>(apiUrl('tcg/series')));
      this.seriesSignal.set(items);
      return items;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async crearSerie(request: UpsertTcgSerieRequest): Promise<TcgSerie> {
    try {
      const creada = await firstValueFrom(
        this.http.post<TcgSerie>(apiUrl('tcg/series'), request),
      );
      this.seriesSignal.update((items) =>
        items.some((item) => item.id === creada.id) ? items : [...items, creada],
      );
      return creada;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async crearSet(request: UpsertTcgSetRequest): Promise<TcgSet> {
    try {
      const creado = await firstValueFrom(this.http.post<TcgSet>(apiUrl('tcg/sets'), request));
      this.setsSignal.update((items) =>
        items.some((item) => item.id === creado.id) ? items : [...items, creado],
      );
      return creado;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async actualizarSet(id: string, request: ActualizarTcgSetRequest): Promise<TcgSet> {
    try {
      const actualizado = await firstValueFrom(
        this.http.put<TcgSet>(apiUrl(`tcg/sets/${id}`), request),
      );
      this.setsSignal.update((items) =>
        items.map((item) => (item.id === actualizado.id ? actualizado : item)),
      );
      this.seriesSignal.update((items) =>
        items.map((item) =>
          item.id === actualizado.serieId
            ? { ...item, codigo: actualizado.serieCodigo, nombre: actualizado.serieNombre }
            : item,
        ),
      );
      this.cartasSignal.update((items) =>
        items.map((carta) =>
          carta.setId === actualizado.id
            ? { ...carta, setCodigo: actualizado.codigo, setNombre: actualizado.nombre }
            : carta,
        ),
      );
      return actualizado;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async eliminarSet(id: string): Promise<void> {
    try {
      await firstValueFrom(this.http.delete(apiUrl(`tcg/sets/${id}`)));
      this.setsSignal.update((items) => items.filter((item) => item.id !== id));
      this.cartasSignal.update((items) => items.filter((carta) => carta.setId !== id));
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async listarSets(serieId: string): Promise<TcgSet[]> {
    try {
      const params = new HttpParams().set('serieId', serieId);
      const items = await firstValueFrom(
        this.http.get<TcgSet[]>(apiUrl('tcg/sets'), { params }),
      );
      this.setsSignal.set(items);
      return items;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async listarCartas(setId: string, numero?: string, rareza?: RarezaTcg): Promise<TcgCarta[]> {
    try {
      let params = new HttpParams().set('setId', setId);
      if (numero?.trim()) {
        params = params.set('numero', numero.trim());
      }
      if (rareza) {
        params = params.set('rareza', rareza);
      }
      const items = await firstValueFrom(
        this.http.get<TcgCarta[]>(apiUrl('tcg/cartas'), { params }),
      );
      this.cartasSignal.set(items);
      return items;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }

  async parsearArchivo(file: File): Promise<ParseCatalogoResultado> {
    try {
      const buffer = await file.arrayBuffer();
      return await parsearCatalogoOficial(buffer, file.name);
    } catch (error) {
      throw new Error(error instanceof Error ? error.message : readApiError(error));
    }
  }

  async parsearTexto(texto: string, nombreArchivo = 'lista.txt'): Promise<ParseCatalogoResultado> {
    try {
      return await parsearCatalogoOficial(texto, nombreArchivo);
    } catch (error) {
      throw new Error(error instanceof Error ? error.message : readApiError(error));
    }
  }

  async importarSet(request: ImportarSetTcgRequest): Promise<ImportarSetTcgResponse> {
    try {
      const resultado = await firstValueFrom(
        this.http.post<ImportarSetTcgResponse>(apiUrl('tcg/cartas/importar-set'), request),
      );
      this.seriesSignal.update((items) => {
        const hay = items.some((item) => item.id === resultado.serie.id);
        return hay
          ? items.map((item) => (item.id === resultado.serie.id ? resultado.serie : item))
          : [...items, resultado.serie];
      });
      this.setsSignal.update((items) => {
        const hay = items.some((item) => item.id === resultado.set.id);
        return hay
          ? items.map((item) => (item.id === resultado.set.id ? resultado.set : item))
          : [...items, resultado.set];
      });
      this.cartasSignal.set(resultado.cartas);
      return resultado;
    } catch (error) {
      throw new Error(readApiError(error));
    }
  }
}
