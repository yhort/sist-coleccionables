# Trunqi TCG — CapitalPOS Web

Frontend Angular (standalone) del POS TCG de Trunqi. Tema **Clean Administrative**.

## Arranque

Levanta `CapitalPos.Tcg.Api` en `http://localhost:5249` y luego:

```bash
cd frontend
npm start
```

La app queda en `http://localhost:4200`. `ng serve` hace proxy de `/api` hacia el API. El login usa JWT + `X-CapitalPos-EmpresaId` (Development: `admin@trunqi.local`, ver `BACKEND_ROADMAP.md`).

Productos, inventario, Kanban, CPE, aperturas, subastas y WooCommerce/webhooks leen y escriben en PostgreSQL a través de `CapitalPos.Tcg.Api`. Pagos, entregas, reportes y dashboard siguen en prototipo in-memory.

## Layout

- Sidebar colapsable con los 11 módulos TCG
- Topbar con buscador de módulos, usuario de sesión y cierre
- `router-outlet` dentro del shell autenticado

Tokens: fondo `#F9FAFB`, superficie `#FFFFFF`, borde `#E5E7EB`, acento `#2563EB`.
