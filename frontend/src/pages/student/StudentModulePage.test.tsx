// @vitest-environment jsdom
import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { studyApi } from '../../services/studyApi'
import type { StudentModule } from '../../types/learning'
import { formatReviewDate, StudentModulePage } from './StudentModulePage'

vi.mock('../../services/studyApi', () => ({ studyApi: {
  getModule: vi.fn(), startSession: vi.fn(), startFreePractice: vi.fn(), resetModuleProgress: vi.fn(),
} }))

const module: StudentModule = {
  id: 'm1', title: 'Fundamentos de Redes', description: 'Introdução', subject: 'Redes', activeSessionId: null,
  activeConcepts: 10, masteredConcepts: 3, learningConcepts: 4, notStartedConcepts: 3,
  pendingReviews: 2, progressPercent: 30, nextReviewAtUtc: null, activeSessionMode: null,
  concepts: [{ id: 'c1', name: 'Endereço IP' }, { id: 'c2', name: 'DNS' }],
}
const props = { moduleId: 'm1', onNavigate: vi.fn(), onLogout: vi.fn(), onUnauthorized: vi.fn() }
const api = vi.mocked(studyApi)

beforeEach(() => { vi.clearAllMocks(); api.getModule.mockResolvedValue(module) })
afterEach(() => cleanup())

describe('student module progress and practice controls', () => {
  it('shows backend progress aggregates and offers whole-module or concept practice', async () => {
    const user = userEvent.setup()
    render(<StudentModulePage {...props} />)
    expect(await screen.findByText('30% dominado')).toBeTruthy()
    expect(screen.getByText('3 de 10 conceitos dominados')).toBeTruthy()
    expect(screen.getByText('4 em aprendizagem')).toBeTruthy()
    expect(screen.getByText('3 ainda não iniciados')).toBeTruthy()
    expect(screen.getByText('2 revisões pendentes')).toBeTruthy()
    expect(screen.getByRole('progressbar').getAttribute('aria-valuenow')).toBe('30')
    expect(screen.queryByText(/EXPOSURE|RECOGNITION|GUIDED_RECALL|FREE_RECALL|MASTERED|Difficulty|Stability|FsrsRating/)).toBeNull()

    await user.click(screen.getByRole('button', { name: 'Escolher conceito' }))
    await user.selectOptions(screen.getByLabelText('Conceito para praticar'), 'c2')
    api.startFreePractice.mockResolvedValue({ sessionId: 's1', moduleId: 'm1', mode: 'FREE_PRACTICE', status: 'ACTIVE', totalActivities: 1, completedActivities: 0, activity: null })
    await user.click(screen.getByRole('button', { name: 'Praticar conceito' }))
    await waitFor(() => expect(api.startFreePractice).toHaveBeenCalledWith('m1', 'c2'))
    expect(props.onNavigate).toHaveBeenCalledWith('/student/sessions/s1')
  })

  it('resumes an active session and exposes an accessible reset confirmation', async () => {
    const user = userEvent.setup()
    api.getModule.mockResolvedValue({ ...module, activeSessionId: 's9', activeSessionMode: 'NORMAL' })
    render(<StudentModulePage {...props} />)
    await user.click(await screen.findByRole('button', { name: 'Continuar sessão' }))
    expect(props.onNavigate).toHaveBeenCalledWith('/student/sessions/s9')
    await user.click(screen.getByRole('button', { name: 'Reiniciar módulo' }))
    const dialog = screen.getByRole('alertdialog', { name: 'Reiniciar módulo?' })
    expect(dialog).toBeTruthy()
    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Cancelar' }))
    await user.tab({ shift: true })
    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Reiniciar módulo' }))
    await user.tab()
    expect(document.activeElement).toBe(within(dialog).getByRole('button', { name: 'Cancelar' }))
    await user.click(screen.getByRole('button', { name: 'Cancelar' }))
    expect(api.resetModuleProgress).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Reiniciar módulo' }))
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('alertdialog')).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Reiniciar módulo' }))
    api.resetModuleProgress.mockResolvedValue(undefined)
    api.getModule.mockResolvedValue({ ...module, progressPercent: 0, masteredConcepts: 0, learningConcepts: 0, notStartedConcepts: 10, pendingReviews: 0 })
    await user.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Reiniciar módulo' }))
    await waitFor(() => expect(api.resetModuleProgress).toHaveBeenCalledWith('m1'))
    expect(await screen.findByText('0% dominado')).toBeTruthy()
  })

  it('starts free practice for the complete module and resumes an active free session', async () => {
    const user = userEvent.setup()
    api.startFreePractice.mockResolvedValue({ sessionId: 'free-session', moduleId: 'm1', mode: 'FREE_PRACTICE', status: 'ACTIVE', totalActivities: 1, completedActivities: 0, activity: null })
    const { unmount } = render(<StudentModulePage {...props} />)
    await user.click(await screen.findByRole('button', { name: 'Praticar módulo inteiro' }))
    expect(api.startFreePractice).toHaveBeenCalledWith('m1', null)
    unmount()

    api.getModule.mockResolvedValue({ ...module, activeSessionId: 'free-session', activeSessionMode: 'FREE_PRACTICE' })
    render(<StudentModulePage {...props} />)
    expect((await screen.findAllByRole('button', { name: 'Continuar prática livre' })).length).toBe(1)
  })

  it('formats today, tomorrow, and later review dates in Portuguese', () => {
    const now = new Date(2026, 9, 1, 12)
    expect(formatReviewDate(new Date(2026, 9, 1, 18, 40).toISOString(), now)).toContain('Hoje')
    expect(formatReviewDate(new Date(2026, 9, 2, 0, 5).toISOString(), now)).toBe('Amanhã')
    expect(formatReviewDate(new Date(2026, 9, 3, 12).toISOString(), now)).not.toBe('Amanhã')
  })
})
