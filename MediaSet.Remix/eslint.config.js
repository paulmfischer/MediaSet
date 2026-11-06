import js from '@eslint/js';
import tsPlugin from '@typescript-eslint/eslint-plugin';
import importPlugin from 'eslint-plugin-import';
import jsxA11y from 'eslint-plugin-jsx-a11y';
import prettier from 'eslint-config-prettier';
import globals from 'globals';

export default [
  // Global ignores (replaces --ignore-path .gitignore)
  {
    ignores: ['.cache/', '.coverage/', 'build/', 'coverage/', 'public/'],
  },

  // Base config
  {
    ...js.configs.recommended,
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      globals: {
        ...globals.browser,
        ...globals.node,
      },
    },
  },

  // Accessibility (Remix components use JSX with HTML attribute names)
  {
    files: ['**/*.{js,jsx,ts,tsx}'],
    ...jsxA11y.flatConfigs.recommended,
  },

  // TypeScript (flat/recommended is an array of configs)
  ...tsPlugin.configs['flat/recommended'].map((config) => ({
    ...config,
    files: config.files ?? ['**/*.{ts,tsx}'],
  })),

  // Import plugin for TypeScript files
  {
    files: ['**/*.{ts,tsx}'],
    plugins: {
      import: importPlugin,
    },
    settings: {
      'import/resolver': {
        node: {
          extensions: ['.ts', '.tsx'],
        },
        typescript: {
          alwaysTryTypes: true,
        },
      },
    },
    rules: {
      ...importPlugin.flatConfigs.recommended.rules,
      ...importPlugin.flatConfigs.typescript.rules,
    },
  },

  // Prettier (must be last to override formatting rules)
  prettier,
];
