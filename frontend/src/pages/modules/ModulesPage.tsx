import { useEffect, useState, type ChangeEvent, type FormEvent } from 'react'
import { moduleApi } from '../../services/moduleApi'
import type { Concept, ConceptInput, ModuleDetails, ModuleInput, ModuleSummary, PublicationValidation } from '../../types/modules'
import { moduleStatusLabel } from '../../types/modules'
import styles from './ModulesPage.module.css'

type ActivityDraft = { id?: string; statement: string; isCorrect: boolean; explanation: string }
type FillDraft = { id?: string; text: string; answers: string; distractors: string }
type OrderDraft = { id?: string; instruction: string; items: string }
type ConceptDraft = {
  id?: string; name: string; definition: string; keywords: string; clues: string; prerequisiteIds: string[]
  recognitionActivities: ActivityDraft[]; fillBlankActivities: FillDraft[]; orderingActivities: OrderDraft[]
}
const blankModule: ModuleInput = { title: '', description: '', subject: '', version: 1 }
const blankConcept = (): ConceptDraft => ({
  name: '', definition: '', keywords: '', clues: '', prerequisiteIds: [],
  recognitionActivities: [], fillBlankActivities: [], orderingActivities: [],
})
const lines = (value: string) => value.split('\n').map(item => item.trim()).filter(Boolean)
const statusCount = (list: { isActive: boolean }[]) => list.filter(item => item.isActive).length

function draftFromConcept(concept: Concept): ConceptDraft {
  return {
    id: concept.id, name: concept.name, definition: concept.definition,
    keywords: concept.keywords.join('\n'), clues: concept.clues.join('\n'),
    prerequisiteIds: concept.prerequisiteIds,
    recognitionActivities: concept.recognitionActivities.filter(item => item.isActive).map(({ id, statement, isCorrect, explanation }) => ({ id, statement, isCorrect, explanation })),
    fillBlankActivities: concept.fillBlankActivities.filter(item => item.isActive).map(item => ({
      id: item.id, text: item.text,
      answers: item.answers.map(answer => `${answer.slotNumber}:${answer.correctText}`).join('\n'),
      distractors: item.distractors.join('\n'),
    })),
    orderingActivities: concept.orderingActivities.filter(item => item.isActive).map(item => ({
      id: item.id, instruction: item.instruction, items: item.items.join('\n'),
    })),
  }
}

function toConceptInput(draft: ConceptDraft): ConceptInput {
  return {
    name: draft.name, definition: draft.definition,
    keywords: lines(draft.keywords), clues: lines(draft.clues), prerequisiteIds: draft.prerequisiteIds,
    recognitionActivities: draft.recognitionActivities.map(item => ({ ...item, id: item.id })),
    fillBlankActivities: draft.fillBlankActivities.map(item => ({
      id: item.id,
      text: item.text,
      answers: lines(item.answers).map(line => {
        const separator = line.indexOf(':')
        return { slotNumber: Number(line.slice(0, separator)), correctText: line.slice(separator + 1).trim() }
      }),
      distractors: lines(item.distractors),
    })),
    orderingActivities: draft.orderingActivities.map(item => ({ id: item.id, instruction: item.instruction, items: lines(item.items) })),
  }
}

