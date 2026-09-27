import type { ConceptInput, ModuleDetails, ModuleInput, ModuleSummary, PublicationValidation } from '../types/modules'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080'
type ApiErrorBody = { message?: string; errors?: Record<string, string[]> | { message: string }[] }

async function read<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.json().catch(() => null) as ApiErrorBody | null
    const details = Array.isArray(body?.errors)
      ? body.errors.map(item => item.message)
      : Object.values(body?.errors ?? {}).flat()
    throw new Error([body?.message, ...details].filter(Boolean).join(' '))
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

async function csrfHeaders() {
  const csrf = await read<{ token: string }>(await fetch(`${apiBaseUrl}/api/auth/csrf`, { credentials: 'include' }))
  return new Headers({ 'X-CSRF-TOKEN': csrf.token })
}

async function send<T>(path: string, method: string, body?: unknown): Promise<T> {
  const headers = await csrfHeaders()
  const init: RequestInit = { method, credentials: 'include', headers }
  if (body !== undefined) {
    headers.set('Content-Type', 'application/json')
    init.body = JSON.stringify(body)
  }
  return read<T>(await fetch(`${apiBaseUrl}${path}`, init))
}

export const moduleApi = {
  list: () => fetch(`${apiBaseUrl}/api/modules`, { credentials: 'include' }).then(read<ModuleSummary[]>),
  get: (id: string) => fetch(`${apiBaseUrl}/api/modules/${id}`, { credentials: 'include' }).then(read<ModuleDetails>),
  create: (input: ModuleInput) => send<ModuleDetails>('/api/modules', 'POST', input),
  update: (id: string, input: ModuleInput) => send<ModuleDetails>(`/api/modules/${id}`, 'PUT', input),
  duplicate: (id: string) => send<ModuleDetails>(`/api/modules/${id}/duplicate`, 'POST'),
  archive: (id: string) => send<void>(`/api/modules/${id}/archive`, 'POST'),
  validate: (id: string) => fetch(`${apiBaseUrl}/api/modules/${id}/publication-validation`, { credentials: 'include' }).then(read<PublicationValidation>),
  publish: (id: string) => send<ModuleDetails>(`/api/modules/${id}/publish`, 'POST'),
  createConcept: (moduleId: string, input: ConceptInput) => send<ModuleDetails['concepts'][number]>(`/api/modules/${moduleId}/concepts`, 'POST', input),
  updateConcept: (moduleId: string, conceptId: string, input: ConceptInput) => send<ModuleDetails['concepts'][number]>(`/api/modules/${moduleId}/concepts/${conceptId}`, 'PUT', input),
  duplicateConcept: (moduleId: string, conceptId: string) => send<ModuleDetails['concepts'][number]>(`/api/modules/${moduleId}/concepts/${conceptId}/duplicate`, 'POST'),
  deactivateConcept: (moduleId: string, conceptId: string) => send<void>(`/api/modules/${moduleId}/concepts/${conceptId}/deactivate`, 'POST'),
  importJson: async (json: string) => {
    const headers = await csrfHeaders()
    headers.set('Content-Type', 'application/json')
    return read<ModuleDetails>(await fetch(`${apiBaseUrl}/api/modules/import`, {
      method: 'POST', credentials: 'include', headers, body: json,
    }))
  },
  exportJson: async (id: string) => {
    const response = await fetch(`${apiBaseUrl}/api/modules/${id}/export`, { credentials: 'include' })
    if (!response.ok) return read<never>(response)
    const disposition = response.headers.get('Content-Disposition') ?? ''
    const fileName = disposition.match(/filename="?([^";]+)"?/i)?.[1] ?? `module-${id}.json`
    return { blob: await response.blob(), fileName }
  },
}
