import { Component, HostListener, OnInit, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { readApiError } from '../../../../core/http/api-error';
import {
  ETIQUETAS_RAREZA,
  ETIQUETAS_TIPO_CARTA,
} from '../../../productos-tcg/models/producto-tcg.model';
import { CatalogoTcgApiService } from '../../data-access/catalogo-tcg.service';
import { ParseCatalogoResultado } from '../../data-access/parse-catalogo-oficial';
import { ImportarSetTcgResponse, TcgSerie, TcgSet } from '../../models/catalogo-tcg.model';

@Component({
  selector: 'app-importar-catalogo-dialog',
  imports: [ReactiveFormsModule],
  templateUrl: './importar-catalogo-dialog.component.html',
  styleUrl: './importar-catalogo-dialog.component.scss',
})
export class ImportarCatalogoDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly catalogoApi = inject(CatalogoTcgApiService);

  readonly serieActual = input<TcgSerie | null>(null);
  readonly setActual = input<TcgSet | null>(null);
  readonly saved = output<ImportarSetTcgResponse>();
  readonly cancelled = output<void>();

  readonly error = signal('');
  readonly aviso = signal('');
  readonly parseando = signal(false);
  readonly importando = signal(false);
  readonly resultado = signal<ParseCatalogoResultado | null>(null);
  readonly archivoNombre = signal('');
  readonly etiquetasRareza = ETIQUETAS_RAREZA;
  readonly etiquetasTipo = ETIQUETAS_TIPO_CARTA;

  readonly form = this.fb.nonNullable.group({
    juego: ['Pokémon', Validators.required],
    serieCodigo: ['', Validators.required],
    serieNombre: ['', Validators.required],
    setCodigo: ['', Validators.required],
    setNombre: ['', Validators.required],
    setNombreEn: [''],
    codigoImpresion: [''],
    lista: [''],
  });

  ngOnInit(): void {
    const serie = this.serieActual();
    const set = this.setActual();
    this.form.patchValue({
      juego: serie?.juego || 'Pokémon',
      serieCodigo: serie?.codigo || '',
      serieNombre: serie?.nombre || '',
      setCodigo: set?.codigo || '',
      setNombre: set?.nombre || '',
      setNombreEn: set?.nombreEn || '',
      codigoImpresion: set?.codigoImpresion || '',
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.cancelled.emit();
  }

  async onArchivo(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    if (file.name.toLowerCase().endsWith('.pdf') || file.type === 'application/pdf') {
      this.resultado.set(null);
      this.archivoNombre.set('');
      this.error.set('Usa un archivo CSV o pega la lista en texto plano.');
      return;
    }
    this.archivoNombre.set(file.name);
    this.parseando.set(true);
    this.error.set('');
    try {
      const parsed = await this.catalogoApi.parsearArchivo(file);
      this.aplicarParseo(parsed);
    } catch (err) {
      this.resultado.set(null);
      this.error.set(err instanceof Error ? err.message : readApiError(err));
    } finally {
      this.parseando.set(false);
    }
  }

  async parsearLista(): Promise<void> {
    const texto = this.form.controls.lista.value.trim();
    if (!texto) {
      this.error.set('Pega una lista o elige un archivo CSV.');
      return;
    }
    this.parseando.set(true);
    this.error.set('');
    try {
      const parsed = await this.catalogoApi.parsearTexto(texto, this.archivoNombre() || 'lista.txt');
      this.aplicarParseo(parsed);
    } catch (err) {
      this.resultado.set(null);
      this.error.set(err instanceof Error ? err.message : readApiError(err));
    } finally {
      this.parseando.set(false);
    }
  }

  async importar(): Promise<void> {
    this.form.markAllAsTouched();
    this.error.set('');
    const parsed = this.resultado();
    if (this.form.invalid || !parsed?.cartas.length) {
      this.error.set('Completa serie/set y carga un CSV o una lista pegada con fichas.');
      return;
    }
    const raw = this.form.getRawValue();
    this.importando.set(true);
    try {
      const resultado = await this.catalogoApi.importarSet({
        serie: {
          juego: raw.juego.trim(),
          codigo: raw.serieCodigo.trim(),
          nombre: raw.serieNombre.trim(),
          activa: true,
        },
        set: {
          codigo: raw.setCodigo.trim(),
          nombre: raw.setNombre.trim(),
          nombreEn: raw.setNombreEn.trim() || null,
          codigoImpresion: raw.codigoImpresion.trim() || null,
          totalCartas: parsed.cartas.length,
        },
        cartas: parsed.cartas,
      });
      this.saved.emit(resultado);
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : readApiError(err));
    } finally {
      this.importando.set(false);
    }
  }

  private aplicarParseo(parsed: ParseCatalogoResultado): void {
    this.resultado.set(parsed);
    this.aviso.set(
      parsed.advertencias.length
        ? `${parsed.cartas.length} fichas listas. ${parsed.advertencias.length} aviso(s).`
        : `${parsed.cartas.length} fichas listas para importar.`,
    );
    const patch: Record<string, string> = {};
    if (!this.form.controls.setCodigo.value && parsed.meta.setCodigo) {
      patch['setCodigo'] = parsed.meta.setCodigo;
    }
    if (!this.form.controls.setNombre.value && parsed.meta.setNombre) {
      patch['setNombre'] = parsed.meta.setNombre;
    }
    if (!this.form.controls.setNombreEn.value && parsed.meta.setNombreEn) {
      patch['setNombreEn'] = parsed.meta.setNombreEn;
    }
    if (!this.form.controls.codigoImpresion.value && parsed.meta.codigoImpresion) {
      patch['codigoImpresion'] = parsed.meta.codigoImpresion;
    }
    if (Object.keys(patch).length) {
      this.form.patchValue(patch);
    }
  }
}
