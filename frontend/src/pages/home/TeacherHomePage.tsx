import { useEffect, useState } from 'react'
import type { AuthenticatedUser } from '../../types/auth'
import { teacherProgressApi, type TeacherDashboard } from '../../services/teacherProgressApi'
import { moduleApi } from '../../services/moduleApi'
import { classroomApi, type ClassroomSummary } from '../../services/classroomApi'
import type { ModuleSummary } from '../../types/modules'
import styles from './TeacherHomePage.module.css'

export function TeacherHomePage({ user, onNavigate }: { user: AuthenticatedUser; onNavigate: (path: string) => void }) {
  const [summary, setSummary] = useState<TeacherDashboard | null>(null)
  const [modules, setModules] = useState<ModuleSummary[]>([])
  const [classrooms, setClassrooms] = useState<ClassroomSummary[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  useEffect(() => { let active = true; Promise.all([teacherProgressApi.dashboard(), moduleApi.list(), classroomApi.list()]).then(([s, m, c]) => { if (active) { setSummary(s); setModules(m.sort((a,b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc))); setClassrooms(c.filter(x => x.status === 'Active').sort((a,b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc))) } }).catch(() => { if (active) setError('Não foi possível carregar a página inicial.') }).finally(() => { if (active) setLoading(false) }); return () => { active = false } }, [])
  const published = modules.some(m => m.status === 'Published' || m.status === 1)
  const steps = [{ done: modules.length > 0, label: 'Criar seu primeiro módulo', path: '/teacher/modules' }, { done: published, label: 'Publicar um módulo', path: '/teacher/modules' }, { done: classrooms.length > 0, label: 'Criar sua primeira turma', path: '/teacher/classrooms' }, { done: (summary?.activeStudents ?? 0) > 0, label: 'Cadastrar alunos', path: '/teacher/classrooms' }]
  return <main className={styles.page}><header className={styles.intro}><p className={styles.eyebrow}>Página inicial</p><h1>Olá, {user.name?.trim().split(/\s+/)[0] || 'professor'}</h1><p>Organize seus conteúdos e acompanhe suas turmas.</p><div><button onClick={() => onNavigate('/teacher/modules')}>Novo módulo</button><button className={styles.secondary} onClick={() => onNavigate('/teacher/classrooms')}>Nova turma</button></div></header>
    {loading && <p role="status">Carregando…</p>}{error && <p role="alert">{error}</p>}{summary && <section className={styles.metrics} aria-label="Resumo"><button onClick={() => onNavigate('/teacher/modules')}><span>Módulos</span><strong>{summary.modules}</strong></button><button onClick={() => onNavigate('/teacher/classrooms')}><span>Turmas ativas</span><strong>{summary.activeClassrooms}</strong></button><button onClick={() => onNavigate('/teacher/dashboard')}><span>Alunos ativos</span><strong>{summary.activeStudents}</strong></button></section>}
    <div className={styles.columns}><section className={styles.card}><header><h2>Módulos recentes</h2><button onClick={() => onNavigate('/teacher/modules')}>Ver todos</button></header>{modules.slice(0,4).map(m => <button className={styles.item} key={m.id} onClick={() => onNavigate('/teacher/modules')}><strong>{m.title}</strong><span>{m.status === 'Published' || m.status === 1 ? 'Publicado' : m.status === 'Archived' || m.status === 2 ? 'Arquivado' : 'Rascunho'} · {m.activeConceptCount} conceitos ativos</span></button>)}{modules.length === 0 && <p>Nenhum módulo cadastrado.</p>}</section>
      <section className={styles.card}><header><h2>Turmas ativas</h2><button onClick={() => onNavigate('/teacher/classrooms')}>Ver todas</button></header>{classrooms.slice(0,4).map(c => <button className={styles.item} key={c.id} onClick={() => onNavigate(`/teacher/dashboard?classroom=${encodeURIComponent(c.id)}`)}><strong>{c.name}</strong><span>{c.studentCount} alunos · {c.moduleCount} módulos</span></button>)}{classrooms.length === 0 && <p>Nenhuma turma ativa.</p>}</section></div>
    {steps.some(s => !s.done) && <section className={styles.card}><h2>Primeiros passos</h2><ul>{steps.map(step => <li key={step.label} data-done={step.done}><span aria-hidden="true">{step.done ? '✓' : '○'}</span>{step.done ? <span>{step.label}</span> : <button onClick={() => onNavigate(step.path)}>{step.label}</button>}</li>)}</ul></section>}
  </main>
}
