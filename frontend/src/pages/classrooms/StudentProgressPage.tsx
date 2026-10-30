import { useEffect, useState } from 'react'
import { teacherProgressApi, type StudentProgress } from '../../services/teacherProgressApi'
import styles from './TeacherProgressPages.module.css'
import { BarChart, LineChart, StackedBarChart } from '../../components/charts/Charts'
import type { StudentStats } from '../../services/teacherProgressApi'
import { StatisticsFilters, defaultStatisticsFilters, statisticsFilterSummary, statisticsFiltersReady, type FilterOption } from '../../components/charts/StatisticsFilters'
import type { StatisticsFilters as FilterValues } from '../../services/teacherProgressApi'
import { activityTypeLabel } from '../../utils/activityTypeLabels'
import { ChartTabsCard, type ChartTab } from '../../components/charts/ChartTabsCard'

type Props = { classroomId: string; studentId: string; onBack: () => void }
export function StudentProgressPage({ classroomId, studentId, onBack }: Props) {
  const [chartTab, setChartTab] = useState('distribution')
  const [data, setData] = useState<StudentProgress | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [stats, setStats] = useState<StudentStats | null>(null)
  const [filters, setFilters] = useState<FilterValues>(defaultStatisticsFilters)
  const [statsLoading, setStatsLoading] = useState(false)
  const [statsError, setStatsError] = useState('')
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    let active = true
    setLoading(true); setError('')
    teacherProgressApi.student(classroomId, studentId).then(result => { if (active) setData(result) })
      .catch(() => { if (active) setError('Não foi possível carregar o progresso deste aluno.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [classroomId, studentId])
  useEffect(() => { if (!statisticsFiltersReady(filters)) return; let active = true; setStatsLoading(true); setStatsError(''); teacherProgressApi.studentStatistics(classroomId, studentId, filters).then(value => { if (active) setStats(value) }).catch(() => { if (active) setStatsError('Não foi possível atualizar as estatísticas do aluno.') }).finally(() => { if (active) setStatsLoading(false) }); return () => { active = false } }, [classroomId, studentId, filters, retry])
  const chartTabs: ChartTab[] = [
    { id: 'distribution', label: 'Distribuição dos conceitos', render: stats && <StackedBarChart title="Distribuição dos conceitos (situação atual)" size="large" values={[{label:'Dominados',value:stats.distribution.mastered,color:'#36b37e'},{label:'Em aprendizagem',value:stats.distribution.learning,color:'#7887ff'},{label:'Não iniciados',value:stats.distribution.notStarted,color:'#777d8e'}]} />, summary: stats ? `${stats.distribution.mastered} dominados · ${stats.distribution.learning} em aprendizagem · ${stats.distribution.notStarted} não iniciados` : undefined },
    { id: 'daily', label: 'Atividades e acertos', render: stats && <LineChart title="Atividades e acertos" labels={stats.daily.map(item=>item.date.slice(5))} size="large" series={[{label:'Atividades',color:'#7887ff',values:stats.daily.map(item=>item.activities)},{label:'Acertos (%)',color:'#36b37e',values:stats.daily.map(item=>item.correctRate)}]} />, summary: stats ? `${stats.daily.reduce((sum,item)=>sum+item.activities,0)} atividades no período` : undefined },
    { id: 'types', label: 'Acerto por atividade', render: stats && <BarChart title="Taxa de acerto por atividade (%)" size="large" labels={stats.activityTypes.map(item=>activityTypeLabel(item.type))} values={stats.activityTypes.map(item=>item.correctRate??0)} suffix="%" hasData={stats.activityTypes.some(item=>item.correctRate!==null)} emptyText="Sem tentativas avaliáveis no período" />, summary: stats ? `${stats.activityTypes.filter(item=>item.correctRate!==null).length} tipos com tentativas avaliáveis` : undefined },
    { id: 'errors', label: 'Conceitos com mais erros', render: stats && <BarChart title="Conceitos com mais erros (%)" size="large" labels={stats.mostErrors.map(item=>item.name)} values={stats.mostErrors.map(item=>item.errorRate)} suffix="%" />, summary: stats ? `${stats.mostErrors.length} conceitos com tentativas suficientes para comparação` : undefined },
  ]
  return <main className={styles.page}>
    <button className={styles.back} type='button' onClick={onBack}>← Progresso da turma</button>
    <p className={styles.eyebrow}>Detalhes do aluno</p><h1>{data?.name || 'Aluno'}</h1>
<StatisticsFilters value={filters} onChange={setFilters} modules={(data?.modules ?? []).map((item): FilterOption => ({ id: item.moduleId, label: item.title }))} showModule showActivityType disablePeriod={chartTab === 'distribution'} disableActivityType={chartTab === 'distribution'} />
    {!statisticsFiltersReady(filters) && <p role="status">Selecione um intervalo de datas válido para atualizar.</p>}
    {statsLoading && <p role="status">Atualizando…</p>}{statsError && <p role="alert">{statsError} <button type="button" onClick={() => setRetry(value => value + 1)}>Tentar novamente</button></p>}
    {stats && <p>{statisticsFilterSummary(filters, [], (data?.modules ?? []).map(item => ({ id: item.moduleId, label: item.title })))}</p>}
    <ChartTabsCard tabs={chartTabs} activeId={chartTab} onChange={setChartTab} label="Gráficos do aluno" loading={statsLoading} empty={!stats && !statsLoading} emptyMessage={statsError || "Não há dados para este gráfico."} />
    {loading && <p role='status' className={styles.state}>Carregando detalhes…</p>}
    {error && <p role='alert' className={styles.error}>{error}</p>}
    {data && <><section className={styles.studentInfo}><div><span>Matrícula</span><strong>{data.enrollmentNumber}</strong></div><div><span>Status</span><strong>{data.isActive ? 'Ativo' : 'Desativado'}</strong></div></section>
      {data.modules.length === 0 ? <section className={styles.empty}><h2>Nenhum módulo publicado associado</h2><p>Os módulos de progresso aparecem após serem publicados e associados a esta turma.</p></section> :
        <section className={styles.moduleCards} aria-label='Progresso por módulo'>{data.modules.map(module => <article className={styles.moduleCard} key={module.moduleId}>
          <div className={styles.moduleTitle}><div><p>{module.subject}</p><h2>{module.title}</h2></div><strong>{module.progressPercent}%</strong></div>
          <div className={styles.progressTrack} role='progressbar' aria-label={'Progresso em ' + module.title} aria-valuemin={0} aria-valuemax={100} aria-valuenow={module.progressPercent}><span style={{ width: module.progressPercent + '%' }} /></div>
          <p className={styles.caption}>{module.masteredConcepts} dominados de {module.activeConcepts} conceitos ativos</p>
          <dl className={styles.stats}><div><dt>Dominados</dt><dd>{module.masteredConcepts}</dd></div><div><dt>Em aprendizagem</dt><dd>{module.learningConcepts}</dd></div><div><dt>Não iniciados</dt><dd>{module.notStartedConcepts}</dd></div><div><dt>Revisões pendentes</dt><dd>{module.pendingReviews}</dd></div></dl>
        </article>)}</section>}
    </>}
  </main>
}
