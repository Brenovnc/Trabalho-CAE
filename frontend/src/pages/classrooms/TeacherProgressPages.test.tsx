// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { teacherProgressApi } from '../../services/teacherProgressApi'
import { StudentProgressPage } from './StudentProgressPage'
import { formatLastAccess } from '../../utils/formatLastAccess'

vi.mock('../../services/teacherProgressApi', () => ({ teacherProgressApi: { dashboard: vi.fn(), classroom: vi.fn(), student: vi.fn(), studentStatistics: vi.fn() } }))
afterEach(() => { cleanup(); vi.resetAllMocks() })

describe('StudentProgressPage', () => {
  it('shows module aggregates without exposing raw learning states', async () => {
    vi.mocked(teacherProgressApi.student).mockResolvedValueOnce({
      studentId: 's1', enrollmentNumber: '001', name: 'Ana', isActive: true,
      modules: [{ moduleId: 'm1', title: 'Redes', subject: 'Computação', activeConcepts: 10, masteredConcepts: 5, learningConcepts: 3, notStartedConcepts: 2, pendingReviews: 2, progressPercent: 50 }],
    })
    vi.mocked(teacherProgressApi.studentStatistics).mockResolvedValueOnce({ distribution: { mastered: 5, learning: 3, notStarted: 2 }, daily: [], activityTypes: [], mostErrors: [] })
    render(<StudentProgressPage classroomId="c1" studentId="s1" onBack={vi.fn()} />)
    expect(await screen.findByText('Ana')).toBeTruthy()
    expect(screen.getByText('Matrícula')).toBeTruthy()
    expect(screen.getByText('Em aprendizagem')).toBeTruthy()
    expect(screen.getByText('Não iniciados')).toBeTruthy()
    expect(screen.getByRole('progressbar').getAttribute('aria-valuenow')).toBe('50')
    expect(screen.queryByText(/GUIDED_RECALL/)).toBeNull()
  })
})

describe('formatLastAccess', () => {
  it('formats missing and older access dates', () => {
    expect(formatLastAccess(null)).toBe('Nunca')
    expect(formatLastAccess('2026-09-22T12:00:00Z')).toMatch(/22\/09\/2026/)
  })
})
