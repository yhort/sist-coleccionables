import { RarezaTcg, TipoCartaTcg } from '../../productos-tcg/models/producto-tcg.model';
import { ImportarSetTcgRequest, UpsertTcgCartaRequest } from '../models/catalogo-tcg.model';

const NOMBRES_ME03: readonly string[] = [
  'Spinarak',
  'Ariados',
  'Shaymin',
  'Snivy',
  'Servine',
  'Serperior',
  'Scatterbug',
  'Spewpa',
  'Vivillon',
  'Rowlet',
  'Dartrix',
  'Decidueye ex',
  'Fletchinder',
  'Talonflame',
  'Salandit',
  'Salazzle ex',
  'Turtonator',
  'Seel',
  'Dewgong',
  'Staryu',
  'Mega Starmie ex',
  'Lapras ex',
  'Amaura',
  'Aurorus',
  'Volcanion',
  'Shinx',
  'Luxio',
  'Luxray',
  'Dedenne',
  'Clefairy',
  'Mega Clefable ex',
  'Mawile',
  'Espurr',
  'Meowstic',
  'Spritzee',
  'Aromatisse',
  'Nosepass',
  'Probopass',
  'Hippopotas',
  'Hippowdon',
  'Landorus',
  'Binacle',
  'Barbaracle',
  'Tyrunt',
  'Tyrantrum',
  'Hawlucha',
  'Mega Zygarde ex',
  'Gastly',
  'Haunter',
  'Gengar',
  'Skorupi',
  'Drapion',
  'Yveltal ex',
  'Chien-Pao',
  'Mega Skarmory ex',
  'Honedge',
  'Doublade',
  'Aegislash',
  'Klefki',
  'Rattata',
  'Raticate',
  'Meowth ex',
  'Snorlax',
  'Bunnelby',
  'Diggersby',
  'Fletchling',
  'Furfrou',
  'Antique Jaw Fossil',
  'Antique Sail Fossil',
  'Core Memory',
  'Crushing Hammer',
  'Energy Search',
  'Energy Swatter',
  'Hole-Digging Shovel',
  'Jacinthe',
  'Judge',
  'Lumiose City',
  'Lumiose Galette',
  'Naveen',
  'Poké Ball',
  'Poké Pad',
  'Pokémon Catcher',
  'Potion',
  "Rosa's Encouragement",
  'Tarragon',
  'Growing Energy',
  'Rocky Energy',
  'Telepathic Energy',
  'Spewpa',
  'Rowlet',
  'Talonflame',
  'Aurorus',
  'Dedenne',
  'Clefairy',
  'Espurr',
  'Probopass',
  'Drapion',
  'Doublade',
  'Raticate',
  'Decidueye ex',
  'Salazzle ex',
  'Mega Starmie ex',
  'Mega Clefable ex',
  'Mega Zygarde ex',
  'Yveltal ex',
  'Mega Skarmory ex',
  'Meowth ex',
  'Energy Recycler',
  'Forest of Vitality',
  'Jacinthe',
  'Lumiose City',
  'Naveen',
  'Poké Pad',
  "Rosa's Encouragement",
  'Sacred Ash',
  'Tarragon',
  'Wondrous Patch',
  'Mega Starmie ex',
  'Mega Clefable ex',
  'Mega Zygarde ex',
  'Meowth ex',
  'Jacinthe',
  "Rosa's Encouragement",
  'Mega Zygarde ex',
];

function numeroMe03(indice: number): string {
  return String(indice + 1).padStart(3, '0');
}

function tipoMe03(n: number): TipoCartaTcg {
  if (n >= 86 && n <= 88) {
    return 'ENERGIA';
  }
  if ((n >= 68 && n <= 85) || (n >= 108 && n <= 117) || n === 122 || n === 123) {
    return 'ENTRENADOR';
  }
  return 'POKEMON';
}

function rarezaMe03(n: number, nombre: string): RarezaTcg {
  if (n === 124) {
    return 'MEGA_HIPER_RARA';
  }
  if (n >= 118) {
    return 'ILUSTRACION_ESPECIAL';
  }
  if (n >= 108) {
    return 'ILUSTRACION_RARA';
  }
  if (n >= 100) {
    return nombre.startsWith('Mega') ? 'ULTRA' : 'DOBLE_RARA';
  }
  if (n >= 89) {
    return 'ILUSTRACION_RARA';
  }
  if (nombre.startsWith('Mega') && nombre.includes('ex')) {
    return 'ULTRA';
  }
  if (nombre.includes(' ex') || nombre.endsWith('ex')) {
    return 'DOBLE_RARA';
  }
  return 'COMUN';
}

function cartasMe03(): UpsertTcgCartaRequest[] {
  if (NOMBRES_ME03.length !== 124) {
    throw new Error(`ME03 debe tener 124 fichas; hay ${NOMBRES_ME03.length}.`);
  }
  return NOMBRES_ME03.map((nombre, indice) => {
    const n = indice + 1;
    return {
      numero: numeroMe03(indice),
      nombre,
      tipoCarta: tipoMe03(n),
      rareza: rarezaMe03(n, nombre),
    };
  });
}

export const IMPORT_ME03_EQUILIBRIO_PERFECTO: ImportarSetTcgRequest = {
  serie: {
    juego: 'Pokémon',
    codigo: 'MEGA',
    nombre: 'Megaevolución',
    activa: true,
  },
  set: {
    codigo: 'ME03',
    nombre: 'Equilibrio Perfecto',
    nombreEn: 'Perfect Order',
    codigoImpresion: 'P11218',
    totalCartas: 124,
  },
  cartas: cartasMe03(),
};
