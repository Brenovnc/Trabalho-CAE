// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { teacherProgressApi } from '../../services/teacherProgressApi'
import { ClassroomProgressPage, formatLastAccess } from './ClassroomProgressPage'
import { StudentProgressPage } from './StudentProgressPage'

vi.mock('../../services/teacherProgressApi', () => ({
  teacherProgressApi: { dashboard: vi.fn(), classroom: vi.fn(), student: vi.fn() },
}))
afterEach(() => { cleanup(); vi.resetAllMocks() })

describe('teacher progress pages', () => {
  it('renders all required classroom columns, missing values, and opens student details', async () => {
    vi.mocked(teacherProgressApi.classroom).mockResolvedValueOnce({
      classroomId: 'c1', classroomName: 'Redes', totalActiveConcepts: 10,
      students: [{
        studentId: 's1', enrollmentNumber: '001', name: null, isActive: true,
        masteredConcepts: 0, learningConcepts: 0, notStartedConcepts: 10,
        pendingReviews: 0, progressPercent: 0, lastAccessAtUtc: null,
      }],
    })
    const onStudent = vi.fn()
    render(<ClassroomProgressPage classroomId='c1' onBack={vi.fn()} onStudent={onStudent} />)
    expect(await screen.findByText('001')).toBeTruthy()
    for (const heading of ['Matrícula', 'Nome', 'Progresso', 'Revisões pendentes', 'Último acesso']) expect(screen.getByText(heading)).toBeTruthy()
    expect(screen.getByText('—')).toBeTruthy()
    expect(screen.getByText('Nunca')).toBeTruthy()
    expect(screen.getByText('0%')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Detalhes' }))
    expect(onStudent).toHaveBeenCalledWith('s1')
  })

  it('renders a useful empty state and loading state', async () => {
    let resolve!: (value: { classroomId: string; classroomName: string; totalActiveConcepts: number; students: [] }) => void
    vi.mocked(teacherProgressApi.classroom).mockReturnValueOnce(new Promise(res => { resolve = res }))
    const { rerender } = render(<ClassroomProgressPage classroomId='c1' onBack={vi.fn()} onStudent={vi.fn()} />)
    expect(screen.getByRole('status').textContent).toContain('Carregando')
    resolve({ classroomId: 'c1', classroomName: 'Vazia', totalActiveConcepts: 0, students: [] })
    expect(await screen.findByText('Nenhum aluno cadastrado nesta turma.')).toBeTruthy()
    vi.mocked(teacherProgressApi.classroom).mockRejectedValueOnce(new Error('offline'))
    rerender(<ClassroomProgressPage classroomId='c2' onBack={vi.fn()} onStudent={vi.fn()} />)
    expect(await screen.findByRole('alert')).toBeTruthy()
  })

  it('shows module aggregates and progress details without exposing raw states', async () => {
    vi.mocked(teacherProgressApi.student).mockResolvedValueOnce({
      studentId: 's1', enrollmentNumber: '001', name: 'Ana', isActive: true,
      modules: [{
        moduleId: 'm1', title: 'Redes', subject: 'Computação', activeConcepts: 10,
        masteredConcepts: 5, learningConcepts: 3, notStartedConcepts: 2, pendingReviews: 2, progressPercent: 50,
      }],
    })
    render(<StudentProgressPage classroomId='c1' studentId='s1' onBack={vi.fn()} />)
    expect(await screen.findByText('Ana')).toBeTruthy()
    expect(screen.getByText('Matrícula')).toBeTruthy()
    expect(screen.getByText('Em aprendizagem')).toBeTruthy()
    expect(screen.getByText('Não iniciados')).toBeTruthy()
    expect(screen.getByText('Revisões pendentes')).toBeTruthy()
    expect(screen.getByRole('progressbar').getAttribute('aria-valuenow')).toBe('50')
    expect(screen.queryByText(/GUIDED_RECALL/)).toBeNull()
  })
})

describe('formatLastAccess', () => {
  it('labels a missing access and formats older UTC dates in Portuguese', () => {
    expect(formatLastAccess(null)).toBe('Nunca')
    expect(formatLastAccess('2026-09-22T12:00:00Z')).toMatch(/22\/09\/2026/)
  })
})
