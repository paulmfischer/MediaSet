import js from "@eslint/js";
import tsPlugin from "@typescript-eslint/eslint-plugin";
import importPlugin from "eslint-plugin-import";
import jsxA11y from "eslint-plugin-jsx-a11y";
import react from "eslint-plugin-react";
import reactHooks from "eslint-plugin-react-hooks";
import prettier from "eslint-config-prettier";
import globals from "globals";

export default [
  // Global ignores (replaces --ignore-path .gitignore)
  {
    ignores: [".cache/", "build/", "coverage/"],
  },

  // Base config
  {
    ...js.configs.recommended,
    languageOptions: {
      ecmaVersion: "latest",
      sourceType: "module",
      globals: {
        ...globals.browser,
        ...globals.commonjs,
        ...globals.es2015,
      },
    },
  },

  // React
  {
    files: ["**/*.{js,jsx,ts,tsx}"],
    ...react.configs.flat.recommended,
    settings: {
      react: {
        version: "detect",
      },
      formComponents: ["Form"],
      linkComponents: [
        { name: "Link", linkAttribute: "to" },
        { name: "NavLink", linkAttribute: "to" },
      ],
    },
  },

  // React JSX Runtime (disables rules not needed with new JSX transform)
  {
    files: ["**/*.{js,jsx,ts,tsx}"],
    ...react.configs.flat["jsx-runtime"],
  },

  // React Hooks
  {
    files: ["**/*.{js,jsx,ts,tsx}"],
    ...reactHooks.configs.flat.recommended,
  },

  // Accessibility
  {
    files: ["**/*.{js,jsx,ts,tsx}"],
    ...jsxA11y.flatConfigs.recommended,
  },

  // TypeScript (flat/recommended is an array of configs)
  ...tsPlugin.configs["flat/recommended"].map((config) => ({
    ...config,
    files: config.files ?? ["**/*.{ts,tsx}"],
  })),

  // Import plugin for TypeScript files
  {
    files: ["**/*.{ts,tsx}"],
    plugins: {
      import: importPlugin,
    },
    settings: {
      "import/internal-regex": "^~/",
      "import/resolver": {
        node: {
          extensions: [".ts", ".tsx"],
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
