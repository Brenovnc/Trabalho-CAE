const iconStyle = { display: 'block', overflow: 'visible' } as const

export function ModulesMetricIcon() {
  return <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" width={20} height={20} fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <path d="M6 3.5h8l4 4V20H6z" />
    <path d="M14 3.5v4h4M9 11h6M9 14.5h6M9 18h4" />
  </svg>
}

export function ClassroomsMetricIcon() {
  return <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" width={20} height={20} fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <circle cx="8" cy="7" r="2.5" />
    <path d="M2.75 20v-1.5a5.25 5.25 0 0 1 10.5 0V20" />
    <circle cx="16.5" cy="8" r="2" />
    <path d="M14.5 14.5a4.25 4.25 0 0 1 6.75 3.4V20h-3" />
  </svg>
}

export function ActiveStudentsMetricIcon() {
  return <svg aria-hidden="true" focusable="false" viewBox="0 0 24 24" width={20} height={20} fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round" style={iconStyle}>
    <circle cx="8" cy="7" r="2.5" />
    <path d="M2.75 20v-1.25a5.25 5.25 0 0 1 10.5 0V20" />
    <path d="m16 10.5 1.75 1.75L21 8.75" />
  </svg>
}
