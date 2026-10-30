// @vitest-environment jsdom
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { classroomApi } from '../../services/classroomApi'
import { TeacherDashboardPage } from './TeacherDashboardPage'

vi.mock('../../services/classroomApi', () => ({ classroomApi: { list: vi.fn(), get: vi.fn() } }))
afterEach(() => { cleanup(); vi.resetAllMocks() })

describe('TeacherDashboardPage', () => {
  it('shows loading and the empty state when there are no active classrooms', async () => {
    vi.mocked(classroomApi.list).mockResolvedValueOnce([])
    render(<TeacherDashboardPage onNavigate={vi.fn()} onStudent={vi.fn()} />)
    expect(screen.getByRole('status').textContent).toContain('Carregando')
    expect(await screen.findByText('Você ainda não tem turmas ativas.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Criar turma' })).toBeTruthy()
  })
  it('shows an API error when classrooms cannot be loaded', async () => {
    vi.mocked(classroomApi.list).mockRejectedValueOnce(new Error('offline'))
    render(<TeacherDashboardPage onNavigate={vi.fn()} onStudent={vi.fn()} />)
    expect(await screen.findByRole('alert')).toBeTruthy()
  })
})
