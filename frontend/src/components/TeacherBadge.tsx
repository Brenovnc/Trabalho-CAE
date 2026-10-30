import { apiUrl } from '../services/authApi'
import styles from './TeacherBadge.module.css'

type Props = { moduleId: string; name: string | null | undefined; institution: string | null | undefined; photoVersion: number | null | undefined; size: number }
export function TeacherBadge({ moduleId, name, institution, photoVersion, size }: Props) {
  const displayName = name?.trim() || 'Professor(a)'
  const photo = photoVersion !== null && photoVersion !== undefined
    ? apiUrl('/api/student/modules/' + moduleId + '/teacher-photo') + '?v=' + photoVersion
    : null
  return <div className={styles.badge}>
    <span className={styles.avatar} style={{ width: size, height: size, fontSize: `${Math.max(16, size * .34)}px` }}>
      {photo ? <img src={photo} alt="" /> : displayName[0].toLocaleUpperCase('pt-BR')}
    </span>
    <span className={styles.details}><small>Professor(a) responsável</small><strong>{displayName}</strong>{institution?.trim() && <span>{institution}</span>}</span>
  </div>
}
