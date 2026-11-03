import {defineConfig, mergeConfig} from 'vitest/config'
import viteConfig from './vite.config'

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: 'jsdom',
      setupFiles: ['src/test/setup.ts'],
      unstubGlobals: true,
      include: ['src/**/*.{test,spec}.{ts,tsx}'],
      coverage: {
        provider: 'v8',
        reporter: ['text', 'lcov', 'cobertura', 'html'],
        reportsDirectory: 'coverage',
        include: ['src/**/*.{ts,vue}'],
        exclude: [
          'src/main.ts',
          'src/vite-env.d.ts',
          'src/lib/api-types.ts',
          'src/lib/utils.ts',
          'src/components/ui/**',
        ],
      },
    },
  }),
)
