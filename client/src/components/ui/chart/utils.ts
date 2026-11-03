import type {ChartConfig} from '.'
import {isClient} from '@vueuse/core'
import {useId} from 'reka-ui'
import {type Component, h, render} from 'vue'

// Simple cache using a Map to store serialized object keys
const cache = new Map<string, string>()

// Convert object to a consistent string key
function serializeKey(key: unknown): string {
  return JSON.stringify(key, key && typeof key === 'object'
    ? Object.keys(key).sort()
    : undefined)
}

export function componentToString(config: ChartConfig, component: Component, props?: Record<string, unknown>) {
  if (!isClient)
    return

  // This function will be called once during mount lifecycle
  const id = useId()

  // https://unovis.dev/docs/auxiliary/Crosshair#component-props
  return (rawData: unknown, position: number | Date) => {
    const data = rawData && typeof rawData === 'object' && 'data' in rawData
      ? (rawData as {data: unknown}).data
      : rawData
    const serializedKey = `${id}-${serializeKey(data)}`
    const cachedContent = cache.get(serializedKey)
    if (cachedContent)
      return cachedContent

    const vnode = h(component, {...props, payload: data, config, x: position})
    const div = document.createElement('div')
    render(vnode, div)
    cache.set(serializedKey, div.innerHTML)
    return div.innerHTML
  }
}
