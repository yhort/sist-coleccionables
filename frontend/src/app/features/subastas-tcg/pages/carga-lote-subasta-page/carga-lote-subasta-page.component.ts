import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { SedesApiService } from '../../../../core/data-access/sedes-api.service';
import { SubastasTcgApiService } from '../../data-access/subastas-tcg.service';
import { LoteMasivoFilaLocal } from '../../models/lote-masivo.model';

const MAX_ITEMS = 50;

@Component({
  selector: 'app-carga-lote-subasta-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './carga-lote-subasta-page.component.html',
  styleUrl: './carga-lote-subasta-page.component.scss',
})
export class CargaLoteSubastaPageComponent {
  private readonly subastasApi = inject(SubastasTcgApiService);
  private readonly sedesApi = inject(SedesApiService);

  readonly filas = signal<LoteMasivoFilaLocal[]>([]);
  readonly sedeId = signal('');
  readonly intervaloMinutos = signal(1);
  readonly incrementoMinimo = signal(1);
  readonly horaFinBaseLocal = signal(defaultHoraFinLocal());
  readonly enviando = signal(false);
  readonly arrastrando = signal(false);
  readonly error = signal('');
  readonly ok = signal('');
  readonly codigoLote = signal('');

  readonly sedes = this.sedesApi.sedes;
  readonly total = computed(() => this.filas().length);
  readonly puedeEnviar = computed(
    () => !!this.sedeId() && this.filas().length > 0 && !this.enviando(),
  );

  constructor() {
    void this.bootstrap();
  }

  async bootstrap(): Promise<void> {
    const sedes = await this.sedesApi.refrescar();
    if (!this.sedeId() && sedes.length) {
      this.sedeId.set(sedes[0].id);
    }
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.arrastrando.set(true);
  }

  onDragLeave(): void {
    this.arrastrando.set(false);
  }

  async onDrop(event: DragEvent): Promise<void> {
    event.preventDefault();
    this.arrastrando.set(false);
    const files = Array.from(event.dataTransfer?.files ?? []).filter((f) =>
      f.type.startsWith('image/'),
    );
    await this.agregarArchivos(files);
  }

  async onFileInput(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    await this.agregarArchivos(files);
  }

  async agregarArchivos(files: File[]): Promise<void> {
    this.error.set('');
    const cupo = MAX_ITEMS - this.filas().length;
    if (cupo <= 0) {
      this.error.set(`Máximo ${MAX_ITEMS} imágenes por lote.`);
      return;
    }

    const seleccion = files.slice(0, cupo);
    const nuevas: LoteMasivoFilaLocal[] = [];
    let index = this.filas().length;

    for (const file of seleccion) {
      const encoded = await fileToDataUrlCompressed(file);
      const hora = sumarMinutosLocal(this.horaFinBaseLocal(), this.intervaloMinutos() * index);
      nuevas.push({
        id: crypto.randomUUID(),
        titulo: nombreSinExtension(file.name),
        precioBase: 1,
        horaFinLocal: hora,
        observaciones: '',
        previewUrl: encoded.dataUrl,
        imagenBase64: encoded.dataUrl,
        imagenNombreArchivo: file.name,
        imagenContentType: encoded.contentType,
      });
      index += 1;
    }

    this.filas.update((actual) => [...actual, ...nuevas]);
    if (files.length > cupo) {
      this.error.set(`Solo se agregaron ${cupo} de ${files.length} (límite ${MAX_ITEMS}).`);
    }
  }

  actualizarFila(id: string, patch: Partial<LoteMasivoFilaLocal>): void {
    this.filas.update((rows) =>
      rows.map((row) => (row.id === id ? { ...row, ...patch } : row)),
    );
  }

  eliminarFila(id: string): void {
    this.filas.update((rows) => rows.filter((row) => row.id !== id));
  }

  reescalonarHoras(): void {
    const base = this.horaFinBaseLocal();
    const intervalo = this.intervaloMinutos();
    this.filas.update((rows) =>
      rows.map((row, i) => ({
        ...row,
        horaFinLocal: sumarMinutosLocal(base, intervalo * i),
      })),
    );
  }

  async copiarTexto(fila: LoteMasivoFilaLocal): Promise<void> {
    const texto =
      fila.textoFacebook ??
      borradorTextoFacebook(fila.titulo, fila.precioBase, fila.horaFinLocal, this.filas().indexOf(fila) + 1);
    try {
      await navigator.clipboard.writeText(texto);
      this.ok.set(`Texto del artículo #${fila.numArticulo ?? this.filas().indexOf(fila) + 1} copiado.`);
    } catch {
      this.error.set('No se pudo copiar al portapapeles.');
    }
  }

