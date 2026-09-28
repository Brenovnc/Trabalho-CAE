import { describe, expect, it, vi } from 'vitest'

describe('study API', () => {
  it('uses the cookie and CSRF-aware API client for submitted answers', async () => {
    vi.resetModules()
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'student-csrf' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ wasCorrect: true }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
    const { studyApi } = await import('./studyApi')
    await studyApi.submit('session-1', 'presentation-1', 'TRUE_FALSE', { answer: { choice: true } })
    expect(fetchMock.mock.calls[0][1]).toMatchObject({ credentials: 'include' })
    const request = fetchMock.mock.calls[1][1] as RequestInit
    expect(request.credentials).toBe('include')
    expect(new Headers(request.headers).get('X-CSRF-TOKEN')).toBe('student-csrf')
    expect(JSON.parse(String(request.body))).toMatchObject({ presentationId: 'presentation-1', type: 'TRUE_FALSE', answer: { choice: true } })
  })
})
