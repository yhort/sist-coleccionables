import { RarezaTcg, TipoCartaTcg } from '../../productos-tcg/models/producto-tcg.model';
import { UpsertTcgCartaRequest } from '../models/catalogo-tcg.model';
import { extraerCatalogoPdf, interpretarSimbolosRareza } from './parse-catalogo-pdf';

export interface MetaCatalogoInferida {
  setCodigo?: string;
  setNombre?: string;
  setNombreEn?: string;
  codigoImpresion?: string;
}

export interface ParseCatalogoResultado {
  cartas: UpsertTcgCartaRequest[];
  advertencias: string[];
  meta: MetaCatalogoInferida;
}

const RAREZAS: Array<{ keys: string[]; valor: RarezaTcg }> = [
  { keys: ['mega hyper rare', 'mega hiper rara', 'mega hyper', 'rara híper mega', 'rara hiper mega', 'mhr'], valor: 'MEGA_HIPER_RARA' },
  { keys: ['special illustration rare', 'ilustracion especial', 'ilustración especial', 'rara ilustración especial', 'rara ilustracion especial', 'sir'], valor: 'ILUSTRACION_ESPECIAL' },
  { keys: ['illustration rare', 'ilustracion rara', 'ilustración rara', 'rara ilustración', 'rara ilustracion', 'ir'], valor: 'ILUSTRACION_RARA' },
  { keys: ['double rare', 'doble rara', 'rara doble', 'rr'], valor: 'DOBLE_RARA' },
  { keys: ['ultra rare', 'ultra rara', 'rara ultra', 'ur'], valor: 'ULTRA' },
  { keys: ['hyper rare', 'hiper rara', 'hr'], valor: 'HIPER_RARA' },
  { keys: ['secret rare', 'secreta', 'sr'], valor: 'SECRETA' },
  { keys: ['rare holo', 'holo rare', 'rara holo', 'rh'], valor: 'RARA_HOLO' },
  { keys: ['ace spec', 'especial', 'special'], valor: 'ESPECIAL' },
  { keys: ['uncommon', 'infrecuente', 'u'], valor: 'INFRECUENTE' },
  { keys: ['common', 'comun', 'común', 'c'], valor: 'COMUN' },
  { keys: ['promo', 'p'], valor: 'PROMO' },
  { keys: ['rare', 'rara', 'r'], valor: 'RARA' },
];

const TIPOS: Array<{ keys: string[]; valor: TipoCartaTcg }> = [
  { keys: ['pokemon', 'pokémon'], valor: 'POKEMON' },
  { keys: ['trainer', 'entrenador', 'item', 'supporter', 'stadium', 'tool'], valor: 'ENTRENADOR' },
  { keys: ['energy', 'energia', 'energía'], valor: 'ENERGIA' },
];

interface FilaCatalogo {
  numero?: string;
  nombre?: string;
  tipo?: string;
  rareza?: string;
}

export async function parsearCatalogoOficial(
  contenido: string | ArrayBuffer,
  nombreArchivo = '',
): Promise<ParseCatalogoResultado> {
  if (contenido instanceof ArrayBuffer && esPdf(contenido)) {
    try {
      const extraido = await extraerCatalogoPdf(contenido);
      if (extraido.filas.length > 0) {
        const resultado = consolidar(extraido.filas, extraido.texto, nombreArchivo);
        resultado.advertencias = [...extraido.advertencias, ...resultado.advertencias];
        return resultado;
      }
    } catch (error) {
      throw new Error(
        error instanceof Error
          ? `No se pudo leer el PDF: ${error.message}`
          : 'No se pudo leer el PDF de la lista oficial.',
      );
    }
    throw new Error(
      'El PDF no tiene filas de fichas reconocibles. Comprueba que sea una lista oficial LATAM (número, nombre y rareza).',
    );
  }

  const texto = contenido instanceof ArrayBuffer ? decodeBytes(contenido) : contenido;
  if (!texto.trim()) {
    throw new Error('El archivo está vacío o no se pudo leer.');
  }
  const csv = parsearCsvSiAplica(texto);
  const crudas = csv ?? parsearLineasSueltas(texto);
  return consolidar(crudas, texto, nombreArchivo);
}

