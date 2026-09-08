export const TAMANOS_PAGINA_TABLA = [15, 30, 50] as const;

export type TamanoPaginaTabla = (typeof TAMANOS_PAGINA_TABLA)[number];

export const TAMANO_PAGINA_TABLA_DEFECTO: TamanoPaginaTabla = 15;

export interface ResultadoPaginado<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export function totalPaginasDe(total: number, pageSize: number): number {
  return Math.max(1, Math.ceil(Math.max(total, 0) / Math.max(pageSize, 1)));
}

export function rangoPaginaDe(
  page: number,
  pageSize: number,
  total: number,
): { desde: number; hasta: number; total: number } {
  if (total <= 0) {
    return { desde: 0, hasta: 0, total: 0 };
  }
  const desde = (page - 1) * pageSize + 1;
  const hasta = Math.min(page * pageSize, total);
  return { desde, hasta, total };
}

export function paginasVisibles(pagina: number, totalPaginas: number, ventana = 5): number[] {
  const total = Math.max(1, totalPaginas);
  const actual = Math.min(Math.max(1, pagina), total);
  if (total <= ventana) {
    return Array.from({ length: total }, (_, i) => i + 1);
  }
  const mitad = Math.floor(ventana / 2);
  let inicio = Math.max(1, actual - mitad);
  const fin = Math.min(total, inicio + ventana - 1);
  inicio = Math.max(1, fin - ventana + 1);
  return Array.from({ length: fin - inicio + 1 }, (_, i) => inicio + i);
}

export function esTamanoPaginaTabla(valor: number): valor is TamanoPaginaTabla {
  return (TAMANOS_PAGINA_TABLA as readonly number[]).includes(valor);
}
