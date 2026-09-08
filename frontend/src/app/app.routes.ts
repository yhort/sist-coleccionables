import { Routes } from '@angular/router';

import { authGuard, guestGuard } from './core/auth/auth.guards';
import { ShellComponent } from './layout/shell/shell.component';

function moduleRoute(
  path: string,
  title: string,
  loadComponent: NonNullable<Routes[number]['loadComponent']>,
): Routes[number] {
  return {
    path,
    title: `${title} | Trunqi TCG`,
    data: {
      pageTitle: title,
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
      moduleRoute('dashboard', 'Dashboard', () =>
        import('./features/dashboard/pages/dashboard-page/dashboard-page.component').then(
          (m) => m.DashboardPageComponent,
        ),
      ),
      moduleRoute('productos-tcg', 'Productos TCG', () =>
        import(
          './features/productos-tcg/pages/productos-tcg-page/productos-tcg-page.component'
        ).then((m) => m.ProductosTcgPageComponent),
      ),
      {
        path: 'inventario/kardex',
        title: 'Kardex | Trunqi TCG',
        data: {
          pageTitle: 'Kardex',
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
      moduleRoute('inventario', 'Inventario', () =>
        import('./features/inventario/pages/inventario-page/inventario-page.component').then(
          (m) => m.InventarioPageComponent,
        ),
      ),
      moduleRoute('aperturas-tcg', 'Aperturas TCG', () =>
        import(
          './features/aperturas-tcg/pages/aperturas-tcg-page/aperturas-tcg-page.component'
        ).then((m) => m.AperturasTcgPageComponent),
      ),
      {
        path: 'subastas-tcg/:id',
        title: 'Pujas | Trunqi TCG',
        data: {
          pageTitle: 'Detalle de subasta',
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
      moduleRoute('subastas-tcg', 'Subastas TCG', () =>
        import('./features/subastas-tcg/pages/subastas-tcg-page/subastas-tcg-page.component').then(
          (m) => m.SubastasTcgPageComponent,
        ),
      ),
      moduleRoute('pedidos-digitales', 'Pedidos Digitales', () =>
        import(
          './features/pedidos-digitales/pages/bandeja-pedidos-digitales-page/bandeja-pedidos-digitales-page.component'
        ).then((m) => m.BandejaPedidosDigitalesPageComponent),
      ),
      moduleRoute('pagos', 'Pagos', () =>
        import('./features/pagos/pages/pagos-bandeja-page/pagos-bandeja-page.component').then(
          (m) => m.PagosBandejaPageComponent,
        ),
      ),
      moduleRoute('caja', 'Caja', () =>
        import('./features/caja/pages/caja-page/caja-page.component').then(
          (m) => m.CajaPageComponent,
        ),
      ),
      moduleRoute('entregas', 'Entregas', () =>
        import('./features/entregas/pages/entregas-page/entregas-page.component').then(
          (m) => m.EntregasPageComponent,
        ),
      ),
      moduleRoute('clientes', 'Clientes', () =>
        import('./features/clientes/pages/clientes-page/clientes-page.component').then(
          (m) => m.ClientesPageComponent,
        ),
      ),
      moduleRoute('proveedores', 'Proveedores', () =>
        import('./features/proveedores/pages/proveedores-page/proveedores-page.component').then(
          (m) => m.ProveedoresPageComponent,
        ),
      ),
      moduleRoute('woocommerce', 'WooCommerce', () =>
        import('./features/woocommerce/pages/woocommerce-page/woocommerce-page.component').then(
          (m) => m.WooCommercePageComponent,
        ),
      ),
      moduleRoute('reportes', 'Reportes', () =>
        import('./features/reportes/pages/reportes-page/reportes-page.component').then(
          (m) => m.ReportesPageComponent,
        ),
      ),
      moduleRoute('ecosistema', 'Ecosistema', () =>
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
