import {describe, expect, it} from 'vitest'

import {ApiError} from '@/lib/api'
import {useAsync} from './useAsync'

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return {promise, resolve, reject}
}

describe('useAsync', () => {
  it('är inte igång från början och har inget fel', () => {
    const {loading, error} = useAsync(async () => 1)

    expect(loading.value).toBe(false)
    expect(error.value).toBe('')
  })

  it('visar loading medan uppgiften pågår och returnerar resultatet', async () => {
    const pending = deferred<string>()
    const {run, loading} = useAsync(() => pending.promise)

    const result = run()
    expect(loading.value).toBe(true)

    pending.resolve('klart')
    expect(await result).toBe('klart')
    expect(loading.value).toBe(false)
  })

  it('skickar vidare argumenten till uppgiften', async () => {
    const {run} = useAsync(async (id: string, count: number) => `${id}:${count}`)

    expect(await run('a', 2)).toBe('a:2')
  })

  it('sätter felet som text och returnerar undefined', async () => {
    const {run, loading, error} = useAsync(async () => {
      throw new ApiError(400, 'title is required')
    })

    expect(await run()).toBeUndefined()
    expect(error.value).toBe('Titeln saknas.')
    expect(loading.value).toBe(false)
  })

  it('använder failureText i stället för undantagets text', async () => {
    const {run, error} = useAsync(async () => {
      throw new ApiError(500, 'internt')
    }, 'Kunde inte hämta delen.')

    await run()

    expect(error.value).toBe('Kunde inte hämta delen.')
  })

  it('rensar det gamla felet när uppgiften startas igen', async () => {
    let fail = true
    const {run, error} = useAsync(async () => {
      if (fail) {
        throw new Error('fel')
      }
      return 'ok'
    })

    await run()
    expect(error.value).toBe('fel')

    fail = false
    const second = run()
    expect(error.value).toBe('')
    expect(await second).toBe('ok')
  })

  it('låter en nyare körning gälla i stället för en äldre som blir klar senare', async () => {
    const first = deferred<string>()
    const second = deferred<string>()
    const calls = [first, second]
    const {run, loading, error} = useAsync(() => calls.shift()!.promise)

    const older = run()
    const newer = run()

    second.resolve('ny')
    expect(await newer).toBe('ny')
    expect(loading.value).toBe(false)

    first.resolve('gammal')
    expect(await older).toBeUndefined()
    expect(error.value).toBe('')
    expect(loading.value).toBe(false)
  })

  it('ignorerar felet från en äldre körning och behåller loading för den nyare', async () => {
    const first = deferred<string>()
    const second = deferred<string>()
    const calls = [first, second]
    const {run, loading, error} = useAsync(() => calls.shift()!.promise)

    const older = run()
    const newer = run()

    first.reject(new Error('gammalt fel'))
    expect(await older).toBeUndefined()
    expect(error.value).toBe('')
    expect(loading.value).toBe(true)

    second.resolve('ny')
    expect(await newer).toBe('ny')
    expect(loading.value).toBe(false)
  })
})
