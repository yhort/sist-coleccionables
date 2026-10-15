export type TenantFeatureName =
  | 'PermitirSinglesTcg'
  | 'PermitirWooCommerceSync'
  | 'PermitirPosMostrador'
  | 'PermitirSubastasLoteFB'
  | 'PermitirCromosSinCodigo';

export interface TenantSettings {
  tenantId: string;
  nombreComercial: string;
  permitirSinglesTcg: boolean;
  permitirWooCommerceSync: boolean;
  permitirPosMostrador: boolean;
  permitirSubastasLoteFB: boolean;
  permitirCromosSinCodigo: boolean;
}

export const TENANT_SETTINGS_TRUNQI_DEFAULT: TenantSettings = {
  tenantId: '',
  nombreComercial: 'TRUNQI',
  permitirSinglesTcg: true,
  permitirWooCommerceSync: true,
  permitirPosMostrador: false,
  permitirSubastasLoteFB: false,
  permitirCromosSinCodigo: false,
};

export function featureEnabled(
  settings: TenantSettings | null | undefined,
  feature: TenantFeatureName,
): boolean {
  if (!settings) {
    // Sesión antigua sin settings: preservar menú Trunqi; ocultar features RCP-only.
    return feature === 'PermitirSinglesTcg' || feature === 'PermitirWooCommerceSync';
  }
  switch (feature) {
    case 'PermitirSinglesTcg':
      return settings.permitirSinglesTcg;
    case 'PermitirWooCommerceSync':
      return settings.permitirWooCommerceSync;
    case 'PermitirPosMostrador':
      return settings.permitirPosMostrador;
    case 'PermitirSubastasLoteFB':
      return settings.permitirSubastasLoteFB;
    case 'PermitirCromosSinCodigo':
      return settings.permitirCromosSinCodigo;
    default:
      return false;
  }
}
