import {globalIgnores} from 'eslint/config'
import {defineConfigWithVueTs, vueTsConfigs} from '@vue/eslint-config-typescript'
import stylistic from '@stylistic/eslint-plugin'
import pluginVue from 'eslint-plugin-vue'

export default defineConfigWithVueTs(
  {
    name: 'app/files-to-lint',
    files: ['**/*.{js,mjs,ts,mts,tsx,vue}'],
  },

  // api-types.ts genereras av npm run gen:api.
  globalIgnores(['**/dist/**', '**/dist-ssr/**', '**/coverage/**', 'src/lib/api-types.ts']),

  // strongly-recommended innehåller layoutreglerna för templates (indrag, attribut, self-closing ...).
  pluginVue.configs['flat/strongly-recommended'],
  vueTsConfigs.recommended,

  // Formatering sköts av ESLint Stylistic. `npm run format` fixar det mesta automatiskt.
  stylistic.configs.customize({
    indent: 2,
    quotes: 'single',
    semi: false,
    braceStyle: '1tbs',
    commaDangle: 'always-multiline',
    arrowParens: true,
  }),

  {
    name: 'app/style',
    rules: {
      // {computed}, inte { computed }
      '@stylistic/object-curly-spacing': ['error', 'never'],
      'vue/object-curly-spacing': ['error', 'never'],

      // ? och : alltid på egna rader
      '@stylistic/multiline-ternary': ['error', 'always'],
      'vue/multiline-ternary': ['error', 'always'],

      // <Button/> och <input/>, inte <Button /> eller <input>
      'vue/html-closing-bracket-spacing': ['error', {selfClosingTag: 'never'}],
      'vue/html-self-closing': ['error', {html: {void: 'always', normal: 'always', component: 'always'}}],

      // Inga enbokstavsnamn, utom i i loopar, T för typparametrar och _ för det man inte använder.
      'id-length': ['error', {min: 2, exceptions: ['i', 'T', '_'], properties: 'never'}],

      'vue/max-attributes-per-line': ['error', {singleline: {max: 3}, multiline: {max: 1}}],
      'vue/singleline-html-element-content-newline': 'off',
    },
  },

  // shadcn-vue genererar komponenter med ettordsnamn (Button, Card ...) och valfria props utan default.
  {
    name: 'app/shadcn-ui',
    files: ['src/components/ui/**/*.vue'],
    rules: {
      'vue/multi-word-component-names': 'off',
      'vue/require-default-prop': 'off',
    },
  },
)
