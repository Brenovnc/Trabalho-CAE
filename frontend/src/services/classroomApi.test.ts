import { afterEach, describe, expect, it, vi } from 'vitest'
import { classroomApi } from './classroomApi'

afterEach(() => vi.unstubAllGlobals())

describe('classroom API', () => {
  it('sends cookies and CSRF and returns the temporary code from the creation response', async () => {
    const created = { studentId: 'student-1', enrollmentNumber: '12345', name: null, temporaryAccessCode: 'A2BCDE', expiresAtUtc: '2026-09-30T00:00:00Z' }
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ token: 'class-csrf' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify(created), { status: 201 }))
    vi.stubGlobal('fetch', fetchMock)

    const response = await classroomApi.createStudent('class-1', '12345', '')

    expect(response.temporaryAccessCode).toBe('A2BCDE')
    const request = fetchMock.mock.calls[1][1] as RequestInit
    expect(request.credentials).toBe('include')
    expect(new Headers(request.headers).get('X-CSRF-TOKEN')).toBe('class-csrf')
    expect(request.body).toBe(JSON.stringify({ enrollmentNumber: '12345', name: null }))
  })

  it('surfaces duplicate enrollment conflicts from the API', async () => {
    const fetchMock = vi.fn((input: RequestInfo | URL) => Promise.resolve(
      String(input).includes('/api/auth/csrf')
        ? new Response(JSON.stringify({ token: 'class-csrf' }), { status: 200 })
        : new Response(JSON.stringify({ status: 409, code: 'enrollment_already_exists', message: 'Esta matrícula já existe na turma.', errors: {} }), { status: 409 }),
    ))
    vi.stubGlobal('fetch', fetchMock)

    await expect(classroomApi.createStudent('class-1', '12345', '')).rejects.toThrow('Esta matrícula já existe')
  })
})

describe('student CSV import', () => {
  it('sends a multipart file with cookies and CSRF without setting JSON content type', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ totalRows: 1, validRows: 1, invalidRows: 0, rows: [] }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
    const csvFile = new File(['matricula' + String.fromCharCode(10) + '12345'], 'students.csv', { type: 'text/csv' })

    await classroomApi.previewCsv('class-1', csvFile)

    const call = fetchMock.mock.calls.find(([url]) => String(url).includes('/import/preview'))!
    const request = call[1] as RequestInit
    expect(request.credentials).toBe('include')
    expect(new Headers(request.headers).get('X-CSRF-TOKEN')).toBeTruthy()
    expect(new Headers(request.headers).get('Content-Type')).toBeNull()
    expect(request.body).toBeInstanceOf(FormData)
  })
})
