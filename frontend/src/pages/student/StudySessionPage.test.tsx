// @vitest-environment jsdom
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiRequestError } from '../../services/authApi'
import type { StudyActivity, StudySession } from '../../types/learning'
import { StudySessionPage } from './StudySessionPage'

const studyMocks = vi.hoisted(() => ({ getSession: vi.fn(), reveal: vi.fn(), submit: vi.fn(), abandon: vi.fn() }))
vi.mock('../../services/studyApi', () => ({ studyApi: studyMocks }))

const baseActivity: StudyActivity = {
  presentationId: 'p1', conceptId: 'c1', conceptName: 'DNS', startedAtUtc: '2026-09-27T12:00:00Z',
  type: 'TRUE_FALSE', payload: { statement: 'DNS resolves names.' },
}
const activeSession: StudySession = {
  sessionId: 's1', status: 'ACTIVE', totalActivities: 1, completedActivities: 0,
  startedAtUtc: '2026-09-27T12:00:00Z', completedAtUtc: null, activity: baseActivity,
}
const props = { sessionId: 's1', onNavigate: vi.fn(), onLogout: vi.fn(), onUnauthorized: vi.fn() }

beforeEach(() => { vi.clearAllMocks() })
afterEach(() => { cleanup(); vi.unstubAllGlobals() })

describe('student session page', () => {
  it('loads the API activity, shows backend feedback, and waits for Continue before rendering next', async () => {
    const user = userEvent.setup()
    studyMocks.getSession.mockResolvedValue(activeSession)
    studyMocks.submit.mockResolvedValue({
      wasCorrect: true, feedback: 'Correct answer from the API.', learningState: 'GUIDED_RECALL',
      completedActivities: 1, totalActivities: 2, sessionStatus: 'ACTIVE',
      nextActivity: { ...baseActivity, presentationId: 'p2', type: 'FILL_BLANK', payload: { text: 'O {{1}}.', slotNumbers: [1], options: ['DNS'] } },
      correctAnswer: null,
    })
    render(<StudySessionPage {...props} />)
    expect(await screen.findByText('DNS resolves names.')).toBeTruthy()
    expect(screen.getByLabelText(/Tempo nesta atividade/)).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Verdadeiro' }))
    expect(await screen.findByText('Correct answer from the API.')).toBeTruthy()
    expect(screen.queryByText('Complete as lacunas')).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Continuar' }))
    expect(await screen.findByText('Complete as lacunas')).toBeTruthy()
    expect(studyMocks.submit).toHaveBeenCalledOnce()
  })

  it('uses the active activity returned by GET again after reload and shows completed summary', async () => {
    studyMocks.getSession.mockResolvedValueOnce(activeSession).mockResolvedValueOnce(activeSession)
    const first = render(<StudySessionPage {...props} />)
    expect(await screen.findByText('DNS resolves names.')).toBeTruthy()
    first.unmount()
    render(<StudySessionPage {...props} />)
    expect(await screen.findByText('DNS resolves names.')).toBeTruthy()
    expect(studyMocks.getSession).toHaveBeenCalledTimes(2)
  })

  it('recovers a 409 by reloading server state without showing incorrect feedback', async () => {
    const user = userEvent.setup()
    studyMocks.getSession.mockResolvedValue(activeSession)
    studyMocks.submit.mockRejectedValue(new ApiRequestError(409, 'stale_activity', 'stale'))
    render(<StudySessionPage {...props} />)
    await screen.findByText('DNS resolves names.')
    await user.click(screen.getByRole('button', { name: 'Verdadeiro' }))
    await waitFor(() => expect(studyMocks.getSession).toHaveBeenCalledTimes(2))
    expect(screen.getByRole('status').textContent).toContain('atualizada')
    expect(screen.queryByText('Incorreto')).toBeNull()
    expect(studyMocks.getSession).toHaveBeenCalledTimes(2)
  })

  it('abandons only after confirmation and returns to the student module list', async () => {
    const user = userEvent.setup()
    vi.stubGlobal('confirm', vi.fn(() => true))
    studyMocks.getSession.mockResolvedValue(activeSession)
    studyMocks.abandon.mockResolvedValue(undefined)
    render(<StudySessionPage {...props} />)
    await screen.findByText('DNS resolves names.')
    await user.click(screen.getByRole('button', { name: 'Abandonar sessão' }))
    await waitFor(() => expect(studyMocks.abandon).toHaveBeenCalledWith('s1'))
    expect(props.onNavigate).toHaveBeenCalledWith('/student')
    vi.unstubAllGlobals()
  })

  it('shows a short session summary only after Continue acknowledges completion', async () => {
    const user = userEvent.setup()
    studyMocks.getSession.mockResolvedValue(activeSession)
    studyMocks.submit.mockResolvedValue({
      wasCorrect: false, feedback: 'Incorrect.', learningState: 'RECOGNITION', completedActivities: 1,
      totalActivities: 1, sessionStatus: 'COMPLETED', nextActivity: null,
      correctAnswer: { choice: true, explanation: 'DNS resolves names.' },
    })
    render(<StudySessionPage {...props} />)
    await screen.findByText('DNS resolves names.')
    await user.click(screen.getByRole('button', { name: 'Verdadeiro' }))
    expect(await screen.findByText('Resposta esperada')).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Ver resumo' }))
    expect(await screen.findByText('Sessão concluída')).toBeTruthy()
    expect(screen.getByText('1 atividade realizada.')).toBeTruthy()
  })
})


