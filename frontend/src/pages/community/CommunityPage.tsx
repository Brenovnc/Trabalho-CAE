import { useEffect, useState } from 'react'
import { communityApi } from '../../services/communityApi'
import { moduleApi } from '../../services/moduleApi'
import type { CommunityItem, CommunityMineItem } from '../../types/community'
import type { ModuleSummary } from '../../types/modules'
import styles from './CommunityPage.module.css'

export function CommunityPage({ onNavigate }: { onNavigate: (path: string) => void }) {
  const [mine, setMine] = useState<CommunityMineItem[]>([]), [modules, setModules] = useState<ModuleSummary[]>([])
  const [items, setItems] = useState<CommunityItem[]>([]), [total, setTotal] = useState(0), [page, setPage] = useState(1)
  const [search, setSearch] = useState(''), [subject, setSubject] = useState(''), [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(''), [error, setError] = useState(''), [success, setSuccess] = useState('')
  async function reloadMine() { setMine(await communityApi.mine()) }
  async function load(nextPage = 1, query = search, filter = subject) { const result = await communityApi.search(query, filter, nextPage); setItems(nextPage === 1 ? result.items : current => [...current, ...result.items]); setTotal(result.total); setPage(nextPage) }
  useEffect(() => { void Promise.all([reloadMine(), moduleApi.list().then(setModules), load()]).catch(e => setError(e.message)) }, [])
  useEffect(() => { const timer = window.setTimeout(() => { void load(1).catch(e => setError(e.message)) }, 300); return () => window.clearTimeout(timer) }, [search, subject])
  const published = modules.filter(m => m.status === 1 || m.status === 'Published')
  const available = published.filter(m => !mine.some(x => x.communitySourceModuleId === m.id))
  async function share(id: string) { setBusy(id); setError(''); try { await communityApi.share(id); await Promise.all([reloadMine(), moduleApi.list().then(setModules)]); setOpen(false) } catch (e) { setError(e instanceof Error ? e.message : 'Falha ao enviar.') } finally { setBusy('') } }
  async function remove(id: string) { if (!window.confirm('Excluir definitivamente? Professores que já baixaram mantêm a cópia deles.')) return; setBusy(id); try { await communityApi.remove(id); await reloadMine() } catch (e) { setError(e instanceof Error ? e.message : 'Falha ao excluir.') } finally { setBusy('') } }
  async function download(item: CommunityItem) { setBusy(item.id); setError(''); try { await communityApi.download(item.id); setSuccess(`“${item.title}” foi adicionado aos seus módulos como rascunho.`); await load(page) } catch (e) { setError(e instanceof Error ? e.message : 'Falha ao baixar.') } finally { setBusy('') } }
  return <main className={styles.page}><header><p className={styles.eyebrow}>COMUNIDADE</p><h1>Módulos compartilhados</h1></header>
    {success && <div className={styles.notice} role="status" aria-live="polite">{success}<button onClick={() => onNavigate('/teacher/modules')}>Ver em Módulos</button><button aria-label="Dispensar" onClick={() => setSuccess('')}>×</button></div>}{error && <p role="alert" className={styles.error}>{error}</p>}
    <section className={styles.panel}><div className={styles.heading}><h2>Meus envios</h2><button onClick={() => setOpen(!open)}>Enviar módulo</button></div>
      {open && <div className={styles.shareBox}><p>Será criada uma cópia na comunidade. Seu nome e sua instituição ficarão visíveis para outros professores. Alterações futuras no módulo original não são enviadas automaticamente.</p>{available.length ? available.map(m => <div className={styles.row} key={m.id}><span>{m.title} · {m.subject} · {m.activeConceptCount} conceitos</span><button disabled={!!busy} onClick={() => void share(m.id)}>Enviar</button></div>) : <p>Nenhum módulo publicado disponível para envio.</p>}</div>}
      {mine.length ? mine.map(item => <div className={styles.row} key={item.id}><span><strong>{item.title}</strong><small>{item.subject} · {item.activeConceptCount} conceitos · {new Date(item.sharedAtUtc).toLocaleDateString('pt-BR')} · {item.downloadCount} downloads</small></span><button onClick={() => onNavigate(`/teacher/modules?open=${item.id}&from=community`)}>Editar</button><button disabled={!!busy} onClick={() => void remove(item.id)}>Excluir</button></div>) : <p>Nenhum envio ainda. Compartilhe uma cópia de um módulo publicado.</p>}</section>
    <section className={styles.panel}><h2>Módulos da comunidade</h2><div className={styles.filters}><input aria-label="Buscar módulos" placeholder="Buscar" value={search} onChange={e => setSearch(e.target.value)} /><input aria-label="Filtrar por assunto" placeholder="Assunto" value={subject} onChange={e => setSubject(e.target.value)} /></div>
      {items.length ? items.map(item => <article className={styles.card} key={item.id}><div><h3>{item.title}</h3><p className={styles.description}>{item.description}</p><p>{item.subject} · {item.activeConceptCount} conceitos · {item.downloadCount} downloads</p><small>{item.authorName}{item.authorInstitution ? ` · ${item.authorInstitution}` : ''}</small><div>{item.crosswordEnabled && <span className={styles.chip}>Cruzadinha</span>}{item.allowAdvanceReview && <span className={styles.chip}>Adiantar revisão</span>}{item.alreadyDownloaded && <span className={styles.chip}>Você já baixou</span>}</div></div><button disabled={!!busy} onClick={() => void download(item)}>{busy === item.id ? 'Baixando…' : item.alreadyDownloaded ? 'Baixar novamente' : 'Baixar'}</button></article>) : <p>Nenhum módulo encontrado.</p>}
      {total > items.length && <button onClick={() => void load(page + 1)}>Carregar mais</button>}
    </section>
  </main>
}