function consolidar(
  crudas: FilaCatalogo[],
  texto: string,
  nombreArchivo: string,
): ParseCatalogoResultado {
  if (crudas.length === 0) {
    throw new Error(
      'No se encontraron fichas. Usa CSV con columnas Número, Nombre, Tipo y Rareza, o una lista oficial.',
    );
  }

  const advertencias: string[] = [];
  const porClave = new Map<string, UpsertTcgCartaRequest>();
  for (const cruda of crudas) {
    const carta = normalizarCarta(cruda);
    if (!carta) {
      advertencias.push(`Se omitió una fila sin número o nombre: ${JSON.stringify(cruda)}`);
      continue;
    }
    const clave = `${carta.numero}|${carta.rareza}`;
    if (porClave.has(clave)) {
      advertencias.push(`Duplicada (${carta.numero} · ${carta.rareza}); se conserva la última.`);
    }
    porClave.set(clave, carta);
  }

  const cartas = [...porClave.values()].sort((a, b) => a.numero.localeCompare(b.numero, 'en'));
  if (cartas.length === 0) {
    throw new Error('El archivo no tiene fichas válidas (número + nombre).');
  }

  return {
    cartas,
    advertencias,
    meta: inferirMeta(texto, nombreArchivo),
  };
}

function esPdf(buffer: ArrayBuffer): boolean {
  const head = new TextDecoder('latin1').decode(buffer.slice(0, 5));
  return head.startsWith('%PDF');
}

function decodeBytes(buffer: ArrayBuffer): string {
  const utf8 = new TextDecoder('utf-8', { fatal: false }).decode(buffer);
  if (utf8.includes('\uFFFD') && utf8.indexOf('\uFFFD') < 40) {
    return new TextDecoder('latin1').decode(buffer);
  }
  return utf8.replace(/^\uFEFF/, '');
}

function parsearCsvSiAplica(texto: string): FilaCatalogo[] | null {
  const lineas = texto
    .split(/\r?\n/)
    .map((linea) => linea.trim())
    .filter((linea) => linea.length > 0 && !linea.startsWith('#'));
  if (lineas.length < 2) {
    return null;
  }
  const delimitador = detectarDelimitador(lineas[0]);
  if (!delimitador) {
    return null;
  }
  const encabezados = dividirFila(lineas[0], delimitador).map(normalizarEncabezado);
  if (!encabezados.some((h) => h === 'numero' || h === 'nombre')) {
    return null;
  }
  return lineas.slice(1).map((linea) => {
    const celdas = dividirFila(linea, delimitador);
    const fila: FilaCatalogo = {};
    encabezados.forEach((clave, i) => {
      const valor = (celdas[i] ?? '').trim();
      if (clave === 'numero') fila.numero = valor;
      if (clave === 'nombre') fila.nombre = valor;
      if (clave === 'tipo') fila.tipo = valor;
      if (clave === 'rareza') fila.rareza = valor;
    });
    return fila;
  });
}

function detectarDelimitador(encabezado: string): ',' | ';' | '\t' | '|' | null {
  const candidatos: Array<',' | ';' | '\t' | '|'> = ['\t', ';', ',', '|'];
  const hayNumero = /numero|number|no\.?|#|collector/i.test(encabezado);
  const hayNombre = /nombre|name/i.test(encabezado);
  if (!hayNumero && !hayNombre) {
    return null;
  }
  return candidatos.find((d) => encabezado.split(d).length >= 2) ?? null;
}

function dividirFila(linea: string, delimitador: string): string[] {
  const celdas: string[] = [];
  let actual = '';
  let entreComillas = false;
  for (let i = 0; i < linea.length; i += 1) {
    const ch = linea[i];
    if (ch === '"') {
      entreComillas = !entreComillas;
      continue;
    }
    if (ch === delimitador && !entreComillas) {
      celdas.push(actual);
      actual = '';
      continue;
    }
    actual += ch;
  }
  celdas.push(actual);
  return celdas;
}

