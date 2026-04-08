import type { Config } from 'tailwindcss';
import plugin from 'tailwindcss/plugin';
import colors from 'tailwindcss/colors';

function hexToRgbChannels(hex: string): string {
  const r = parseInt(hex.slice(1, 3), 16);
  const g = parseInt(hex.slice(3, 5), 16);
  const b = parseInt(hex.slice(5, 7), 16);
  return `${r} ${g} ${b}`;
}

const ENTITY_COLORS = {
  books: colors.green[400],
  movies: colors.red[400],
  games: colors.purple[400],
  musics: colors.pink[400],
};

export default {
  content: ['./app/**/{**,.client,.server}/**/*.{js,jsx,ts,tsx}'],
  theme: {
    extend: {
      colors: {
        entity: 'rgb(var(--entity-color) / <alpha-value>)',
      },
    },
  },
  plugins: [
    plugin(function ({ addBase }) {
      addBase(
        Object.fromEntries(
          Object.entries(ENTITY_COLORS).map(([name, hex]) => [
            `.entity-${name}`,
            { '--entity-color': hexToRgbChannels(hex) },
          ])
        )
      );
    }),
  ],
} satisfies Config;
