import { useEffect, useState } from 'react'
import styles from './SessionTimer.module.css'

export function formatElapsed(startedAtUtc: string, nowMs: number) {
  const seconds = Math.max(0, Math.floor((nowMs - Date.parse(startedAtUtc)) / 1000))
  const minutes = Math.floor(seconds / 60).toString().padStart(2, '0')
  return `${minutes}:${(seconds % 60).toString().padStart(2, '0')}`
}

type Props = { startedAtUtc: string }
export function SessionTimer({ startedAtUtc }: Props) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])
  return <p className={styles.timer} aria-label={`Tempo nesta atividade: ${formatElapsed(startedAtUtc, now)}`}><span aria-hidden="true">{formatElapsed(startedAtUtc, now)}</span></p>
}
