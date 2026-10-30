import { useEffect, useMemo, useState } from 'react'
import { classroomApi } from '../../services/classroomApi'
import type { ClassroomSummary } from '../../services/classroomApi'
import { teacherProgressApi, type ClassroomProgress, type ClassroomStats, type StatisticsFilters as FilterValues } from '../../services/teacherProgressApi'
import { BarChart, LineChart, StackedBarChart } from '../../components/charts/Charts'
import { StatisticsFilters, defaultStatisticsFilters, statisticsFilterSummary, statisticsFiltersReady, type FilterOption } from '../../components/charts/StatisticsFilters'
import { formatLastAccess } from '../../utils/formatLastAccess'
import styles from './TeacherDashboardPage.module.css'
import { activityTypeLabel } from '../../utils/activityTypeLabels'
import { ChartTabsCard, type ChartTab } from '../../components/charts/ChartTabsCard'

type Props = { onNavigate: (path: string, replace?: boolean) => void; onStudent: (classroomId: string, studentId: string) => void }
const tabs = ['distribution', 'daily', 'bands', 'types', 'errors'] as const
export function TeacherDashboardPage({ onNavigate, onStudent }: Props) {
  const [classes, setClasses] = useState<ClassroomSummary[]>([])
  const [modules, setModules] = useState<FilterOption[]>([])
  const [classroomId, setClassroomId] = useState('')
  const [progress, setProgress] = useState<ClassroomProgress | null>(null)
  const [stats, setStats] = useState<ClassroomStats | null>(null)
  const [filters, setFilters] = useState<FilterValues>(defaultStatisticsFilters)
  const [tab, setTab] = useState<(typeof tabs)[number]>('distribution')
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [retry, setRetry] = useState(0)
  useEffect(() => { let active = true; classroomApi.list().then(c => { if (!active) return; const activeClasses = c.filter(x => x.status === 'Active'); setClasses(activeClasses); const query = new URLSearchParams(window.location.search).get('classroom'); let stored = ''; try { stored = localStorage.getItem('pere.lastClassroom') ?? '' } catch {} const chosen = activeClasses.some(x => x.id === query) ? query! : activeClasses.some(x => x.id === stored) ? stored : activeClasses[0]?.id ?? ''; setClassroomId(chosen) }).catch(() => setError('Não foi possível carregar as turmas.')).finally(() => { if (active) setLoading(false) }); return () => { active = false } }, [])
  useEffect(() => { if (!classroomId) { setModules([]); return } let active = true; classroomApi.get(classroomId).then(c => { if (active) setModules(c.modules.filter(x => x.status === 'Published' || x.status === '1').map(x => ({id:x.id,label:x.title}))) }).catch(() => { if(active) setModules([]) }); return () => { active = false } }, [classroomId])
  useEffect(() => { if (!classroomId) { setProgress(null); setStats(null); return } let active = true; setLoading(true); setError(''); Promise.all([teacherProgressApi.classroom(classroomId), teacherProgressApi.classroomStatistics(classroomId, filters)]).then(([p,s]) => { if (active) { setProgress(p); setStats(s) } }).catch(() => { if (active) setError('Não foi possível carregar os dados da turma.') }).finally(() => { if (active) setLoading(false) }); return () => { active = false } }, [classroomId, filters, retry])
  const selected = useMemo(() => classes.find(x => x.id === classroomId), [classes, classroomId])
  function chooseClass(id: string) { setClassroomId(id); setFilters(defaultStatisticsFilters); try { localStorage.setItem('pere.lastClassroom', id) } catch {} onNavigate(id ? `/teacher/dashboard?classroom=${encodeURIComponent(id)}` : '/teacher/dashboard', true) }
  const chartTabs: ChartTab[] = [
    { id: 'distribution', label: 'Distribuição', render: stats && <StackedBarChart title="Distribuição dos conceitos (situação atual)" size="large" values={[{label:'Dominados',value:stats.distribution.mastered,color:'#36b37e'},{label:'Em aprendizagem',value:stats.distribution.learning,color:'#7887ff'},{label:'Não iniciados',value:stats.distribution.notStarted,color:'#777d8e'}]} />, summary: stats ? `${stats.distribution.mastered} dominados · ${stats.distribution.learning} em aprendizagem · ${stats.distribution.notStarted} não iniciados` : undefined },
    { id: 'daily', label: 'Atividades e acertos', render: stats && <LineChart title="Atividades e taxa de acerto" labels={stats.daily.map(x=>x.date.slice(5))} size="large" series={[{label:'Atividades',color:'#7887ff',values:stats.daily.map(x=>x.activities)},{label:'Acertos (%)',color:'#36b37e',values:stats.daily.map(x=>x.correctRate)}]} />, summary: stats ? `${stats.daily.reduce((sum,item)=>sum+item.activities,0)} atividades no período` : undefined },
    { id: 'bands', label: 'Faixas de progresso', render: stats && <BarChart title="Alunos por faixa de progresso" size="large" labels={stats.progressBands.map(x=>x.range)} values={stats.progressBands.map(x=>x.students)} />, summary: stats ? `${stats.progressBands.reduce((sum,item)=>sum+item.students,0)} alunos distribuídos por faixa de progresso` : undefined },
    { id: 'types', label: 'Acerto por atividade', render: stats && <BarChart title="Taxa de acerto por atividade (%)" size="large" labels={stats.activityTypes.map(x=>activityTypeLabel(x.type))} values={stats.activityTypes.map(x=>x.correctRate??0)} suffix="%" hasData={stats.activityTypes.some(x=>x.correctRate!==null)} />, summary: stats ? `${stats.activityTypes.filter(x=>x.correctRate!==null).length} tipos com tentativas avaliáveis` : undefined },
    { id: 'errors', label: 'Mais erros', render: stats && <BarChart title="Conceitos com mais erros (%)" size="large" labels={stats.mostErrors.map(x=>x.name)} values={stats.mostErrors.map(x=>x.errorRate)} suffix="%" />, summary: stats ? `${stats.mostErrors.length} conceitos com tentativas suficientes para comparação` : undefined },
  ]
  const options = classes.map(x => ({ id: x.id, label: x.name }))
  return <main className={styles.page}><header className={styles.intro}><div><p className={styles.eyebrow}>Dashboard</p><h1>Acompanhamento</h1></div><label className={styles.classSelect}>Turma<select aria-label="Turma" value={classroomId} onChange={e => chooseClass(e.target.value)}>{classes.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label></header>
    {loading && !selected && <p role="status">Carregando dados…</p>}{error && <p role="alert" className={styles.error}>{error}<button onClick={() => setRetry(v => v + 1)}>Tentar novamente</button></p>}
    {!loading && !classes.length && <section className={styles.empty}><h2>Você ainda não tem turmas ativas.</h2><button onClick={() => onNavigate('/teacher/classrooms')}>Criar turma</button></section>}
    {selected && <>
      <section className={styles.filterCard}><h2>Filtros</h2><StatisticsFilters value={filters} onChange={setFilters} modules={modules} showModule showActivityType showMinAttempts disablePeriod={!(tab === 'daily' || tab === 'types' || tab === 'errors')} disableActivityType={!(tab === 'daily' || tab === 'types' || tab === 'errors')} disableMinAttempts={tab !== 'errors'} />{!statisticsFiltersReady(filters) && <p role="status">Selecione um intervalo de datas válido.</p>}<p>{statisticsFilterSummary(filters, options, modules)}</p></section>
      <ChartTabsCard tabs={chartTabs} activeId={tab} onChange={value => setTab(value as (typeof tabs)[number])} label="Gráficos do dashboard" loading={loading} empty={!stats && !loading && !error} emptyMessage="Não há dados para este gráfico nesta turma." />
      {progress && <section className={styles.tableCard} aria-label="Progresso dos alunos"><h2>Progresso dos alunos</h2>{progress.students.length===0 ? <p>Nenhum aluno cadastrado nesta turma.</p> : <div className={styles.tableWrap}><table><thead><tr><th>Matrícula</th><th>Nome</th><th>Progresso</th><th>Revisões pendentes</th><th>Último acesso</th><th>Detalhes</th></tr></thead><tbody>{progress.students.map(student=><tr key={student.studentId}><td><strong>{student.enrollmentNumber}</strong></td><td>{student.name||'—'}</td><td>{student.progressPercent}%</td><td>{student.pendingReviews}</td><td>{formatLastAccess(student.lastAccessAtUtc)}</td><td><button onClick={()=>onStudent(classroomId,student.studentId)}>Detalhes</button></td></tr>)}</tbody></table></div>}</section>}
    </>}
  </main>
}