function normalizarEncabezado(valor: string): string {
  const t = valor.trim().toLowerCase().replace(/['"]/g, '');
  if (/^(n[o°º.]?|#|numero|número|number|collector|card\s*number)$/.test(t)) {
    return 'numero';
  }
  if (/^(nombre|name|card\s*name)$/.test(t)) {
    return 'nombre';
  }
  if (/^(tipo|type|card\s*type)$/.test(t)) {
    return 'tipo';
  }
  if (/^(rareza|rarity)$/.test(t)) {
    return 'rareza';
  }
  return t;
}

function parsearLineasSueltas(texto: string): FilaCatalogo[] {
  const filas: FilaCatalogo[] = [];
  const visto = new Set<string>();
  const rarezaAlt = RAREZAS.flatMap((r) => r.keys)
    .filter((clave) => clave.length > 1)
    .sort((a, b) => b.length - a.length);
  const rarezaGrupo = rarezaAlt.map(escapeRegExp).join('|');
  const lineaRe = new RegExp(
    `\\b(\\d{1,3})\\s+([A-Za-z0-9À-ÿ'’\\.\\- ]+?)\\s+(${rarezaGrupo})\\b`,
    'gi',
  );

  for (const linea of texto.split(/\r?\n/)) {
    extraerDeTexto(linea, lineaRe, filas, visto);
  }
  extraerDeTexto(texto.replace(/\s+/g, ' '), lineaRe, filas, visto);
  return filas;
}

function extraerDeTexto(
  texto: string,
  regex: RegExp,
  filas: FilaCatalogo[],
  visto: Set<string>,
): void {
  regex.lastIndex = 0;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(texto))) {
    const numero = match[1];
    let nombre = limpiarNombre(match[2]);
    const rareza = match[3];
    const tipo = extraerTipoDeNombre(nombre);
    if (tipo) {
      nombre = tipo.nombre;
    }
    const clave = `${normalizarNumero(numero)}|${nombre.toLowerCase()}|${rareza.toLowerCase()}`;
    if (visto.has(clave) || nombre.length < 2) {
      continue;
    }
    visto.add(clave);
    filas.push({
      numero,
      nombre,
      tipo: tipo?.tipo ?? '',
      rareza,
    });
  }
}

function extraerTipoDeNombre(nombre: string): { nombre: string; tipo: string } | null {
  const partes = nombre.trim().split(/\s+/);
  if (partes.length < 2) {
    return null;
  }
  const ultimo = partes[partes.length - 1];
  const tipo = mapearTipo(ultimo);
  if (!tipo || tipo === 'OTRO') {
    return null;
  }
  if (!TIPOS.some((t) => t.keys.includes(ultimo.toLowerCase()))) {
    return null;
  }
  return { nombre: partes.slice(0, -1).join(' '), tipo: ultimo };
}

function limpiarNombre(valor: string): string {
  return valor
    .replace(/\s+/g, ' ')
    .replace(/\b(pokémon|pokemon|trainer|entrenador|energy|energía|energia)\s*$/i, '')
    .trim();
}

function normalizarCarta(fila: FilaCatalogo): UpsertTcgCartaRequest | null {
  const numero = normalizarNumero(fila.numero ?? '');
  const nombre = (fila.nombre ?? '').replace(/\s+/g, ' ').trim();
  if (!numero || !nombre) {
    return null;
  }
  return {
    numero,
    nombre,
    tipoCarta: mapearTipo(fila.tipo ?? nombre),
    rareza: mapearRareza(fila.rareza ?? ''),
  };
}

function normalizarNumero(valor: string): string {
  const texto = valor.trim().replace(/^#/, '').replace(/\/.*$/, '');
  if (!texto) {
    return '';
  }
  if (/^\d+$/.test(texto)) {
    return texto.padStart(3, '0');
  }
  return texto.toUpperCase();
}

function mapearRareza(valor: string): RarezaTcg {
  const porSimbolo = interpretarSimbolosRareza(valor);
  if (porSimbolo) {
    return porSimbolo;
  }
  const t = valor.trim().toLowerCase().replace(/[_-]+/g, ' ');
  if (!t) {
    return 'COMUN';
  }
  for (const item of RAREZAS) {
    for (const clave of item.keys) {
      if (clave.length <= 3) {
        if (t === clave) {
          return item.valor;
        }
        continue;
      }
      if (t === clave || t.includes(clave)) {
        return item.valor;
      }
    }
  }
  return 'COMUN';
}

function mapearTipo(valor: string): TipoCartaTcg {
  const t = valor.trim().toLowerCase();
  if (/^(part\.?|obj\.?|est\.?|entrenador|partidario|objeto|estadio)$/.test(t)) {
    return 'ENTRENADOR';
  }
  if (/^(e\.|energ[ií]a)$/.test(t)) {
    return 'ENERGIA';
  }
  const hit = TIPOS.find((item) => item.keys.some((k) => t === k || t.includes(k)));
  return hit?.valor ?? 'POKEMON';
}

function inferirMeta(texto: string, nombreArchivo: string): MetaCatalogoInferida {
  const fuente = `${nombreArchivo}\n${texto.slice(0, 2500)}`;
  const impresion = fuente.match(/\bP\d{4,6}\b/i)?.[0]?.toUpperCase();
  const codigoArchivo = nombreArchivo.match(/^([a-z]{2,5})_web_cardlist/i)?.[1]?.toUpperCase();
  const setCodigo =
    codigoArchivo ??
    fuente.match(/\b(ASC|ME\d{2}|M\d|SV\d{1,2}|TWM|OBF|PAL|PAR|TEF)\b/i)?.[0]?.toUpperCase() ??
    nombreArchivo.match(/\b([A-Z]{1,4}\d{1,3})\b/i)?.[1]?.toUpperCase();
  const nombreEn = fuente.match(
    /Perfect Order|Prismatic Evolutions|Surging Sparks|Journey Together|Ascenso Heroico|Héroes Ascendentes|Heroes Ascendentes|Ascended Heroes|Heroic Rise/i,
  )?.[0];
  return {
    setCodigo,
    setNombreEn: nombreEn,
    setNombre: nombreEn,
    codigoImpresion: impresion,
  };
}

function escapeRegExp(valor: string): string {
  return valor.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
