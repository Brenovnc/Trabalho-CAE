import { useId, type ReactNode } from 'react'
import type { StatisticsFilters as FilterValues } from '../../services/teacherProgressApi'
import styles from './StatisticsFilters.module.css'

export type FilterOption = { id: string; label: string }
type Props = {
  value: FilterValues; onChange: (value: FilterValues) => void
  classrooms?: FilterOption[]; modules?: FilterOption[]
  showClassroom?: boolean; showModule?: boolean; showActivityType?: boolean; showMinAttempts?: boolean
  disablePeriod?: boolean; disableActivityType?: boolean; disableMinAttempts?: boolean
}
const today = () => new Date().toISOString().slice(0, 10)
const shiftDate = (value: string, amount: number) => { const date = new Date(value + 'T00:00:00Z'); date.setUTCDate(date.getUTCDate() + amount); return date.toISOString().slice(0, 10) }
export const defaultStatisticsFilters: FilterValues = { days: 30 }
export function statisticsFiltersReady(value: FilterValues) { return !value.custom || Boolean(value.from && value.to && value.from <= value.to && (new Date(value.to + 'T00:00:00Z').getTime() - new Date(value.from + 'T00:00:00Z').getTime()) / 86400000 < 180 && value.to <= today()) }
export function statisticsFilterSummary(value: FilterValues, classrooms: FilterOption[] = [], modules: FilterOption[] = []) {
  const parts = value.custom ? [value.from && value.to ? value.from + ' a ' + value.to : 'Período personalizado incompleto'] : ['Últimos ' + (value.days ?? 30) + ' dias']
  if (value.classroomId) parts.push(classrooms.find(item => item.id === value.classroomId)?.label ?? 'Turma selecionada')
  if (value.moduleId) parts.push(modules.find(item => item.id === value.moduleId)?.label ?? 'Módulo selecionado')
  if (value.activityType) parts.push(({ TRUE_FALSE: 'Verdadeiro ou falso', FILL_BLANK: 'Completar lacunas', GUESS_CONCEPT: 'Jogo das pistas', ORDERING: 'Ordenação' } as Record<string, string>)[value.activityType] ?? value.activityType)
  if (value.minAttempts) parts.push('Mínimo de ' + value.minAttempts + ' tentativas')
  return parts.join(' · ')
}

function FilterField({ id, label, disabled, children }: { id: string; label: string; disabled?: boolean; children: (descriptionId?: string) => ReactNode }) {
  const generatedId = useId().replaceAll(':', '')
  const descriptionId = disabled ? `${generatedId}-${id}-not-applicable` : undefined
  return <label className={styles.field} data-disabled={disabled ? 'true' : undefined}>
    <span className={styles.fieldLabel}>{label}{disabled && <span className={styles.unavailable}>Indisponível</span>}</span>
    {children(descriptionId)}
    <small className={styles.filterHelp} data-visible={disabled ? 'true' : 'false'} id={descriptionId}>Não se aplica a este gráfico</small>
  </label>
}

export function StatisticsFilters({ value, onChange, classrooms = [], modules = [], showClassroom, showModule, showActivityType, showMinAttempts, disablePeriod, disableActivityType, disableMinAttempts }: Props) {
  const maxToday = today()
  function update(partial: Partial<FilterValues>) { onChange({ ...value, ...partial }) }
  const fromMin = value.to ? shiftDate(value.to, -179) : shiftDate(maxToday, -179)
  const toMax = value.from ? [maxToday, shiftDate(value.from, 179)].sort()[0] : maxToday
  return <div className={styles.filters}>
    <FilterField id="period" label="Período" disabled={disablePeriod}>{descriptionId => <select disabled={disablePeriod} aria-describedby={descriptionId} value={value.custom ? 'custom' : value.days ?? 30} onChange={event => event.target.value === 'custom'
      ? update({ custom: true, days: undefined, from: '', to: '' })
      : update({ custom: false, days: Number(event.target.value), from: undefined, to: undefined })}>
      {[7, 30, 90, 180].map(days => <option key={days} value={days}>{days} dias</option>)}<option value="custom">Personalizado</option>
    </select>}</FilterField>
    {value.custom && <><FilterField id="from" label="De" disabled={disablePeriod}>{descriptionId => <input disabled={disablePeriod} aria-describedby={descriptionId} type="date" min={fromMin} max={value.to || maxToday} value={value.from ?? ''} onChange={event => update({ from: event.target.value || undefined })} />}</FilterField>
      <FilterField id="to" label="Até" disabled={disablePeriod}>{descriptionId => <input disabled={disablePeriod} aria-describedby={descriptionId} type="date" min={value.from || fromMin} max={toMax} value={value.to ?? ''} onChange={event => update({ to: event.target.value || undefined })} />}</FilterField></>}
    {showClassroom && <FilterField id="classroom" label="Turma">{() => <select value={value.classroomId ?? ''} onChange={event => update({ classroomId: event.target.value || undefined })}><option value="">Todas as turmas</option>{classrooms.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}</select>}</FilterField>}
    {showModule && <FilterField id="module" label="Módulo">{() => <select value={value.moduleId ?? ''} onChange={event => update({ moduleId: event.target.value || undefined })}><option value="">Todos os módulos</option>{modules.map(item => <option key={item.id} value={item.id}>{item.label}</option>)}</select>}</FilterField>}
    {showActivityType && <FilterField id="activity-type" label="Tipo de atividade" disabled={disableActivityType}>{descriptionId => <select disabled={disableActivityType} aria-describedby={descriptionId} value={value.activityType ?? ''} onChange={event => update({ activityType: event.target.value || undefined })}><option value="">Todos os tipos</option><option value="TRUE_FALSE">Verdadeiro ou falso</option><option value="FILL_BLANK">Completar lacunas</option><option value="GUESS_CONCEPT">Jogo das pistas</option><option value="ORDERING">Ordenação</option></select>}</FilterField>}
    {showMinAttempts && <FilterField id="min-attempts" label="Mínimo de tentativas por conceito" disabled={disableMinAttempts}>{descriptionId => <select disabled={disableMinAttempts} aria-describedby={descriptionId} value={value.minAttempts ?? 3} onChange={event => update({ minAttempts: Number(event.target.value) })}><option value={3}>3</option><option value={5}>5</option><option value={10}>10</option></select>}</FilterField>}
    <button type="button" className={styles.clear} onClick={() => onChange({ ...defaultStatisticsFilters, ...(showMinAttempts ? { minAttempts: 3 } : {}) })}>Limpar filtros</button>
  </div>
}
