import { useId, useRef, type KeyboardEvent, type ReactNode } from 'react'
import styles from './ChartTabsCard.module.css'

export type ChartTab = { id: string; label: string; render: ReactNode; summary?: ReactNode }
type Props = {
  tabs: ChartTab[]
  activeId: string
  onChange: (id: string) => void
  label: string
  loading?: boolean
  empty?: boolean
  emptyMessage?: string
}

export function ChartTabsCard({ tabs, activeId, onChange, label, loading = false, empty = false, emptyMessage = 'Ainda não há dados para exibir.' }: Props) {
  const generatedId = useId().replaceAll(':', '')
  const tabRefs = useRef(new Map<string, HTMLButtonElement>())
  const active = tabs.find(tab => tab.id === activeId) ?? tabs[0]
  if (!active) return null
  const tabId = (id: string) => `chart-${generatedId}-tab-${id}`
  const panelId = `chart-${generatedId}-panel`
  function handleKeyDown(event: KeyboardEvent<HTMLButtonElement>, currentIndex: number) {
    let nextIndex: number | null = null
    if (event.key === 'ArrowRight') nextIndex = (currentIndex + 1) % tabs.length
    else if (event.key === 'ArrowLeft') nextIndex = (currentIndex - 1 + tabs.length) % tabs.length
    else if (event.key === 'Home') nextIndex = 0
    else if (event.key === 'End') nextIndex = tabs.length - 1
    if (nextIndex === null) return
    event.preventDefault()
    const next = tabs[nextIndex]
    onChange(next.id)
    tabRefs.current.get(next.id)?.focus()
  }
  return <section className={styles.card}>
    <div className={styles.tabList} role="tablist" aria-label={label}>
      {tabs.map((tab, index) => <button
        key={tab.id}
        ref={element => { if (element) tabRefs.current.set(tab.id, element); else tabRefs.current.delete(tab.id) }}
        id={tabId(tab.id)}
        type="button"
        role="tab"
        aria-selected={active.id === tab.id}
        aria-controls={panelId}
        tabIndex={active.id === tab.id ? 0 : -1}
        onClick={() => onChange(tab.id)}
        onKeyDown={event => handleKeyDown(event, index)}
      >{tab.label}</button>)}
    </div>
    <div className={styles.panel} id={panelId} role="tabpanel" aria-labelledby={tabId(active.id)} tabIndex={0}>
      {loading ? <p role="status">Carregando gráfico…</p> : empty ? <p className={styles.empty}>{emptyMessage}</p> : <>
        <div className={styles.plot}>{active.render}</div>
        {active.summary && <p className={styles.summary} aria-live="polite">{active.summary}</p>}
      </>}
    </div>
  </section>
}
