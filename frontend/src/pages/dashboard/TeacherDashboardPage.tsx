import { useEffect, useState } from 'react'
import { teacherProgressApi } from '../../services/teacherProgressApi'
import styles from './TeacherDashboardPage.module.css'

type Dashboard = { modules: number; activeClassrooms: number; activeStudents: number }
type Props = { onNavigate: (path: string) => void }

export function TeacherDashboardPage({ onNavigate }: Props) {
  const [data, setData] = useState<Dashboard | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  useEffect(() => {
    let active = true
    teacherProgressApi.dashboard()
      .then(value => { if (active) setData(value) })
      .catch(() => { if (active) setError('Não foi possível carregar o dashboard.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])
  return <main className={styles.page}>
    <div className={styles.intro}><p className={styles.eyebrow}>Visão geral</p><h1>Bom trabalho. Aqui está sua sala.</h1><p>Acompanhe seus módulos e turmas em um só lugar.</p></div>
    {loading && <p className={styles.state} role='status'>Carregando resumo…</p>}
    {error && <p className={styles.error} role='alert'>{error} <button type='button' onClick={() => window.location.reload()}>Tentar novamente</button></p>}
    {data && <section className={styles.metrics} aria-label='Resumo da conta'>
      <article><span className={styles.metricIcon} aria-hidden='true'>▤</span><p>Módulos não arquivados</p><strong>{data.modules}</strong><button type='button' onClick={() => onNavigate('/teacher/modules')}>Gerenciar módulos <span aria-hidden='true'>→</span></button></article>
      <article><span className={styles.metricIcon} aria-hidden='true'>▣</span><p>Turmas ativas</p><strong>{data.activeClassrooms}</strong><button type='button' onClick={() => onNavigate('/teacher/classrooms')}>Ver turmas <span aria-hidden='true'>→</span></button></article>
      <article><span className={styles.metricIcon} aria-hidden='true'>♙</span><p>Alunos ativos</p><strong>{data.activeStudents}</strong><button type='button' onClick={() => onNavigate('/teacher/classrooms')}>Acompanhar progresso <span aria-hidden='true'>→</span></button></article>
    </section>}
    <section className={styles.next}><div><p className={styles.eyebrow}>Próximos passos</p><h2>Continue de onde parou</h2><p>Abra uma turma para consultar o progresso dos alunos ou prepare conteúdo para suas aulas.</p></div><div className={styles.actions}><button type='button' onClick={() => onNavigate('/teacher/classrooms')}>Abrir turmas</button><button className={styles.secondary} type='button' onClick={() => onNavigate('/teacher/modules')}>Ver módulos</button></div></section>
  </main>
}
