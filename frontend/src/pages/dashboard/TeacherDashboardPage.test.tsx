// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { teacherProgressApi } from '../../services/teacherProgressApi'
import { TeacherDashboardPage } from './TeacherDashboardPage'

vi.mock('../../services/teacherProgressApi', () => ({ teacherProgressApi: { dashboard: vi.fn() } }))
afterEach(() => { cleanup(); vi.resetAllMocks() })

describe('TeacherDashboardPage', () => {
  it('shows loading state and real summary values with navigation actions', async () => {
    let resolve!: (value: { modules: number; activeClassrooms: number; activeStudents: number }) => void
    vi.mocked(teacherProgressApi.dashboard).mockReturnValueOnce(new Promise(res => { resolve = res }))
    const onNavigate = vi.fn()
    render(<TeacherDashboardPage onNavigate={onNavigate} />)
    expect(screen.getByRole('status').textContent).toContain('Carregando')
    resolve({ modules: 4, activeClassrooms: 3, activeStudents: 86 })
    expect(await screen.findByText('86')).toBeTruthy()
    expect(screen.getByText('4')).toBeTruthy()
    expect(screen.getByText('3')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /Gerenciar módulos/ }))
    expect(onNavigate).toHaveBeenCalledWith('/teacher/modules')
  })

  it('shows an API error without fabricating values', async () => {
    vi.mocked(teacherProgressApi.dashboard).mockRejectedValueOnce(new Error('offline'))
    render(<TeacherDashboardPage onNavigate={vi.fn()} />)
    expect(await screen.findByRole('alert')).toBeTruthy()
    expect(screen.queryByText('0')).toBeNull()
  })
})
