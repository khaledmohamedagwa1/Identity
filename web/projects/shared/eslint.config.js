// @ts-check
const { defineConfig } = require('eslint/config');
const rootConfig = require('../../eslint.config.js');

module.exports = defineConfig([
  ...rootConfig,
  {
    files: ['**/*.ts'],
    rules: {
      // `appCan` keeps the directive name the spec uses (section 7.8).
      '@angular-eslint/directive-selector': [
        'error',
        { type: 'attribute', prefix: ['ip', 'app'], style: 'camelCase' },
      ],
      '@angular-eslint/component-selector': [
        'error',
        { type: 'element', prefix: 'ip', style: 'kebab-case' },
      ],
    },
  },
]);
