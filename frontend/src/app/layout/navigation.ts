export type NavIconName =
  | 'dashboard'
  | 'productos'
  | 'inventario'
  | 'aperturas'
  | 'subastas'
  | 'pedidos'
  | 'pagos'
  | 'caja'
  | 'entregas'
  | 'clientes'
  | 'proveedores'
  | 'woocommerce'
  | 'reportes'
  | 'ecosistema';

export interface AppNavItem {
  label: string;
  route: string;
  exact: boolean;
  icon: NavIconName;
  keywords: readonly string[];
}

export const APP_NAV_ITEMS: readonly AppNavItem[] = [
  {
    label: 'Dashboard',
    route: '/app/dashboard',
    exact: true,
    icon: 'dashboard',
    keywords: ['inicio', 'kpis', 'resumen'],
  },
  {
    label: 'Productos TCG',
    route: '/app/productos-tcg',
    exact: false,
    icon: 'productos',
    keywords: ['cartas', 'sellados', 'accesorios', 'compuestos', 'catalogo'],
  },
  {
    label: 'Inventario',
    route: '/app/inventario',
    exact: false,
    icon: 'inventario',
    keywords: ['stock', 'kardex', 'almacen'],
  },
  {
    label: 'Aperturas TCG',
    route: '/app/aperturas-tcg',
    exact: false,
    icon: 'aperturas',
    keywords: ['abrir', 'sobres', 'cajas', 'sellado'],
  },
  {
    label: 'Subastas TCG',
    route: '/app/subastas-tcg',
    exact: false,
    icon: 'subastas',
    keywords: ['pujas', 'facebook', 'lote'],
  },
  {
    label: 'Pedidos Digitales',
    route: '/app/pedidos-digitales',
    exact: false,
    icon: 'pedidos',
    keywords: ['kanban', 'whatsapp', 'tienda'],
  },
  {
    label: 'Pagos',
    route: '/app/pagos',
    exact: false,
    icon: 'pagos',
    keywords: ['yape', 'izipay', 'bandeja'],
  },
  {
    label: 'Caja',
    route: '/app/caja',
    exact: false,
    icon: 'caja',
    keywords: ['apertura', 'cierre', 'arqueo', 'turno', 'efectivo', 'caja chica'],
  },
  {
    label: 'Entregas',
    route: '/app/entregas',
    exact: false,
    icon: 'entregas',
    keywords: ['courier', 'tracking', 'despacho'],
  },
  {
    label: 'Clientes',
    route: '/app/clientes',
    exact: false,
    icon: 'clientes',
    keywords: ['dni', 'ruc', 'varios', 'publico', 'maestro'],
  },
  {
    label: 'Proveedores',
    route: '/app/proveedores',
    exact: false,
    icon: 'proveedores',
    keywords: ['ruc', 'razon social', 'compra', 'kardex'],
  },
  {
    label: 'WooCommerce',
    route: '/app/woocommerce',
    exact: false,
    icon: 'woocommerce',
    keywords: ['tienda', 'sync', 'web'],
  },
  {
    label: 'Reportes',
    route: '/app/reportes',
    exact: false,
    icon: 'reportes',
    keywords: ['ventas', 'canales', 'exportar'],
  },
  {
    label: 'Ecosistema',
    route: '/app/ecosistema',
    exact: false,
    icon: 'ecosistema',
    keywords: ['cpe', 'sunat', 'conexiones', 'salud', 'sedes', 'usuarios', 'permisos', 'webhook'],
  },
];
