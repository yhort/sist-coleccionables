# Activar tenant RCP Subastas en local

## Credenciales seed (Development)

| Tenant | EmpresaId | Email | Password | Sede |
|--------|-----------|-------|----------|------|
| Trunqi (default) | `c0a1e001-0000-4000-8000-000000000001` | `admin@trunqi.local` | `Admin123!` | Sede principal |
| **RCP Subastas** | `c0a1e001-0000-4000-8000-000000000002` | `admin@rcp.local` | `Admin123!` | Jr. Carabaya #1150 |

El seed corre al arrancar la API en `Development` (`DevelopmentDataSeeder`).

## Feature toggles

| Toggle | Trunqi | RCP |
|--------|--------|-----|
| `PermitirSinglesTcg` | true | false |
| `PermitirWooCommerceSync` | true | false |
| `PermitirPosMostrador` | false | **true** |
| `PermitirSubastasLoteFB` | false | **true** |
| `PermitirCromosSinCodigo` | false | **true** |

## Cómo entrar

1. Arrancar API + frontend (`proxy.conf.json` → API local).
2. Login con `admin@rcp.local` / `Admin123!` (seleccionar empresa RCP si el login lo pide).
3. Menú esperado: POS Mostrador, Carga lote FB, productos con flujo sin EAN. Sin WooCommerce / Singles TCG.
4. Abrir caja en sede **Jr. Carabaya #1150** antes de cobrar en POS.

## CPE en local

En `appsettings.Development.json`, `CpeApi:Modo` = `Local` (firma UBL en proceso, sin `localhost:5096`).  
Para OSE/real: `Modo` = `Http` y CPE API levantada.

## Smoke E2E Paola (automatizado)

```bash
dotnet test backend/CapitalPos.Tcg.Api.Tests/CapitalPos.Tcg.Api.Tests.csproj \
  --filter "FullyQualifiedName~PaolaFlujoE2ETests|FullyQualifiedName~PosCobrarEmitirE2ETests|FullyQualifiedName~LoteMasivoSubastaE2ETests|FullyQualifiedName~TenantFeaturesE2ETests|FullyQualifiedName~SkuInternoProductoE2ETests"
```