  exportarJson(): void {
    const payload = {
      sedeId: this.sedeId(),
      intervaloMinutos: this.intervaloMinutos(),
      incrementoMinimo: this.incrementoMinimo(),
      horaFinBase: localInputToIso(this.horaFinBaseLocal()),
      items: this.filas().map((f, i) => ({
        numArticulo: i + 1,
        titulo: f.titulo,
        precioBase: f.precioBase,
        horaFin: localInputToIso(f.horaFinLocal),
        observaciones: f.observaciones,
        imagenNombreArchivo: f.imagenNombreArchivo,
        textoFacebook: f.textoFacebook ?? null,
        subastaId: f.subastaId ?? null,
      })),
    };
    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `lote-fb-${Date.now()}.json`;
    a.click();
    URL.revokeObjectURL(url);
  }

  async crearLote(): Promise<void> {
    this.error.set('');
    this.ok.set('');
    if (!this.puedeEnviar()) {
      return;
    }

    this.enviando.set(true);
    try {
      const respuesta = await this.subastasApi.crearLoteMasivo({
        sedeId: this.sedeId(),
        fechaInicio: new Date().toISOString(),
        horaFinBase: localInputToIso(this.horaFinBaseLocal()),
        intervaloMinutos: this.intervaloMinutos(),
        incrementoMinimo: this.incrementoMinimo(),
        items: this.filas().map((f) => ({
          titulo: f.titulo.trim(),
          precioBase: Number(f.precioBase),
          horaFin: localInputToIso(f.horaFinLocal),
          observaciones: f.observaciones?.trim() || null,
          imagenBase64: f.imagenBase64,
          imagenNombreArchivo: f.imagenNombreArchivo,
          imagenContentType: f.imagenContentType,
        })),
      });

      this.codigoLote.set(respuesta.codigoLote);
      const porArt = new Map(respuesta.items.map((i) => [i.numArticulo, i]));
      this.filas.update((rows) =>
        rows.map((row, i) => {
          const item = porArt.get(i + 1);
          if (!item) {
            return row;
          }
          return {
            ...row,
            textoFacebook: item.textoFacebook,
            subastaId: item.subastaId,
            numArticulo: item.numArticulo,
            previewUrl: item.imagenUrl || row.previewUrl,
          };
        }),
      );
      this.ok.set(
        `Lote #${respuesta.codigoLote} creado: ${respuesta.totalItems} borradores listos para Facebook.`,
      );
      await this.subastasApi.refrescar();
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'No se pudo crear el lote.');
    } finally {
      this.enviando.set(false);
    }
  }
}

function defaultHoraFinLocal(): string {
  const d = new Date();
  d.setHours(21, 0, 0, 0);
  if (d.getTime() <= Date.now()) {
    d.setDate(d.getDate() + 1);
  }
  return toLocalInputValue(d);
}

function toLocalInputValue(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function sumarMinutosLocal(localValue: string, minutos: number): string {
  const base = new Date(localValue);
  if (Number.isNaN(base.getTime())) {
    return localValue;
  }
  base.setMinutes(base.getMinutes() + minutos);
  return toLocalInputValue(base);
}

function localInputToIso(localValue: string): string {
  const d = new Date(localValue);
  return Number.isNaN(d.getTime()) ? new Date().toISOString() : d.toISOString();
}

function nombreSinExtension(name: string): string {
  return name.replace(/\.[^.]+$/, '').replace(/[_-]+/g, ' ').trim() || 'Artículo';
}

function borradorTextoFacebook(
  titulo: string,
  precio: number,
  horaFinLocal: string,
  numArt: number,
): string {
  const d = new Date(horaFinLocal);
  const hora = Number.isNaN(d.getTime())
    ? horaFinLocal
    : d.toLocaleString('es-PE', { dateStyle: 'short', timeStyle: 'short' });
  return [
    'SUBASTA : #BORRADOR',
    `ARTÍCULO #${numArt}`,
    `PRODUCTO: ${titulo}`,
    `PRECIO: ${precio} SOLES`,
    `FINALIZA: ${hora}`,
    'ENTREGA: Jr. Carabaya #1150',
  ].join('\n');
}

async function fileToDataUrlCompressed(file: File): Promise<{ dataUrl: string; contentType: string }> {
  if (file.size <= 900_000 && file.type === 'image/jpeg') {
    const dataUrl = await readAsDataUrl(file);
    return { dataUrl, contentType: 'image/jpeg' };
  }

  const bitmap = await createImageBitmap(file);
  const maxSide = 1600;
  const scale = Math.min(1, maxSide / Math.max(bitmap.width, bitmap.height));
  const w = Math.max(1, Math.round(bitmap.width * scale));
  const h = Math.max(1, Math.round(bitmap.height * scale));
  const canvas = document.createElement('canvas');
  canvas.width = w;
  canvas.height = h;
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    const dataUrl = await readAsDataUrl(file);
    return { dataUrl, contentType: file.type || 'image/jpeg' };
  }
  ctx.drawImage(bitmap, 0, 0, w, h);
  bitmap.close();
  const dataUrl = canvas.toDataURL('image/jpeg', 0.82);
  return { dataUrl, contentType: 'image/jpeg' };
}

function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result ?? ''));
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}
