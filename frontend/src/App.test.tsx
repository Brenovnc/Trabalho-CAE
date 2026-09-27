import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthForm } from './pages/auth/AuthForm'
import { ModulesPage } from './pages/modules/ModulesPage'
import { getCurrentUser, prepareCsrfToken, submitAuth } from './services/authApi'
import { moduleApi } from './services/moduleApi'

afterEach(() => vi.unstubAllGlobals())

describe('authentication interface and API client', () => {
  it('renders accessible choices and the teacher login form', () => {
    const markup = renderToStaticMarkup(createElement(AuthForm, { onSubmit: async () => {} }))

    expect(markup).toContain('Professor: entrar')
    expect(markup).toContain('Aluno: primeiro acesso')
    expect(markup).toContain('E-mail')
    expect(markup).toContain('Senha')
  })

  it('uses credentials and sends the in-memory CSRF token for mutations', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'csrf-value' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        id: 'teacher-id', role: 'TEACHER', name: 'Teacher', email: 'teacher@example.test',
        classroomId: null, enrollmentNumber: null,
      }), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'authenticated-csrf' }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)

    await prepareCsrfToken()
    const user = await submitAuth('teacher-register', {
      name: 'Teacher', email: 'teacher@example.test', password: 'secure-password',
      confirmPassword: 'secure-password',
    })

    expect(user.role).toBe('TEACHER')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ credentials: 'include' })
    const mutation = fetchMock.mock.calls[1][1] as RequestInit
    expect(mutation.credentials).toBe('include')
    expect(new Headers(mutation.headers).get('X-CSRF-TOKEN')).toBe('csrf-value')
    expect(fetchMock).toHaveBeenCalledTimes(3)
    expect(fetchMock.mock.calls[2][0]).toContain('/api/auth/csrf')
  })

  it('turns API errors into useful messages', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValueOnce(new Response(
      JSON.stringify({ status: 401, code: 'invalid_credentials', message: 'Credenciais inválidas.', errors: {} }),
      { status: 401 },
    )))

    await expect(getCurrentUser()).rejects.toMatchObject({
      status: 401,
      code: 'invalid_credentials',
      message: 'Credenciais inválidas.',
    })
  })

  it('renders the basic module management screen', () => {
    const markup = renderToStaticMarkup(createElement(ModulesPage, { onLogout: () => {} }))
    expect(markup).toContain('Gestão de módulos')
    expect(markup).toContain('Novo módulo')
    expect(markup).toContain('Meus módulos')
  })

  it('sends cookies and CSRF when publishing and shows publication errors', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'module-csrf' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({
        isValid: false, errors: [{ conceptId: 'concept', code: 'missing_fill_blank_activities', message: 'O conceito precisa de mais atividades de lacunas.' }],
      }), { status: 422 }))
    vi.stubGlobal('fetch', fetchMock)

    await expect(moduleApi.publish('module-id')).rejects.toThrow('atividades de lacunas')
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ credentials: 'include' })
    const mutation = fetchMock.mock.calls[1][1] as RequestInit
    expect(mutation.credentials).toBe('include')
    expect(new Headers(mutation.headers).get('X-CSRF-TOKEN')).toBe('module-csrf')
  })
})
