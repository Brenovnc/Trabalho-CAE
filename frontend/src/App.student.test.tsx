// @vitest-environment jsdom
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'

beforeEach(() => window.history.replaceState({}, '', '/'))
afterEach(() => { cleanup(); vi.unstubAllGlobals() })
const teacher = { id: 't1', role: 'TEACHER', name: 'Teacher', email: 'teacher@test', classroomId: null, enrollmentNumber: null }
const student = { id: 's1', role: 'STUDENT', name: 'Student', email: null, classroomId: 'c1', enrollmentNumber: '001' }

function stubIdentity(identity: object | null) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL) => {
    const url = String(input)
    if (url.endsWith('/api/auth/csrf')) return new Response(JSON.stringify({ token: 'csrf' }), { status: 200 })
    if (url.endsWith('/api/auth/me')) return identity
      ? new Response(JSON.stringify(identity), { status: 200 })
      : new Response(JSON.stringify({ status: 401, code: 'unauthenticated', message: 'Sign in', errors: {} }), { status: 401 })
    if (url.endsWith('/api/student/modules')) return new Response(JSON.stringify([]), { status: 200 })
    if (url.endsWith('/api/modules')) return new Response(JSON.stringify([]), { status: 200 })
    return new Response(JSON.stringify({}), { status: 200 })
  })
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

describe('role-based application navigation', () => {
  it('routes a student to the student modules area only', async () => {
    stubIdentity(student)
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Escolha um módulo' })).toBeTruthy()
    expect(await screen.findByRole('heading', { name: 'Nenhum módulo disponível' })).toBeTruthy()
    expect(screen.queryByText(/Gestão de módulos/)).toBeNull()
    expect(screen.queryByRole('navigation', { name: 'Navegação principal' })).toBeNull()
  })

  it('keeps a teacher in the existing teacher area and out of student navigation', async () => {
    stubIdentity(teacher)
    render(<App />)
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Escolha um módulo' })).toBeNull())
    expect(await screen.findByRole('heading', { name: /Aqui está sua sala/ })).toBeTruthy()
    expect(screen.getByRole('navigation', { name: 'Navegação principal' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Dashboard' }).getAttribute('aria-current')).toBe('page')
    expect(screen.getByRole('button', { name: 'Módulos' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Turmas' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Conta' })).toBeTruthy()
  })

  it('returns an unauthenticated visitor to an existing login form', async () => {
    stubIdentity(null)
    render(<App />)
    expect(await screen.findByRole('heading', { name: 'Plataforma Educacional' })).toBeTruthy()
    expect(screen.getByRole('heading', { name: 'Entrar como professor' })).toBeTruthy()
  })
})

