import { Routes } from '@angular/router';

import { authGuard, guestGuard, permissionGuard } from './core/auth/auth.guards';
import { ShellComponent } from './layout/shell/shell.component';
import { ModuloPermiso } from './features/ecosistema/models/ecosistema.model';

function moduleRoute(
  path: string,
  title: string,
  modulo: ModuloPermiso,
  loadComponent: NonNullable<Routes[number]['loadComponent']>,
): Routes[number] {
  return {
    path,
    title: `${title} | Trunqi TCG`,
    canActivate: [permissionGuard],
    data: {
      pageTitle: title,
      module: modulo,
      breadcrumb: [
        { label: 'Inicio', route: '/app/dashboard' },
        { label: title },
      ],
    },
    loadComponent,
  };
}

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'app/dashboard',
  },
  {
    path: 'login',
    title: 'Iniciar sesión | Trunqi TCG',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/pages/login-page/login-page.component').then(
        (m) => m.LoginPageComponent,
      ),
  },
  {
    path: 'app',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard',
      },
      moduleRoute('dashboard', 'Dashboard', 'dashboard', () =>
        import('./features/dashboard/pages/dashboard-page/dashboard-page.component').then(
          (m) => m.DashboardPageComponent,
        ),
      ),
      moduleRoute('productos-tcg', 'Productos TCG', 'productos-tcg', () =>
        import(
          './features/productos-tcg/pages/productos-tcg-page/productos-tcg-page.component'
        ).then((m) => m.ProductosTcgPageComponent),
      ),
      {
        path: 'inventario/kardex',
        title: 'Kardex | Trunqi TCG',
        canActivate: [permissionGuard],
        data: {
          pageTitle: 'Kardex',
          module: 'inventario',
          breadcrumb: [
            { label: 'Inicio', route: '/app/dashboard' },
            { label: 'Inventario', route: '/app/inventario' },
            { label: 'Kardex' },
          ],
        },
        loadComponent: () =>
          import('./features/inventario/pages/kardex-page/kardex-page.component').then(
            (m) => m.KardexPageComponent,
          ),
      },
      moduleRoute('inventario', 'Inventario', 'inventario', () =>
        import('./features/inventario/pages/inventario-page/inventario-page.component').then(
          (m) => m.InventarioPageComponent,
        ),
      ),
      moduleRoute('aperturas-tcg', 'Aperturas TCG', 'aperturas-tcg', () =>
        import(
          './features/aperturas-tcg/pages/aperturas-tcg-page/aperturas-tcg-page.component'
        ).then((m) => m.AperturasTcgPageComponent),
      ),
      {
        path: 'subastas-tcg/:id',
        title: 'Pujas | Trunqi TCG',
        canActivate: [permissionGuard],
        data: {
          pageTitle: 'Detalle de subasta',
          module: 'subastas-tcg',
          breadcrumb: [
            { label: 'Inicio', route: '/app/dashboard' },
            { label: 'Subastas TCG', route: '/app/subastas-tcg' },
            { label: 'Pujas' },
          ],
        },
        loadComponent: () =>
          import(
            './features/subastas-tcg/pages/subasta-detalle-page/subasta-detalle-page.component'
          ).then((m) => m.SubastaDetallePageComponent),
      },
      moduleRoute('subastas-tcg', 'Subastas TCG', 'subastas-tcg', () =>
        import('./features/subastas-tcg/pages/subastas-tcg-page/subastas-tcg-page.component').then(
          (m) => m.SubastasTcgPageComponent,
        ),
      ),
      moduleRoute('pedidos-digitales', 'Pedidos Digitales', 'pedidos-digitales', () =>
        import(
          './features/pedidos-digitales/pages/bandeja-pedidos-digitales-page/bandeja-pedidos-digitales-page.component'
        ).then((m) => m.BandejaPedidosDigitalesPageComponent),
      ),
      moduleRoute('pagos', 'Pagos', 'pagos', () =>
        import('./features/pagos/pages/pagos-bandeja-page/pagos-bandeja-page.component').then(
          (m) => m.PagosBandejaPageComponent,
        ),
      ),
      moduleRoute('caja', 'Caja', 'caja', () =>
        import('./features/caja/pages/caja-page/caja-page.component').then(
          (m) => m.CajaPageComponent,
        ),
      ),
      moduleRoute('entregas', 'Entregas', 'entregas', () =>
        import('./features/entregas/pages/entregas-page/entregas-page.component').then(
          (m) => m.EntregasPageComponent,
        ),
      ),
      moduleRoute('clientes', 'Clientes', 'clientes', () =>
        import('./features/clientes/pages/clientes-page/clientes-page.component').then(
          (m) => m.ClientesPageComponent,
        ),
      ),
      moduleRoute('proveedores', 'Proveedores', 'proveedores', () =>
        import('./features/proveedores/pages/proveedores-page/proveedores-page.component').then(
          (m) => m.ProveedoresPageComponent,
        ),
      ),
      moduleRoute('woocommerce', 'WooCommerce', 'woocommerce', () =>
        import('./features/woocommerce/pages/woocommerce-page/woocommerce-page.component').then(
          (m) => m.WooCommercePageComponent,
        ),
      ),
      moduleRoute('reportes', 'Reportes', 'reportes', () =>
        import('./features/reportes/pages/reportes-page/reportes-page.component').then(
          (m) => m.ReportesPageComponent,
        ),
      ),
      moduleRoute('ecosistema', 'Ecosistema', 'ecosistema', () =>
        import('./features/ecosistema/pages/ecosistema-page/ecosistema-page.component').then(
          (m) => m.EcosistemaPageComponent,
        ),
      ),
    ],
  },
  {
    path: '**',
    redirectTo: 'app/dashboard',
  },
];
