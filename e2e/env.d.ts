// @types/node finns inte i e2e (det enda beroendet är @playwright/test), så vi deklarerar
// bara den del av process vi använder. Playwrights egna typer kräver att libCheck är avstängd.
declare const process: {
  env: Record<string, string | undefined>
}

// Filerna för tokenet i auth.ts. Typerna deklareras här av samma skäl som process.
declare module 'node:fs/promises' {
  export function mkdir(path: URL, options: {recursive: boolean}): Promise<string | undefined>
  export function readFile(path: URL, encoding: 'utf8'): Promise<string>
  export function writeFile(path: URL, data: string): Promise<void>
}
