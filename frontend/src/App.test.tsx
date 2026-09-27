import { createElement } from 'react'
import { renderToStaticMarkup } from 'react-dom/server'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AuthForm } from './pages/auth/AuthForm'
import { getCurrentUser, prepareCsrfToken, submitAuth } from './services/authApi'

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
})