export function ModulesPage() {
  const [modules, setModules] = useState<ModuleSummary[]>([])
  const [selected, setSelected] = useState<ModuleDetails | null>(null)
  const [moduleDraft, setModuleDraft] = useState<ModuleInput>(blankModule)
  const [conceptDraft, setConceptDraft] = useState<ConceptDraft>(blankConcept())
  const [validation, setValidation] = useState<PublicationValidation | null>(null)
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  async function refreshList() { setLoading(true); try { setModules(await moduleApi.list()) } finally { setLoading(false) } }
  async function openModule(id: string) {
    try {
      setError(''); setNotice(''); setBusy(true)
      const [details, result] = await Promise.all([moduleApi.get(id), moduleApi.validate(id)])
      setSelected(details)
      setModuleDraft({ title: details.title, description: details.description ?? '', subject: details.subject, version: details.version })
      setValidation(result)
      setConceptDraft(blankConcept())
    } catch (cause) { setError(messageOf(cause)) } finally { setBusy(false) }
  }
  useEffect(() => { void refreshList().catch(cause => setError(messageOf(cause))) }, [])

  async function reloadSelected(id = selected?.id) {
    if (!id) return
    const [details, result] = await Promise.all([moduleApi.get(id), moduleApi.validate(id)])
    setSelected(details); setValidation(result)
    setModuleDraft({ title: details.title, description: details.description ?? '', subject: details.subject, version: details.version })
    if (conceptDraft.id) {
      const updatedConcept = details.concepts.find(item => item.id === conceptDraft.id)
      setConceptDraft(updatedConcept ? draftFromConcept(updatedConcept) : blankConcept())
    }
    await refreshList()
  }

  async function run(action: () => Promise<unknown>, success: string, reload = true) {
    try {
      setBusy(true); setError(''); setNotice('')
      const result = await action()
      setNotice(success)
      if (reload && selected) await reloadSelected()
      return result
    } catch (cause) { setError(messageOf(cause)); return null } finally { setBusy(false) }
  }

  async function createModule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const created = await run(() => moduleApi.create(moduleDraft), 'Módulo criado.', false) as ModuleDetails | null
    if (created) await openModule(created.id)
    await refreshList().catch(cause => setError(messageOf(cause)))
  }
  async function saveModule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await run(() => moduleApi.update(selected!.id, moduleDraft), 'Dados do módulo salvos.')
  }
  async function saveConcept(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const input = toConceptInput(conceptDraft)
    const result = conceptDraft.id
      ? await run(() => moduleApi.updateConcept(selected!.id, conceptDraft.id!, input), 'Conceito salvo.')
      : await run(() => moduleApi.createConcept(selected!.id, input), 'Conceito criado.')
    if (result) {
      const concept = result as Concept
      setConceptDraft(draftFromConcept(concept))
      await reloadSelected()
    }
  }
  async function duplicateModule() {
    const copy = await run(() => moduleApi.duplicate(selected!.id), 'Módulo duplicado.', false) as ModuleDetails | null
    if (copy) { await refreshList(); await openModule(copy.id) }
  }
  async function duplicateConcept() {
    const copy = await run(() => moduleApi.duplicateConcept(selected!.id, conceptDraft.id!), 'Conceito duplicado.') as Concept | null
    if (copy) setConceptDraft(draftFromConcept(copy))
  }
  async function deactivateConcept() {
    if (!conceptDraft.id || !selected) return
    await run(() => moduleApi.deactivateConcept(selected.id, conceptDraft.id!), 'Conceito desativado.')
    setConceptDraft(blankConcept())
  }
  async function publish() {
    const published = await run(() => moduleApi.publish(selected!.id), 'Módulo publicado.')
    if (published) await reloadSelected()
  }
  async function archive() {
    if (!selected) return
    await run(() => moduleApi.archive(selected.id), 'Módulo arquivado.')
  }
  async function exportModule(id: string) {
    try {
      setBusy(true); setError(''); setNotice('')
      const file = await moduleApi.exportJson(id)
      const url = URL.createObjectURL(file.blob)
      const link = document.createElement('a')
      link.href = url; link.download = file.fileName; link.click()
      URL.revokeObjectURL(url)
      setNotice('JSON exportado.')
    } catch (cause) { setError(messageOf(cause)) } finally { setBusy(false) }
  }

  async function importFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.currentTarget.files?.[0]
    event.currentTarget.value = ''
    if (!file) return
    if (file.size > 1_048_576) { setError('O arquivo JSON pode ter no máximo 1 MiB.'); return }
    try {
      setBusy(true); setError(''); setNotice('')
      const imported = await moduleApi.importJson(await file.text())
      const result = await moduleApi.validate(imported.id)
      setSelected(imported)
      setModuleDraft({ title: imported.title, description: imported.description ?? '', subject: imported.subject, version: imported.version })
      setValidation(result); setConceptDraft(blankConcept())
      await refreshList()
      setNotice('Módulo importado como rascunho.')
    } catch (cause) { setError(messageOf(cause)) } finally { setBusy(false) }
  }

  const activeConcepts = selected?.concepts.filter(concept => concept.isActive) ?? []
  const issueConcept = (id: string | null) => id ? selected?.concepts.find(concept => concept.id === id)?.name ?? 'Conceito' : 'Módulo'

  return (
    <main className={styles.page}>
      <header className={styles.header}><div><p className={styles.eyebrow}>Biblioteca pedagógica</p><h1>Gestão de módulos</h1></div></header>
      {(error || notice) && <p className={error ? styles.error : styles.notice} role="status">{error || notice}</p>}
      {busy && <p className={styles.muted} role="status">Salvando…</p>}

      {!selected ? (
        <div className={styles.columns}>
          <section className={styles.panel}>
            <h2>Novo módulo</h2>
            <ModuleFields value={moduleDraft} onChange={setModuleDraft} onSubmit={createModule} busy={busy} />
          </section>
          <section className={styles.panel}>
            <h2>Meus módulos</h2>
            <label className={styles.importControl}>Importar módulo JSON<input type="file" accept=".json,application/json" disabled={busy} onChange={(event) => void importFile(event)} /></label>
            {loading ? <p className={styles.muted} role="status">Carregando lista...</p> : modules.length === 0 ? <p className={styles.muted}>Nenhum módulo criado.</p> : (
              <ul className={styles.moduleList}>{modules.map(module => (
                <li key={module.id} className={styles.moduleRow}>
                  <button type="button" className={styles.moduleLink} onClick={() => void openModule(module.id)}>
                    <span><strong>{module.title}</strong><small>{module.subject} · {module.activeConceptCount} conceitos ativos</small></span>
                    <span className={styles.status}>{moduleStatusLabel(module.status)}</span>
                  </button>
                  <button type="button" className={styles.secondary} disabled={busy} onClick={() => void exportModule(module.id)}>Exportar JSON</button>
                </li>
              ))}</ul>
            )}
          </section>
        </div>
      ) : (
        <>
          <button type="button" className={styles.back} onClick={() => { setSelected(null); setValidation(null); setModuleDraft(blankModule); setConceptDraft(blankConcept()) }}>← Meus módulos</button>
          <section className={styles.panel}>
            <div className={styles.titleRow}><div><p className={styles.eyebrow}>{moduleStatusLabel(selected.status)}</p><h2>{selected.title}</h2></div><div className={styles.actions}>
              <button className={styles.secondary} disabled={busy} type="button" onClick={() => void exportModule(selected.id)}>Exportar JSON</button>
              <button className={styles.secondary} disabled={busy} type="button" onClick={() => void duplicateModule()}>Duplicar módulo</button>
              {selected.status !== 2 && selected.status !== 'Archived' && <button className={styles.danger} disabled={busy} type="button" onClick={() => void archive()}>Arquivar</button>}
            </div></div>
            <form className={styles.formGrid} onSubmit={saveModule}>
              <label>Título<input required maxLength={160} value={moduleDraft.title} onChange={event => setModuleDraft({ ...moduleDraft, title: event.target.value })} /></label>
              <label>Disciplina<input required maxLength={120} value={moduleDraft.subject} onChange={event => setModuleDraft({ ...moduleDraft, subject: event.target.value })} /></label>
              <label className={styles.wide}>Descrição<textarea maxLength={4000} rows={3} value={moduleDraft.description ?? ''} onChange={event => setModuleDraft({ ...moduleDraft, description: event.target.value })} /></label>
              <label>Versão<input required type="number" min={1} value={moduleDraft.version} onChange={event => setModuleDraft({ ...moduleDraft, version: Number(event.target.value) })} /></label>
              <button disabled={busy} type="submit">Salvar módulo</button>
            </form>
          </section>

          <section className={styles.panel}>
            <div className={styles.titleRow}><div><h2>Validação de publicação</h2><p className={styles.muted}>{activeConcepts.length} conceitos ativos</p></div>
              <button type="button" disabled={busy || !validation?.isValid || selected.status === 2 || selected.status === 'Archived'} onClick={() => void publish()}>Publicar módulo</button>
            </div>
            {validation && (validation.isValid ? <p className={styles.valid}>✓ Conteúdo válido para publicação. Ordenação é opcional.</p> : (
              <ul className={styles.issues}>{validation.errors.map((issue, index) => <li key={`${issue.code}-${issue.conceptId}-${index}`}><strong>{issueConcept(issue.conceptId)}:</strong> {issue.message}</li>)}</ul>
            ))}
          </section>

          <div className={styles.editorColumns}>
            <section className={styles.panel}>
              <div className={styles.titleRow}><h2>Conceitos</h2><button type="button" className={styles.secondary} onClick={() => setConceptDraft(blankConcept())}>+ Novo conceito</button></div>
              {selected.concepts.length === 0 ? <p className={styles.muted}>Adicione conceitos para compor o módulo.</p> : (
                <ul className={styles.conceptList}>{selected.concepts.map(concept => (
                  <li key={concept.id} className={!concept.isActive ? styles.inactive : ''}>
                    <button type="button" className={styles.moduleLink} onClick={() => setConceptDraft(draftFromConcept(concept))}>
                      <span><strong>{concept.name}</strong><small>{concept.isActive ? `${concept.keywords.length} keywords · ${concept.clues.length}/3 pistas · ${statusCount(concept.recognitionActivities)}/3 verdadeiro/falso · ${statusCount(concept.fillBlankActivities)}/3 lacunas` : 'Desativado'}</small></span>
                    </button>
                  </li>
                ))}</ul>
              )}
            </section>
            <ConceptEditor module={selected} value={conceptDraft} onChange={setConceptDraft} onSubmit={saveConcept}
              onDuplicate={() => void duplicateConcept()} onDeactivate={() => void deactivateConcept()} busy={busy} />
          </div>
        </>
      )}
    </main>
  )
}

