import {localAuth} from './localAuth'
import type {AuthProvider} from './types'

// Den enda platsen som väljer implementation. Microsoft-inloggning (#80) byts in här.
export const auth: AuthProvider = localAuth

export {AuthError} from './types'
