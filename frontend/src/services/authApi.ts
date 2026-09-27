import type { ApiErrorBody, AuthenticatedUser, AuthMode } from '../types/auth'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080'
let csrfToken: string | null = null

export class ApiRequestError extends Error {
  constructor(readonly status: number, readonly code: string, message: string) {
    super(message)
    this.name = 'ApiRequestError'
  }
}

async function parseResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = (await response.json()) as ApiErrorBody
    throw new ApiRequestError(response.status, error.code, error.message)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

export async function prepareCsrfToken(): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/auth/csrf`, { credentials: 'include' })
  const result = await parseResponse<{ token: string }>(response)
  csrfToken = result.token
}

async function post<T>(path: string, body?: unknown): Promise<T> {
  if (!csrfToken) await prepareCsrfToken()

  const headers = new Headers({ 'X-CSRF-TOKEN': csrfToken! })
  const request: RequestInit = { method: 'POST', credentials: 'include', headers }
  if (body !== undefined) {
    headers.set('Content-Type', 'application/json')
    request.body = JSON.stringify(body)
  }

  return parseResponse<T>(await fetch(`${apiBaseUrl}${path}`, request))
}

export async function requestApi<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const headers = new Headers()
  const init: RequestInit = { method, credentials: 'include', headers }
  if (method !== 'GET' && method !== 'HEAD') {
    if (!csrfToken) await prepareCsrfToken()
    headers.set('X-CSRF-TOKEN', csrfToken!)
  }
  if (body !== undefined) {
    headers.set('Content-Type', 'application/json')
    init.body = JSON.stringify(body)
  }
  return parseResponse<T>(await fetch(`${apiBaseUrl}${path}`, init))
}
export function getCurrentUser(): Promise<AuthenticatedUser> {
  return fetch(`${apiBaseUrl}/api/auth/me`, { credentials: 'include' })
    .then(response => parseResponse<AuthenticatedUser>(response))
}

export async function submitAuth(mode: AuthMode, fields: Record<string, string>): Promise<AuthenticatedUser> {
  let operation: Promise<AuthenticatedUser>
  switch (mode) {
    case 'teacher-register':
      operation = post('/api/auth/teachers/register', {
        name: fields.name,
        email: fields.email,
        password: fields.password,
        confirmPassword: fields.confirmPassword,
      })
      break
    case 'teacher-login':
      operation = post('/api/auth/teachers/login', {
        email: fields.email,
        password: fields.password,
      })
      break
    case 'student-activate':
      operation = post('/api/auth/students/activate', {
        classroomCode: fields.classroomCode,
        enrollmentNumber: fields.enrollmentNumber,
        temporaryCode: fields.temporaryCode,
        password: fields.password,
        confirmPassword: fields.confirmPassword,
      })
      break
    case 'student-login':
      operation = post('/api/auth/students/login', {
        classroomCode: fields.classroomCode,
        enrollmentNumber: fields.enrollmentNumber,
        password: fields.password,
      })
      break
  }

  const user = await operation
  await prepareCsrfToken()
  return user
}
export async function logout(): Promise<{ message: string }> {
  const result = await post<{ message: string }>('/api/auth/logout')
  await prepareCsrfToken()
  return result
}
