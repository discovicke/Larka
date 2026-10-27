// @types/node finns inte i e2e (det enda beroendet är @playwright/test), så vi deklarerar
// bara den del av process vi använder. Playwrights egna typer kräver att libCheck är avstängd.
declare const process: {
  env: Record<string, string | undefined>
}
