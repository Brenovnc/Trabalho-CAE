import { useEffect, useState } from 'react'
import { teacherProgressApi, type StudentProgress } from '../../services/teacherProgressApi'
import styles from './TeacherProgressPages.module.css'
import { BarChart, LineChart, StackedBarChart } from '../../components/charts/Charts'
import type { StudentStats } from '../../services/teacherProgressApi'

type Props = { classroomId: string; studentId: string; onBack: () => void }
export function StudentProgressPage({ classroomId, studentId, onBack }: Props) {
  const [data, setData] = useState<StudentProgress | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [stats, setStats] = useState<StudentStats | null>(null)
  const [days, setDays] = useState(30)
  useEffect(() => {
    let active = true
    setLoading(true); setError('')
    teacherProgressApi.student(classroomId, studentId).then(result => { if (active) setData(result) })
      .catch(() => { if (active) setError('Não foi possível carregar o progresso deste aluno.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [classroomId, studentId])
  useEffect(() => { let active = true; teacherProgressApi.studentStatistics(classroomId, studentId, days).then(value => { if (active) setStats(value) }).catch(() => { if (active) setStats(null) }); return () => { active = false } }, [classroomId, studentId, days])
  return <main className={styles.page}>
    <button className={styles.back} type='button' onClick={onBack}>← Progresso da turma</button>
    <p className={styles.eyebrow}>Detalhes do aluno</p><h1>{data?.name || 'Aluno'}</h1>
    <label>Período <select value={days} onChange={event => setDays(Number(event.target.value))}><option value={7}>7 dias</option><option value={30}>30 dias</option><option value={90}>90 dias</option></select></label>
    {stats && <section className={styles.moduleCards}><StackedBarChart title="Distribuição dos conceitos" values={[{ label: 'Dominados', value: stats.distribution.mastered, color: '#36b37e' }, { label: 'Em aprendizagem', value: stats.distribution.learning, color: '#7887ff' }, { label: 'Não iniciados', value: stats.distribution.notStarted, color: '#777d8e' }]} /><LineChart title="Atividades e acertos" labels={stats.daily.map(item => item.date.slice(5))} series={[{ label: 'Atividades', color: '#7887ff', values: stats.daily.map(item => item.activities) }, { label: 'Acertos (%)', color: '#36b37e', values: stats.daily.map(item => item.correctRate) }]} /><BarChart title="Tentativas por atividade" labels={stats.activityTypes.map(item => item.type)} values={stats.activityTypes.map(item => item.attempts)} /></section>}
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
