import bash from 'highlight.js/lib/languages/bash'
import csharp from 'highlight.js/lib/languages/csharp'
import css from 'highlight.js/lib/languages/css'
import dockerfile from 'highlight.js/lib/languages/dockerfile'
import javascript from 'highlight.js/lib/languages/javascript'
import json from 'highlight.js/lib/languages/json'
import sql from 'highlight.js/lib/languages/sql'
import typescript from 'highlight.js/lib/languages/typescript'
import xml from 'highlight.js/lib/languages/xml'
import yaml from 'highlight.js/lib/languages/yaml'
import {createLowlight} from 'lowlight'

// Bara språken som kurserna använder, så att inte alla highlight.js-grammatiker hamnar i bundlen.
export const lowlight = createLowlight({bash, csharp, css, dockerfile, javascript, json, sql, typescript, xml, yaml})

lowlight.registerAlias({
  bash: ['sh', 'shell', 'zsh', 'console'],
  csharp: ['cs', 'c#'],
  dockerfile: ['docker'],
  javascript: ['js', 'jsx', 'mjs'],
  typescript: ['ts', 'tsx', 'vue'],
  xml: ['html', 'svg', 'cshtml', 'razor'],
  yaml: ['yml'],
})

// Namnet som visas i kodblockets rubrik. Okända språk visas som de skrevs.
const labels: Record<string, string> = {
  'bash': 'Bash',
  'sh': 'Shell',
  'shell': 'Shell',
  'zsh': 'Shell',
  'console': 'Terminal',
  'csharp': 'C#',
  'cs': 'C#',
  'c#': 'C#',
  'css': 'CSS',
  'dockerfile': 'Dockerfile',
  'docker': 'Dockerfile',
  'javascript': 'JavaScript',
  'js': 'JavaScript',
  'jsx': 'JSX',
  'mjs': 'JavaScript',
  'json': 'JSON',
  'sql': 'SQL',
  'typescript': 'TypeScript',
  'ts': 'TypeScript',
  'tsx': 'TSX',
  'vue': 'Vue',
  'xml': 'XML',
  'html': 'HTML',
  'svg': 'SVG',
  'cshtml': 'Razor',
  'razor': 'Razor',
  'yaml': 'YAML',
  'yml': 'YAML',
}

export function languageLabel(language: string | null | undefined): string {
  if (!language) {
    return 'Kod'
  }
  return labels[language.toLowerCase()] ?? language
}
