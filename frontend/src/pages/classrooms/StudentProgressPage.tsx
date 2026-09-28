import { useEffect, useState } from 'react'
import { teacherProgressApi, type StudentProgress } from '../../services/teacherProgressApi'
import styles from './TeacherProgressPages.module.css'

type Props = { classroomId: string; studentId: string; onBack: () => void }
export function StudentProgressPage({ classroomId, studentId, onBack }: Props) {
  const [data, setData] = useState<StudentProgress | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  useEffect(() => {
    let active = true
    setLoading(true); setError('')
    teacherProgressApi.student(classroomId, studentId).then(result => { if (active) setData(result) })
      .catch(() => { if (active) setError('Não foi possível carregar o progresso deste aluno.') })
      .finally(() => { if (active) setLoading(false) })
    return () => { active = false }
  }, [classroomId, studentId])
  return <main className={styles.page}>
    <button className={styles.back} type='button' onClick={onBack}>← Progresso da turma</button>
    <p className={styles.eyebrow}>Detalhes do aluno</p><h1>{data?.name || 'Aluno'}</h1>
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
