export interface Option {
  value: string
  label: string
}

/** Det som visas under ett formulär efter att man tryckt på knappen. */
export type FormResult
  = | {kind: 'response', status: number, body: unknown}
    | {kind: 'error', status?: number, message: string}