function ModuleFields({ value, onChange, onSubmit, busy }: { value: ModuleInput; onChange: (value: ModuleInput) => void; onSubmit: (event: FormEvent<HTMLFormElement>) => void; busy: boolean }) {
  return <form className={styles.formGrid} onSubmit={onSubmit}>
    <label>Título<input required maxLength={160} value={value.title} onChange={event => onChange({ ...value, title: event.target.value })} /></label>
    <label>Disciplina<input required maxLength={120} value={value.subject} onChange={event => onChange({ ...value, subject: event.target.value })} /></label>
    <label className={styles.wide}>Descrição<textarea rows={3} maxLength={4000} value={value.description ?? ''} onChange={event => onChange({ ...value, description: event.target.value })} /></label>
    <label>Versão<input type="number" min={1} required value={value.version} onChange={event => onChange({ ...value, version: Number(event.target.value) })} /></label>
    <button disabled={busy} type="submit">Criar módulo</button>
  </form>
}

function ConceptEditor({ module, value, onChange, onSubmit, onDuplicate, onDeactivate, busy }: {
  module: ModuleDetails; value: ConceptDraft; onChange: (value: ConceptDraft) => void; onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onDuplicate: () => void; onDeactivate: () => void; busy: boolean
}) {
  const activeConcepts = module.concepts.filter(concept => concept.isActive && concept.id !== value.id)
  function patch<K extends keyof ConceptDraft>(key: K, next: ConceptDraft[K]) { onChange({ ...value, [key]: next }) }
  return <section className={styles.panel}>
    <div className={styles.titleRow}><h2>{value.id ? 'Editar conceito' : 'Novo conceito'}</h2>{value.id && <div className={styles.actions}>
      <button type="button" className={styles.secondary} disabled={busy} onClick={onDuplicate}>Duplicar</button>
      <button type="button" className={styles.danger} disabled={busy} onClick={onDeactivate}>Desativar</button>
    </div>}</div>
    <form onSubmit={onSubmit} className={styles.conceptForm}>
      <label>Nome<input required maxLength={160} value={value.name} onChange={event => patch('name', event.target.value)} /></label>
      <label>Definição<textarea required maxLength={12000} rows={4} value={value.definition} onChange={event => patch('definition', event.target.value)} /></label>
      <label>Keywords <small>Uma por linha</small><textarea rows={3} value={value.keywords} onChange={event => patch('keywords', event.target.value)} /></label>
      <label>Pistas na ordem desejada <small>Uma por linha; são necessárias 3 para publicar</small><textarea rows={4} value={value.clues} onChange={event => patch('clues', event.target.value)} /></label>
      <fieldset><legend>Pré-requisitos</legend>
        {activeConcepts.length === 0 ? <p className={styles.muted}>Nenhum outro conceito disponível.</p> : activeConcepts.map(concept => (
          <label className={styles.check} key={concept.id}><input type="checkbox" checked={value.prerequisiteIds.includes(concept.id)} onChange={event => patch('prerequisiteIds', event.target.checked ? [...value.prerequisiteIds, concept.id] : value.prerequisiteIds.filter(id => id !== concept.id))} />{concept.name}</label>
        ))}
      </fieldset>
      <fieldset><legend>Verdadeiro/falso ({value.recognitionActivities.length}/3 mínimos)</legend>
        {value.recognitionActivities.map((activity, index) => <div className={styles.activity} key={activity.id ?? `tf-${index}`}>
          <label>Afirmação<input required value={activity.statement} onChange={event => patch('recognitionActivities', value.recognitionActivities.map((item, i) => i === index ? { ...item, statement: event.target.value } : item))} /></label>
          <label>Explicação<input required value={activity.explanation} onChange={event => patch('recognitionActivities', value.recognitionActivities.map((item, i) => i === index ? { ...item, explanation: event.target.value } : item))} /></label>
          <label className={styles.check}><input type="checkbox" checked={activity.isCorrect} onChange={event => patch('recognitionActivities', value.recognitionActivities.map((item, i) => i === index ? { ...item, isCorrect: event.target.checked } : item))} />A afirmação é verdadeira</label>
          <button type="button" className={styles.textButton} onClick={() => patch('recognitionActivities', value.recognitionActivities.filter((_, i) => i !== index))}>Remover atividade</button>
        </div>)}
        <button type="button" className={styles.secondary} onClick={() => patch('recognitionActivities', [...value.recognitionActivities, { statement: '', isCorrect: false, explanation: '' }])}>+ Atividade verdadeiro/falso</button>
      </fieldset>
      <fieldset><legend>Lacunas ({value.fillBlankActivities.length}/3 mínimos)</legend>
        <p className={styles.muted}>Marque os slots no texto como {'{{1}}'} e informe respostas como <code>1:DNS</code>, uma por linha.</p>
        {value.fillBlankActivities.map((activity, index) => <div className={styles.activity} key={activity.id ?? `fill-${index}`}>
          <label>Texto com slots<textarea required rows={2} value={activity.text} onChange={event => patch('fillBlankActivities', value.fillBlankActivities.map((item, i) => i === index ? { ...item, text: event.target.value } : item))} /></label>
          <label>Respostas corretas<textarea required rows={2} value={activity.answers} onChange={event => patch('fillBlankActivities', value.fillBlankActivities.map((item, i) => i === index ? { ...item, answers: event.target.value } : item))} /></label>
          <label>Distratores <small>Uma opção por linha</small><textarea rows={2} value={activity.distractors} onChange={event => patch('fillBlankActivities', value.fillBlankActivities.map((item, i) => i === index ? { ...item, distractors: event.target.value } : item))} /></label>
          <button type="button" className={styles.textButton} onClick={() => patch('fillBlankActivities', value.fillBlankActivities.filter((_, i) => i !== index))}>Remover atividade</button>
        </div>)}
        <button type="button" className={styles.secondary} onClick={() => patch('fillBlankActivities', [...value.fillBlankActivities, { text: '', answers: '', distractors: '' }])}>+ Atividade de lacunas</button>
      </fieldset>
      <fieldset><legend>Ordenação (opcional)</legend>
        {value.orderingActivities.map((activity, index) => <div className={styles.activity} key={activity.id ?? `order-${index}`}>
          <label>Instrução<input required value={activity.instruction} onChange={event => patch('orderingActivities', value.orderingActivities.map((item, i) => i === index ? { ...item, instruction: event.target.value } : item))} /></label>
          <label>Itens na ordem correta <small>Um por linha</small><textarea required rows={3} value={activity.items} onChange={event => patch('orderingActivities', value.orderingActivities.map((item, i) => i === index ? { ...item, items: event.target.value } : item))} /></label>
          <button type="button" className={styles.textButton} onClick={() => patch('orderingActivities', value.orderingActivities.filter((_, i) => i !== index))}>Remover atividade</button>
        </div>)}
        <button type="button" className={styles.secondary} onClick={() => patch('orderingActivities', [...value.orderingActivities, { instruction: '', items: '' }])}>+ Atividade de ordenação</button>
      </fieldset>
      <div className={styles.formActions}><button disabled={busy} type="submit">{value.id ? 'Salvar conceito' : 'Adicionar conceito'}</button><button type="button" className={styles.secondary} onClick={() => onChange(blankConcept())}>Limpar formulário</button></div>
    </form>
  </section>
}

function messageOf(cause: unknown) { return cause instanceof Error ? cause.message : 'Não foi possível concluir a operação.' }
