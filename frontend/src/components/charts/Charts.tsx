import type { CSSProperties, ReactNode } from 'react'
import styles from './Charts.module.css'

export type ChartSeries = { label: string; values: (number | null)[]; color: string; marker?: 'circle' | 'square' | 'diamond' }
type LineChartProps = { title: string; labels: string[]; series: ChartSeries[]; hasData?: boolean; emptyText?: string; percent?: boolean; size?: 'large' }
type BarChartProps = { title: string; labels: string[]; values: number[]; hasData?: boolean; emptyText?: string; suffix?: string; size?: 'large' }
type StackedBarChartProps = { title: string; values: { label: string; value: number; color: string }[]; hasData?: boolean; emptyText?: string; size?: 'large' }

export function LineChart({ title, labels, series, hasData = series.some(item => item.values.some(value => value !== null)), emptyText, percent = false, size }: LineChartProps) {
  if (!hasData) return <ChartFrame title={title} size={size}><p className={styles.empty}>{emptyText ?? 'Ainda não há atividades neste período'}</p></ChartFrame>
  const values = series.flatMap(item => item.values.filter((value): value is number => value !== null))
  const max = Math.max(1, ...values)
  const min = Math.min(0, ...values)
  const points = (items: (number | null)[]) => items.map((value, index) => value === null ? null : ({
    x: 48 + index * (504 / Math.max(1, labels.length - 1)),
    y: 202 - ((value - min) / Math.max(1, max - min)) * 164,
    value,
  }))
  const tickIndexes = labels.map((_, index) => index).filter(index => index === 0 || index === labels.length - 1 || index % Math.ceil(labels.length / 5) === 0)
  return <ChartFrame title={title} size={size}>
    <svg className={styles.svg} viewBox="0 0 600 250" role="img" aria-label={title}>
      <line x1="46" y1="38" x2="46" y2="204" /><line x1="46" y1="204" x2="554" y2="204" />
      <text x="48" y="22">{percent ? 'Acerto (%)' : 'Atividades'}</text>
      {series.map(item => {
        const plotted = points(item.values)
        let drawing = false
        const path = plotted.map(point => {
          if (!point) { drawing = false; return '' }
          const command = drawing ? 'L ' : 'M '
          drawing = true
          return command + point.x + ' ' + point.y
        }).filter(Boolean).join(' ')
        return <g key={item.label} className={styles.series}>
          <path d={path} stroke={item.color} fill="none" />
          {plotted.map((point, index) => point && <g key={labels[index]} className={styles.point}>
            {item.marker === 'square' ? <rect x={point.x - 3.5} y={point.y - 3.5} width="7" height="7" fill={item.color} /> : item.marker === 'diamond'
              ? <polygon points={point.x + ',' + (point.y - 4) + ' ' + (point.x + 4) + ',' + point.y + ' ' + point.x + ',' + (point.y + 4) + ' ' + (point.x - 4) + ',' + point.y} fill={item.color} />
              : <circle cx={point.x} cy={point.y} r="4" fill={item.color} />}
            <title>{labels[index]}: {point.value}{percent ? '%' : ''}</title>
          </g>)}
        </g>
      })}
      {tickIndexes.map(index => <text key={labels[index] + index} x={48 + index * (504 / Math.max(1, labels.length - 1))} y="230">{labels[index]}</text>)}
    </svg>
    <Legend labels={series.map(item => ({ label: item.label, color: item.color, marker: item.marker }))} />
    <table className={styles.srOnly}><caption>{title}</caption><thead><tr><th>Data</th>{series.map(item => <th key={item.label}>{item.label}</th>)}</tr></thead>
      <tbody>{labels.map((label, index) => <tr key={label}><th>{label}</th>{series.map(item => <td key={item.label}>{item.values[index] ?? 'Sem dados'}</td>)}</tr>)}</tbody></table>
  </ChartFrame>
}

export function BarChart({ title, labels, values, hasData = values.some(value => value > 0), emptyText, suffix = '', size }: BarChartProps) {
  if (!hasData) return <ChartFrame title={title} size={size}><p className={styles.empty}>{emptyText ?? 'Ainda não há atividades neste período'}</p></ChartFrame>
  const max = Math.max(1, ...values)
  return <ChartFrame title={title} size={size}>
    <svg className={styles.svg} viewBox="0 0 600 250" role="img" aria-label={title}>
      <line x1="46" y1="38" x2="46" y2="204" /><line x1="46" y1="204" x2="554" y2="204" />
      {values.map((value, index) => {
        const step = 496 / Math.max(1, values.length)
        const width = Math.min(64, step * .55)
        const x = 52 + index * step + (step - width) / 2
        const height = value / max * 156
        return <g key={labels[index]}><rect x={x} y={204 - height} width={width} height={height} rx="3" />
          <text x={x + width / 2} y={Math.max(33, 199 - height)} textAnchor="middle">{value}{suffix}</text>
          <text x={x + width / 2} y="226" textAnchor="middle">{labels[index]}</text></g>
      })}
    </svg>
    <table className={styles.srOnly}><caption>{title}</caption><thead><tr><th>Categoria</th><th>Valor</th></tr></thead><tbody>{labels.map((label,index)=><tr key={label}><th>{label}</th><td>{values[index]}{suffix}</td></tr>)}</tbody></table>
  </ChartFrame>
}

export function StackedBarChart({ title, values, hasData = values.some(item => item.value > 0), emptyText, size }: StackedBarChartProps) {
  const total = values.reduce((sum, item) => sum + item.value, 0)
  if (!hasData || total === 0) return <ChartFrame title={title} size={size}><p className={styles.empty}>{emptyText ?? 'Ainda não há dados neste período'}</p></ChartFrame>
  let offset = 0
  return <ChartFrame title={title} size={size}>
    <svg className={styles.stackedSvg} viewBox="0 0 600 135" role="img" aria-label={title}>
      <text x="20" y="22">{title}</text>
      {values.map(item => {
        const width = 560 * item.value / total
        const x = 20 + offset
        offset += width
        return <g key={item.label}><rect x={x} y="42" width={width} height="42" fill={item.color} />
          {width > 48 && <text x={x + width / 2} y="68" textAnchor="middle">{item.value}</text>}</g>
      })}
    </svg>
    <Legend labels={values.map(item => ({ label: item.label + ' (' + item.value + ')', color: item.color }))} />
    <table className={styles.srOnly}><caption>{title}</caption><thead><tr><th>Estado</th><th>Conceitos</th></tr></thead><tbody>{values.map(item=><tr key={item.label}><th>{item.label}</th><td>{item.value}</td></tr>)}</tbody></table>
  </ChartFrame>
}

function ChartFrame({ title, children, size }: { title: string; children: ReactNode; size?: 'large' }) {
  return <section className={`${styles.card} ${size === 'large' ? styles.large : ''}`} aria-label={title}><h3>{title}</h3>{children}</section>
}
function Legend({ labels }: { labels: { label: string; color: string; marker?: 'circle' | 'square' | 'diamond' }[] }) {
  return <ul className={styles.legend}>{labels.map(item=><li key={item.label}>
    <span className={styles.swatch} data-shape={item.marker ?? 'square'} style={{ '--swatch': item.color } as CSSProperties} aria-hidden="true" />{item.label}
  </li>)}</ul>
}
