import type { ConceptInput, ModuleDetails, ModuleInput, ModuleSummary, PublicationValidation } from '../types/modules'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080'

async function read<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { message?: string; errors?: { message: string }[] } | null
    const detail = body?.message ?? body?.errors?.map(item => item.message).join(' ') ?? 'A solicitação não foi concluída.'
    throw new Error(detail)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

async function send<T>(path: string, method: string, body?: unknown): Promise<T> {
  const csrf = await read<{ token: string }>(await fetch(`${apiBaseUrl}/api/auth/csrf`, { credentials: 'include' }))
  const headers = new Headers({ 'X-CSRF-TOKEN': csrf.token })
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
}
