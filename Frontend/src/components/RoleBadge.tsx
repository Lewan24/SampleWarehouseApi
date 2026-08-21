import type { Role } from '../api/types'

const STYLES: Record<Role, string> = {
  Admin: 'bg-signal-soft text-signal-ink',
  Manager: 'bg-success-soft text-success-ink',
  Viewer: 'bg-slate-line/60 text-slate-ink',
}

/** The die-cut "shelf tag" motif, sized down as a compact role badge. */
export function RoleBadge({ role }: { role: Role }) {
  return (
    <span
      className={`tag-shape inline-flex items-center py-0.5 pr-2.5 text-[11px] font-semibold uppercase tracking-wide ${STYLES[role]}`}
    >
      {role}
    </span>
  )
}
