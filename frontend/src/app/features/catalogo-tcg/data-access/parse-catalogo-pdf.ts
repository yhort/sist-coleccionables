import { RarezaTcg } from '../../productos-tcg/models/producto-tcg.model';

interface PdfTextItem {
  str: string;
  transform: number[];
  width: number;
  height: number;
  fontName: string;
}

export interface FilaPdfCatalogo {
  numero?: string;
  nombre?: string;
  tipo?: string;
  rareza?: string;
}

interface Glifo {
  str: string;
  x: number;
  y: number;
  w: number;
  h: number;
  fontName: string;
}

const ROMBO_RE = /[◆♦◇♢⬥⬦❖]/u;
const ESTRELLA_RE = /[★☆✦✧✪✯⋆✶✩✫✬✭✮✰⭐]/u;
const CASILLA_PURA_RE = /[■□▪▫⬛⬜▣☒☐◼◻◾◽]/u;

const ENCABEZADO_RE =
  /^(no\.?|n[o°º]|#|nombre( de la carta)?|card name|tipo|type|rareza|rarity|lista de cartas|expansión|expansion)$/i;

const RUIDO_PAGINA_RE =
  /^(www\.|https?:|página|page|pokémon tcg|jcc pokémon|©|the pokémon company|pokemon\.com|¡usa|casillas|llevar la cuenta)/i;

const TIPO_LATAM_RE = /^(part\.?|obj\.?|est\.?|e\.|entrenador|partidario|objeto|estadio)$/i;

let workerListo = false;

function asegurarWorker(GlobalWorkerOptions: { workerSrc: string }): void {
  if (workerListo) {
    return;
  }
  const base = typeof document !== 'undefined' ? document.baseURI : '/';
  GlobalWorkerOptions.workerSrc = new URL(
    'assets/pdfjs/pdf.worker.min.mjs',
    base,
  ).toString();
  workerListo = true;
}

export async function extraerCatalogoPdf(
  buffer: ArrayBuffer,
): Promise<{ filas: FilaPdfCatalogo[]; texto: string; advertencias: string[] }> {
  const { getDocument, GlobalWorkerOptions } = await import('pdfjs-dist');
  asegurarWorker(GlobalWorkerOptions);
  const copia = new ArrayBuffer(buffer.byteLength);
  new Uint8Array(copia).set(new Uint8Array(buffer));
  const loadingTask = getDocument({ data: copia, disableRange: true });
  const pdf = await loadingTask.promise;
  try {
    const filas: FilaPdfCatalogo[] = [];
    const textos: string[] = [];

    for (let n = 1; n <= pdf.numPages; n += 1) {
      const page = await pdf.getPage(n);
      const viewport = page.getViewport({ scale: 1 });
      const content = await page.getTextContent({ includeMarkedContent: false });
      const glifos: Glifo[] = [];
      for (const item of content.items) {
        if (!esTextItem(item)) {
          continue;
        }
        const glifo = aGlifo(item);
        if (glifo.str.trim().length > 0) {
          glifos.push(glifo);
        }
      }
      textos.push(glifos.map((g) => g.str).join(' '));
      filas.push(...filasDesdePagina(glifos, viewport.width));
    }

    const texto = textos.join('\n');
    const conRareza = filas.filter((f) => (f.rareza ?? '').length > 0).length;
    const advertencias: string[] = [];
    if (filas.length > 0 && conRareza === 0) {
      advertencias.push(
        'Esta lista LATAM no trae rareza (las casillas sirven para marcar la colección). Se asignó COMUN por defecto.',
      );
    }
    return { filas, texto, advertencias };
  } finally {
    await loadingTask.destroy();
  }
}

export function interpretarSimbolosRareza(valor: string, fontName = ''): RarezaTcg | null {
  const t = valor.trim();
  if (!t) {
    return null;
  }
  const fuenteSimbolo = esFuenteSimbolo(fontName);
  const { rombos, estrellas, estrellasClaras, casillas } = contarSimbolos(t, fuenteSimbolo);
  if (rombos + estrellas + casillas === 0) {
    return null;
  }
  if (casillas > 0 && estrellas === 0 && rombos === 0) {
    return 'ESPECIAL';
  }
  if (estrellasClaras >= 3 || (estrellas >= 3 && estrellasClaras > 0)) {
    return 'MEGA_HIPER_RARA';
  }
  if (estrellas >= 3) {
    return 'HIPER_RARA';
  }
  if (estrellasClaras === 2) {
    return 'ILUSTRACION_ESPECIAL';
  }
  if (estrellasClaras === 1) {
    return 'ILUSTRACION_RARA';
  }
  if (estrellas === 2) {
    return 'ULTRA';
  }
  if (estrellas === 1) {
    return 'DOBLE_RARA';
  }
  if (rombos >= 4) {
    return 'DOBLE_RARA';
  }
  if (rombos === 3) {
    return 'RARA';
  }
  if (rombos === 2) {
    return 'INFRECUENTE';
  }
  if (rombos === 1) {
    return 'COMUN';
  }
  return null;
}

function esTextItem(item: unknown): item is PdfTextItem {
  return typeof item === 'object' && item !== null && 'str' in item && 'transform' in item;
}

function aGlifo(item: PdfTextItem): Glifo {
  const [, , , , x, y] = item.transform;
  return {
    str: item.str.replace(/\u00a0/g, ' '),
    x,
    y,
    w: item.width,
    h: item.height,
    fontName: item.fontName,
  };
}

function filasDesdePagina(glifos: Glifo[], pageWidth: number): FilaPdfCatalogo[] {
  const numeros = glifos.filter((g) => esNumeroCarta(g.str));
  if (numeros.length === 0) {
    return [];
  }
  let columnas = detectarColumnas(
    numeros.map((g) => g.x),
    pageWidth,
  );
  if (columnas.length === 0) {
    columnas = [Math.min(...numeros.map((g) => g.x))];
  }
  const filas: FilaPdfCatalogo[] = [];
  for (let i = 0; i < columnas.length; i += 1) {
    const x0 = columnas[i] - 8;
    const x1 = i + 1 < columnas.length ? columnas[i + 1] - 12 : pageWidth + 20;
    const deColumna = glifos.filter((g) => g.x >= x0 && g.x < x1);
    filas.push(...filasDeColumna(deColumna));
  }
  return filas;
}

function detectarColumnas(xs: number[], _pageWidth: number): number[] {
  const ordenados = [...xs].sort((a, b) => a - b);
  const grupos: number[][] = [];
  // Listas LATAM: 3–6 columnas con ~100 pt de separación; el jitter del número es < 8 pt.
  const umbral = 40;
  for (const x of ordenados) {
    const grupo = grupos[grupos.length - 1];
    if (!grupo || x - grupo[grupo.length - 1] > umbral) {
      grupos.push([x]);
    } else {
      grupo.push(x);
    }
  }
  return grupos.map((grupo) => grupo.reduce((a, b) => a + b, 0) / grupo.length);
}

function filasDeColumna(glifos: Glifo[]): FilaPdfCatalogo[] {
  if (glifos.length === 0) {
    return [];
  }
  const alto = mediana(glifos.map((g) => g.h || 8)) || 8;
  const tolerancia = Math.max(4, alto * 0.65);
  const ordenados = [...glifos].sort((a, b) => b.y - a.y || a.x - b.x);
  const grupos: Glifo[][] = [];
  for (const glifo of ordenados) {
    const grupo = grupos.find((row) => Math.abs(mediaY(row) - glifo.y) <= tolerancia);
    if (grupo) {
      grupo.push(glifo);
    } else {
      grupos.push([glifo]);
    }
  }

  const filas: FilaPdfCatalogo[] = [];
  for (const grupo of grupos) {
    const fila = parsearFila(grupo.sort((a, b) => a.x - b.x));
    if (!fila) {
      continue;
    }
    if (!fila.numero && filas.length > 0 && fila.nombre) {
      if (esTextoDecorativo(fila.nombre) || /^\s*=/.test(fila.nombre)) {
        continue;
      }
      const previa = filas[filas.length - 1];
      previa.nombre = `${previa.nombre ?? ''} ${fila.nombre}`.trim();
      continue;
    }
    if (fila.numero) {
      filas.push(fila);
    }
  }
  return filas.filter((f) => (f.nombre ?? '').length > 1);
}

function parsearFila(glifos: Glifo[]): FilaPdfCatalogo | null {
  const textos = glifos.map((g) => g.str.trim()).filter(Boolean);
  if (textos.every((t) => ENCABEZADO_RE.test(t) || RUIDO_PAGINA_RE.test(t))) {
    return null;
  }
  const idxNumero = glifos.findIndex((g) => esNumeroCarta(g.str));
  const glifoNumero = idxNumero >= 0 ? glifos[idxNumero] : undefined;
  const numero = glifoNumero?.str.trim() ?? '';
  const resto = (idxNumero >= 0 ? glifos.slice(idxNumero + 1) : glifos).filter((g) => {
    const t = g.str.trim();
    return !ENCABEZADO_RE.test(t) && !RUIDO_PAGINA_RE.test(t) && !esTextoDecorativo(t);
  });

  const nombrePartes: string[] = [];
  const tipoPartes: string[] = [];
  const rarezaPartes: string[] = [];

  for (const glifo of resto) {
    const t = glifo.str.trim();
    if (!t) {
      continue;
    }
    if (esCasillaControl(glifo, glifoNumero)) {
      continue;
    }
    if (esGlifoRareza(glifo)) {
      rarezaPartes.push(normalizarGlifoRareza(glifo));
      continue;
    }
    if (TIPO_LATAM_RE.test(t)) {
      tipoPartes.push(t);
      continue;
    }
    nombrePartes.push(t);
  }

  const nombre = nombrePartes
    .join(' ')
    .replace(/\s+/g, ' ')
    .replace(/\s*=\s*(set est[aá]ndar|com[uú]n|infrecuente|rara).*$/i, '')
    .trim();
  if (!numero && !nombre) {
    return null;
  }
  return {
    numero: numero || undefined,
    nombre: nombre || undefined,
    tipo: tipoPartes.join(' ') || undefined,
    rareza: rarezaPartes.join('') || undefined,
  };
}

function esNumeroCarta(valor: string): boolean {
  return /^\d{1,3}(?:\s*\/\s*\d{1,3})?$/.test(valor.trim());
}

function esCasillaControl(glifo: Glifo, numero?: Glifo): boolean {
  const t = glifo.str.trim();
  if (![...t].every((ch) => CASILLA_PURA_RE.test(ch))) {
    return false;
  }
  if (!numero) {
    return t.length <= 2;
  }
  const dx = glifo.x - numero.x;
  return dx >= 0 && dx <= 30;
}

function esTextoDecorativo(valor: string): boolean {
  return /lista de cartas|jcc pokémon|pokemon\.com|megaevoluci[oó]n|ascenso heroico|héroes ascendentes|casillas para|nombres de los personajes|creatures inc|^\s*=\s*(set est[aá]ndar|com[uú]n|infrecuente|rara)/i.test(
    valor,
  );
}

function esFuenteSimbolo(fontName: string): boolean {
  return /zapf|dingbat|symbol|wingding|rarity|rareza|gding|gothicsymbols/i.test(fontName);
}

function esGlifoRareza(glifo: Glifo): boolean {
  const t = glifo.str.trim();
  if (contarSimbolos(t, esFuenteSimbolo(glifo.fontName)).total > 0) {
    return true;
  }
  if (esFuenteSimbolo(glifo.fontName) && t.length <= 6) {
    return true;
  }
  return false;
}

function normalizarGlifoRareza(glifo: Glifo): string {
  if (!esFuenteSimbolo(glifo.fontName)) {
    return glifo.str;
  }
  return [...glifo.str]
    .map((ch) => {
      const code = ch.charCodeAt(0);
      if (ch === 'l' || code === 108) {
        return '◆';
      }
      if (ch === 'n' || code === 110) {
        return '★';
      }
      if (ch === 'u' || code === 117) {
        return '■';
      }
      if (ROMBO_RE.test(ch) || ESTRELLA_RE.test(ch) || CASILLA_PURA_RE.test(ch)) {
        return ch;
      }
      if (code < 32 || (code >= 0xe000 && code <= 0xf8ff)) {
        return '◆';
      }
      return ch;
    })
    .join('');
}

function contarSimbolos(
  valor: string,
  fuenteSimbolo: boolean,
): { rombos: number; estrellas: number; estrellasClaras: number; casillas: number; total: number } {
  let rombos = 0;
  let estrellas = 0;
  let estrellasClaras = 0;
  let casillas = 0;
  for (const ch of valor) {
    if (CASILLA_PURA_RE.test(ch)) {
      casillas += 1;
      continue;
    }
    if (ESTRELLA_RE.test(ch)) {
      estrellas += 1;
      if (/[☆⭐✩✪✰]/.test(ch)) {
        estrellasClaras += 1;
      }
      continue;
    }
    if (ROMBO_RE.test(ch)) {
      rombos += 1;
      continue;
    }
    if (fuenteSimbolo) {
      if (ch === 'l') {
        rombos += 1;
      } else if (ch === 'n') {
        estrellas += 1;
      } else if (ch === 'u') {
        casillas += 1;
      }
    }
  }
  return {
    rombos,
    estrellas,
    estrellasClaras,
    casillas,
    total: rombos + estrellas + casillas,
  };
}

function mediaY(glifos: Glifo[]): number {
  return glifos.reduce((sum, g) => sum + g.y, 0) / glifos.length;
}

function mediana(valores: number[]): number {
  if (valores.length === 0) {
    return 0;
  }
  const orden = [...valores].sort((a, b) => a - b);
  return orden[Math.floor(orden.length / 2)];
}
