import { useEffect, useState } from 'react'
import { teacherProgressApi, type ClassroomProgress } from '../../services/teacherProgressApi'
import styles from './TeacherProgressPages.module.css'
import { BarChart, LineChart, StackedBarChart } from '../../components/charts/Charts'
import type { ClassroomStats } from '../../services/teacherProgressApi'

export function formatLastAccess(value: string | null) {
  if (!value) return 'Nunca'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return 'Nunca'
  const today = new Date()
  const yesterday = new Date(today)
  yesterday.setDate(today.getDate() - 1)
  const sameDay = (left: Date, right: Date) => left.getFullYear() === right.getFullYear() && left.getMonth() === right.getMonth() && left.getDate() === right.getDate()
  if (sameDay(date, today)) return 'Hoje'
  if (sameDay(date, yesterday)) return 'Ontem'
  return new Intl.DateTimeFormat('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' }).format(date)
}

type Props = { classroomId: string; onBack: () => void; onStudent: (studentId: string) => void }
export function ClassroomProgressPage({ classroomId, onBack, onStudent }: Props) {
  const [data, setData] = useState<ClassroomProgress | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [stats, setStats] = useState<ClassroomStats | null>(null)
  const [days, setDays] = useState(30)
  useEffect(() => {
    let active = true
    setLoading(true); setError('')
    teacherProgressApi.classroom(classroomId).then(result => { if (active) setData(result) })
      .catch(() => { if (active) setError('Não foi possível carregar o progresso da turma.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [classroomId])
  useEffect(() => { let active = true; teacherProgressApi.classroomStatistics(classroomId, days).then(value => { if (active) setStats(value) }).catch(() => { if (active) setStats(null) }); return () => { active = false } }, [classroomId, days])
  return <main className={styles.page}>
    <button className={styles.back} type='button' onClick={onBack}>← Turmas</button>
    <p className={styles.eyebrow}>Acompanhamento da turma</p><h1>{data?.classroomName ?? 'Progresso dos alunos'}</h1>
    <label>Período <select value={days} onChange={event => setDays(Number(event.target.value))}><option value={7}>7 dias</option><option value={30}>30 dias</option><option value={90}>90 dias</option></select></label>
    {stats && <div className={styles.tableCard}><StackedBarChart title="Distribuição dos conceitos" values={[{ label: 'Dominados', value: stats.distribution.mastered, color: '#36b37e' }, { label: 'Em aprendizagem', value: stats.distribution.learning, color: '#7887ff' }, { label: 'Não iniciados', value: stats.distribution.notStarted, color: '#777d8e' }]} /><LineChart title="Atividades e taxa de acerto" labels={stats.daily.map(item => item.date.slice(5))} series={[{ label: 'Atividades', color: '#7887ff', values: stats.daily.map(item => item.activities) }, { label: 'Acertos (%)', color: '#36b37e', values: stats.daily.map(item => item.correctRate) }]} /><BarChart title="Alunos por faixa de progresso" labels={stats.progressBands.map(item => item.range)} values={stats.progressBands.map(item => item.students)} /><BarChart title="Taxa de acerto por atividade (%)" labels={stats.activityTypes.map(item => item.type)} values={stats.activityTypes.map(item => item.correctRate ?? 0)} suffix="%" hasData={stats.activityTypes.some(item => item.correctRate !== null)} emptyText="Sem tentativas avaliáveis no período" /><BarChart title="Conceitos com mais erros (%)" labels={stats.mostErrors.map(item => item.name)} values={stats.mostErrors.map(item => item.errorRate)} suffix="%" /></div>}
    {loading && <p role='status' className={styles.state}>Carregando progresso…</p>}
    {error && <p role='alert' className={styles.error}>{error}<button type='button' onClick={() => { setData(null); setLoading(true); setError(''); void teacherProgressApi.classroom(classroomId).then(setData).catch(() => setError('Não foi possível carregar o progresso da turma.')).finally(() => setLoading(false)) }}>Tentar novamente</button></p>}
    {!loading && !error && data && (data.students.length === 0 ? <section className={styles.empty}><h2>Nenhum aluno cadastrado nesta turma.</h2><p>Cadastre alunos na página da turma para acompanhar o progresso.</p></section> :
      <section className={styles.tableCard} aria-label='Progresso dos alunos'><p className={styles.caption}>{data.students.length} alunos · {data.totalActiveConcepts} conceitos ativos nos módulos publicados</p>
        <div className={styles.tableWrap}><table><thead><tr><th scope='col'>Matrícula</th><th scope='col'>Nome</th><th scope='col'>Progresso</th><th scope='col'>Revisões pendentes</th><th scope='col'>Último acesso</th><th scope='col'><span className={styles.srOnly}>Detalhes</span></th></tr></thead>
          <tbody>{data.students.map(student => <tr key={student.studentId}><td><strong>{student.enrollmentNumber}</strong></td><td>{student.name || '—'} {!student.isActive && <span className={styles.inactive}>Desativado</span>}</td>
            <td><span className={styles.percent}>{student.progressPercent}%</span><span className={styles.subtle}>{student.masteredConcepts}/{data.totalActiveConcepts} dominados</span></td>
            <td>{student.pendingReviews}</td><td>{formatLastAccess(student.lastAccessAtUtc)}</td><td><button className={styles.detailButton} type='button' onClick={() => onStudent(student.studentId)}>Detalhes</button></td></tr>)}</tbody>
        </table></div></section>)}
  </main>
}
