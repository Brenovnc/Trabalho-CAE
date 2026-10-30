import { useEffect, useState } from 'react'
import { teacherProgressApi } from '../../services/teacherProgressApi'
import styles from './TeacherDashboardPage.module.css'
import { BarChart, LineChart, StackedBarChart } from '../../components/charts/Charts'
import type { TeacherDashboardStats } from '../../services/teacherProgressApi'
import { StatisticsFilters, defaultStatisticsFilters, statisticsFilterSummary, statisticsFiltersReady, type FilterOption } from '../../components/charts/StatisticsFilters'
import type { StatisticsFilters as FilterValues } from '../../services/teacherProgressApi'
import { classroomApi } from '../../services/classroomApi'
import { moduleApi } from '../../services/moduleApi'
import type { ModuleSummary } from '../../types/modules'

type Dashboard = { modules: number; activeClassrooms: number; activeStudents: number }
type Props = { onNavigate: (path: string) => void }

export function TeacherDashboardPage({ onNavigate }: Props) {
  const [data, setData] = useState<Dashboard | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [stats, setStats] = useState<TeacherDashboardStats | null>(null)
  const [filters, setFilters] = useState<FilterValues>(defaultStatisticsFilters)
  const [statsLoading, setStatsLoading] = useState(false)
  const [statsError, setStatsError] = useState('')
  const [retry, setRetry] = useState(0)
  const [classroomOptions, setClassroomOptions] = useState<FilterOption[]>([])
  const [moduleOptions, setModuleOptions] = useState<FilterOption[]>([])
  useEffect(() => {
    let active = true
    teacherProgressApi.dashboard()
      .then(value => { if (active) setData(value) })
      .catch(() => { if (active) setError('Não foi possível carregar o dashboard.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [])
  useEffect(() => { let active = true; Promise.all([classroomApi.list(), moduleApi.list()]).then(([classrooms, modules]) => { if (active) { setClassroomOptions(classrooms.filter(item => item.status === 'Active' || item.status === '1').map(item => ({ id: item.id, label: item.name }))); setModuleOptions(modules.filter(item => item.status === 1 || item.status === 'Published').map((item: ModuleSummary) => ({ id: item.id, label: item.title }))) } }).catch(() => {}); return () => { active = false } }, [])
  useEffect(() => { if (!statisticsFiltersReady(filters)) return; let active = true; setStatsLoading(true); setStatsError(''); teacherProgressApi.dashboardStatistics(filters).then(value => { if (active) setStats(value) }).catch(() => { if (active) setStatsError('Não foi possível atualizar as estatísticas.') }).finally(() => { if (active) setStatsLoading(false) }); return () => { active = false } }, [filters, retry])
  return <main className={styles.page}>    <div className={styles.intro}><p className={styles.eyebrow}>Visão geral</p><h1>Bom trabalho. Aqui está sua sala.</h1><p>Acompanhe seus módulos e turmas em um só lugar.</p></div>
    {loading && <p className={styles.state} role='status'>Carregando resumo…</p>}
    {error && <p className={styles.error} role='alert'>{error} <button type='button' onClick={() => window.location.reload()}>Tentar novamente</button></p>}
    {data && <section className={styles.metrics} aria-label='Resumo da conta'>
      <article><span className={styles.metricIcon} aria-hidden='true'>▤</span><p>Módulos não arquivados</p><strong>{data.modules}</strong><button type='button' onClick={() => onNavigate('/teacher/modules')}>Gerenciar módulos <span aria-hidden='true'>→</span></button></article>
      <article><span className={styles.metricIcon} aria-hidden='true'>▣</span><p>Turmas ativas</p><strong>{data.activeClassrooms}</strong><button type='button' onClick={() => onNavigate('/teacher/classrooms')}>Ver turmas <span aria-hidden='true'>→</span></button></article>
      <article><span className={styles.metricIcon} aria-hidden='true'>♙</span><p>Alunos ativos</p><strong>{data.activeStudents}</strong><button type='button' onClick={() => onNavigate('/teacher/classrooms')}>Acompanhar progresso <span aria-hidden='true'>→</span></button></article>
    </section>}
    <section className={styles.next}><div><p className={styles.eyebrow}>Próximos passos</p><h2>Atalhos</h2><p>Abra uma turma para consultar o progresso dos alunos ou prepare conteúdo para suas aulas.</p></div><div className={styles.actions}><button type='button' onClick={() => onNavigate('/teacher/classrooms')}>Abrir turmas</button><button className={styles.secondary} type='button' onClick={() => onNavigate('/teacher/modules')}>Ver módulos</button></div></section>
    <StatisticsFilters value={filters} onChange={setFilters} classrooms={classroomOptions} modules={moduleOptions} showClassroom showModule showActivityType />
    {!statisticsFiltersReady(filters) && <p role="status">Selecione um intervalo de datas válido para atualizar.</p>}
    {statsLoading && <p role="status">Atualizando…</p>}{statsError && <p role="alert">{statsError} <button type="button" onClick={() => setRetry(value => value + 1)}>Tentar novamente</button></p>}
    {stats && <><p>{statisticsFilterSummary(filters, classroomOptions, moduleOptions)}</p><div className={styles.metrics}><StackedBarChart title="Conceitos por progresso (situação atual)" values={[{ label: 'Dominados', value: stats.distribution.mastered, color: '#36b37e' }, { label: 'Em aprendizagem', value: stats.distribution.learning, color: '#7887ff' }, { label: 'Não iniciados', value: stats.distribution.notStarted, color: '#777d8e' }]} /><LineChart title="Atividades e acertos" labels={stats.daily.map(item => item.date.slice(5))} series={[{ label: 'Atividades', color: '#7887ff', values: stats.daily.map(item => item.activities) }, { label: 'Acerto (%)', color: '#36b37e', values: stats.daily.map(item => item.correctRate) }]} /><BarChart title="Progresso médio por turma (%)" labels={stats.classrooms.map(item => item.name)} values={stats.classrooms.map(item => item.averageProgress)} suffix="%" /></div></>}

  </main>
}
