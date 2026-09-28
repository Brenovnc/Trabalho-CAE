import { useEffect, useState, type FormEvent } from 'react'
import { classroomApi, type ClassroomDetails, type ClassroomSummary, type OneTimeCredentials, type Student, type StudentCsvPreview } from '../../services/classroomApi'
import { moduleApi } from '../../services/moduleApi'
import type { ModuleSummary } from '../../types/modules'
import styles from './ClassroomsPage.module.css'

type Props = { onProgress: (classroomId: string) => void }
const messageOf = (error: unknown) => error instanceof Error ? error.message : 'Não foi possível concluir a operação.'

export function ClassroomsPage({ onProgress }: Props) {
  const [items, setItems] = useState<ClassroomSummary[]>([])
  const [modules, setModules] = useState<ModuleSummary[]>([])
  const [selected, setSelected] = useState<ClassroomDetails | null>(null)
  const [students, setStudents] = useState<Student[]>([])
  const [name, setName] = useState(''); const [code, setCode] = useState('')
  const [enrollment, setEnrollment] = useState(''); const [studentName, setStudentName] = useState('')
  const [moduleId, setModuleId] = useState('')
  const [credentials, setCredentials] = useState<OneTimeCredentials | null>(null)
  const [csvFile, setCsvFile] = useState<File | null>(null)
  const [csvPreview, setCsvPreview] = useState<StudentCsvPreview | null>(null)
  const [csvResult, setCsvResult] = useState<{ createdCount: number; skippedRows: StudentCsvPreview['rows'] } | null>(null)
  const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)

  async function refresh() { setLoading(true); try { setItems(await classroomApi.list()) } finally { setLoading(false) } }
  async function open(id: string) {
    setError(''); setNotice(''); setBusy(true)
    try { const [classroom, list] = await Promise.all([classroomApi.get(id), classroomApi.students(id)]); setSelected(classroom); setStudents(list); setName(classroom.name); setCode(classroom.code) }
    catch (cause) { setError(messageOf(cause)) } finally { setBusy(false) }
  }
  useEffect(() => { void Promise.all([refresh(), moduleApi.list()]).then(([, available]) => setModules(available)).catch(cause => setError(messageOf(cause))) }, [])

  async function run(action: () => Promise<unknown>, message: string) {
    setBusy(true); setError(''); setNotice('')
    try { await action(); setNotice(message); await refresh(); if (selected) await open(selected.id) }
    catch (cause) { setError(messageOf(cause)) } finally { setBusy(false) }
  }
  async function saveClassroom(event: FormEvent) {
    event.preventDefault()
    await run(() => selected ? classroomApi.update(selected.id, name, code) : classroomApi.create(name, code), selected ? 'Turma atualizada.' : 'Turma criada.')
    if (!selected) { setName(''); setCode('') }
  }
  async function createStudent(event: FormEvent) {
    event.preventDefault(); if (!selected) return
    setBusy(true); setError(''); setNotice(''); setCredentials(null)
    try { const generated = await classroomApi.createStudent(selected.id, enrollment, studentName); setCredentials(generated); setEnrollment(''); setStudentName(''); setNotice('Aluno cadastrado. Guarde o código temporário agora; ele não poderá ser consultado novamente.'); await open(selected.id) }
    catch (cause) { setError(messageOf(cause)) } finally { setBusy(false) }
  }
  async function oneTime(action: () => Promise<OneTimeCredentials>) {
    setBusy(true); setError(''); setCredentials(null)
    try { setCredentials(await action()); setNotice('Novo código gerado. Guarde-o agora; ele não poderá ser consultado novamente.'); if (selected) await open(selected.id) }
    catch (cause) { setError(messageOf(cause)) } finally { setBusy(false) }
  }
  async function copyCode() { if (credentials) await navigator.clipboard.writeText(credentials.temporaryAccessCode) }
  async function previewCsv(event: FormEvent) {
    event.preventDefault(); if (!selected || !csvFile) return
    setBusy(true); setError(''); setNotice(''); setCsvResult(null)
    try { setCsvPreview(await classroomApi.previewCsv(selected.id, csvFile)) }
    catch (cause) { setError(messageOf(cause)); setCsvPreview(null) } finally { setBusy(false) }
  }

  async function confirmCsv() {
    if (!selected || !csvFile) return
    setBusy(true); setError(''); setNotice('')
    try {
      const result = await classroomApi.confirmCsv(selected.id, csvFile)
      const blob = new Blob(['\uFEFF', result.credentialsCsv], { type: 'text/csv;charset=utf-8' })
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url; anchor.download = 'credenciais-' + selected.code.toLowerCase() + '.csv'; anchor.click()
      URL.revokeObjectURL(url)
      setCsvResult({ createdCount: result.createdCount, skippedRows: result.skippedRows })
      setCsvPreview(null); setCsvFile(null)
      setNotice(result.createdCount + ' aluno(s) cadastrado(s). O CSV de códigos foi baixado agora e não poderá ser recuperado depois.')
      await open(selected.id)
    } catch (cause) { setError(messageOf(cause)) } finally { setBusy(false) }
  }

  return <main className={styles.page}>
    <header className={styles.header}><div><p>Organização</p><h1>Turmas e alunos</h1><p>Gerencie matrículas, módulos associados e o progresso dos alunos.</p></div></header>
    {(error || notice) && <p role="status" className={error ? styles.error : styles.notice}>{error || notice}</p>}{busy && <p role="status">Salvando…</p>}
    {credentials && <section className={styles.oneTime}><h2>Código temporário — mostrar uma única vez</h2><p>Matrícula: {credentials.enrollmentNumber}{credentials.name ? ` · ${credentials.name}` : ''}</p><code>{credentials.temporaryAccessCode}</code><p>Expira em {new Date(credentials.expiresAtUtc).toLocaleString()}</p><button type="button" onClick={() => void copyCode()}>Copiar código</button><button type="button" onClick={() => setCredentials(null)}>Fechar</button></section>}
    {!selected ? <div className={styles.columns}>
      <form className={styles.panel} onSubmit={saveClassroom}><h2>Nova turma</h2><label>Nome<input required maxLength={160} value={name} onChange={e => setName(e.target.value)} /></label><label>Código<input required minLength={3} maxLength={32} pattern="[A-Za-z0-9]+(-[A-Za-z0-9]+)*" title="Use letras, números e hífens entre grupos" value={code} onChange={e => setCode(e.target.value)} /></label><button disabled={busy}>Criar turma</button></form>
      <section className={styles.panel}><h2>Minhas turmas</h2>{loading ? <p role="status">Carregando turmas...</p> : items.length ? <ul>{items.map(x => <li key={x.id}><button className={styles.link} type="button" onClick={() => { setCredentials(null); void open(x.id) }}><strong>{x.name}</strong><span>{x.code} · {x.studentCount} alunos · {x.moduleCount} módulos · {x.status}</span></button></li>)}</ul> : <p>Nenhuma turma cadastrada.</p>}</section>
    </div> : <>
      <button type="button" onClick={() => { setSelected(null); setCredentials(null); setNotice('') }}>← Minhas turmas</button>
      <button type="button" onClick={() => onProgress(selected.id)}>Acompanhar progresso</button>
      <section className={styles.panel}><h2>{selected.name} <small>({selected.status})</small></h2><form className={styles.form} onSubmit={saveClassroom}><label>Nome<input required maxLength={160} value={name} onChange={e => setName(e.target.value)} /></label><label>Código<input required minLength={3} maxLength={32} pattern="[A-Za-z0-9]+(-[A-Za-z0-9]+)*" value={code} onChange={e => setCode(e.target.value)} /></label><button disabled={busy}>Salvar</button><button type="button" disabled={busy || selected.status === 'Archived'} onClick={() => void run(() => classroomApi.archive(selected.id), 'Turma arquivada.')}>Arquivar</button></form>
        <h3>Módulos associados</h3><ul>{selected.modules.map(m => <li key={m.id}>{m.title} · {m.status} <button type="button" onClick={() => void run(() => classroomApi.unassign(selected.id, m.id), 'Módulo desassociado.')}>Desassociar</button></li>)}</ul>
        <form className={styles.form} onSubmit={e => { e.preventDefault(); if (moduleId) void run(() => classroomApi.assign(selected.id, moduleId), 'Módulo associado.') }}><label>Associar módulo publicado<select value={moduleId} onChange={e => setModuleId(e.target.value)}><option value="">Selecione…</option>{modules.filter(m => m.status === 1 || m.status === 'Published').map(m => <option key={m.id} value={m.id}>{m.title}</option>)}</select></label><button disabled={busy || !moduleId}>Associar</button></form>
      </section>
      <section className={styles.panel}><h2>Alunos</h2><form className={styles.form} onSubmit={e => void createStudent(e)}><label>Matrícula<input required maxLength={64} value={enrollment} onChange={e => setEnrollment(e.target.value)} /></label><label>Nome (opcional)<input maxLength={120} value={studentName} onChange={e => setStudentName(e.target.value)} /></label><button disabled={busy || selected.status === 'Archived'}>Cadastrar e gerar código</button></form>
        <section className={styles.csvImport} aria-labelledby="csv-import-title"><h3 id="csv-import-title">Importar alunos por CSV</h3><p>Colunas aceitas: <code>matricula</code> e, opcionalmente, <code>nome</code>. Somente linhas válidas serão cadastradas. Códigos temporários são disponibilizados uma única vez no download após confirmar.</p>
          <form className={styles.form} onSubmit={e => void previewCsv(e)}><label>Arquivo CSV (até 1 MiB)<input type="file" accept=".csv,text/csv" onChange={e => { setCsvFile(e.target.files?.[0] ?? null); setCsvPreview(null); setCsvResult(null) }} /></label><button disabled={busy || selected.status === 'Archived' || !csvFile}>Analisar CSV</button></form>
          {csvPreview && <><p role="status">{csvPreview.totalRows} linhas: {csvPreview.validRows} válidas e {csvPreview.invalidRows} inválidas. A confirmação reanalisa o arquivo; apenas válidas serão cadastradas.</p><div className={styles.csvTableWrap}><table className={styles.csvTable}><thead><tr><th>Linha</th><th>Matrícula</th><th>Nome</th><th>Estado / erros</th></tr></thead><tbody>{csvPreview.rows.map(row => <tr key={row.lineNumber} data-valid={row.isValid}><td>{row.lineNumber}</td><td>{row.enrollmentNumber || '—'}</td><td>{row.name || '—'}</td><td>{row.isValid ? 'Válida' : row.errors.join(' ')}</td></tr>)}</tbody></table></div><button type="button" disabled={busy || !csvPreview.validRows} onClick={() => void confirmCsv()}>Confirmar {csvPreview.validRows} linha(s) válida(s)</button></>}
          {csvResult && <><p role="status">Criados: {csvResult.createdCount}. Ignorados: {csvResult.skippedRows.length}.</p>{csvResult.skippedRows.length > 0 && <ul>{csvResult.skippedRows.map(row => <li key={row.lineNumber}>Linha {row.lineNumber}: {row.errors.join(' ')}</li>)}</ul>}</>}
        </section>
        {students.length ? <ul>{students.map(student => <li key={student.id}><strong>{student.enrollmentNumber}</strong> {student.name ?? '—'} · {student.isActive ? (student.isActivated ? 'Ativo' : 'Aguardando ativação') : 'Desativado'} <button type="button" disabled={busy || !student.isActive} onClick={() => void oneTime(() => classroomApi.resetAccess(selected.id, student.id))}>Redefinir acesso</button><button type="button" disabled={busy || !student.isActive} onClick={() => void run(() => classroomApi.deactivate(selected.id, student.id), 'Aluno desativado.')}>Desativar</button></li>)}</ul> : <p>Nenhum aluno cadastrado.</p>}
      </section>
    </>}
  </main>
}

