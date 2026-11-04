import type { CommunityMineItem, CommunityPageResult } from '../types/community'
import type { ModuleSummary, ModuleDetails } from '../types/modules'
const base = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080'
async function read<T>(response: Response): Promise<T> { if (!response.ok) { const body = await response.json().catch(() => null); throw new Error(body?.message ?? 'Não foi possível concluir a operação.') } return response.status === 204 ? undefined as T : response.json() as Promise<T> }
async function send<T>(path: string, method: string, body?: unknown): Promise<T> { const csrf = await read<{ token: string }>(await fetch(`${base}/api/auth/csrf`, { credentials: 'include' })); return read<T>(await fetch(`${base}${path}`, { method, credentials: 'include', headers: { 'X-CSRF-TOKEN': csrf.token, ...(body ? { 'Content-Type': 'application/json' } : {}) }, body: body ? JSON.stringify(body) : undefined })) }
export const communityApi = {
  mine: () => fetch(`${base}/api/community/modules/mine`, { credentials: 'include' }).then(read<CommunityMineItem[]>),
  search: (search: string, subject: string, page: number) => fetch(`${base}/api/community/modules?search=${encodeURIComponent(search)}&subject=${encodeURIComponent(subject)}&page=${page}&pageSize=24`, { credentials: 'include' }).then(read<CommunityPageResult>),
  download: (id: string) => send<ModuleDetails>(`/api/community/modules/${id}/download`, 'POST'),
  share: (moduleId: string) => send<CommunityMineItem>('/api/community/modules', 'POST', { moduleId }),
  remove: (id: string) => send<void>(`/api/community/modules/${id}`, 'DELETE'),
}
export type { ModuleSummary }
