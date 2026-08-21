import { Link } from 'react-router-dom'

export function ForbiddenPage() {
  return (
    <div className="py-16 text-center">
      <p className="tag-shape mx-auto inline-flex bg-danger-soft py-1 pr-3 text-xs font-semibold uppercase tracking-wide text-danger-ink">
        Restricted
      </p>
      <h1 className="mt-4 font-display text-2xl font-semibold text-ink">You don't have access to this page</h1>
      <p className="mt-2 text-sm text-ink-soft">
        Your role doesn't include this action. Ask an admin if you think this is wrong.
      </p>
      <Link
        to="/products"
        className="mt-6 inline-block rounded-md bg-signal px-4 py-2 text-sm font-semibold text-slate hover:bg-signal-strong"
      >
        Back to inventory
      </Link>
    </div>
  )
}
