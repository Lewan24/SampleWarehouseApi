import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="flex min-h-dvh flex-col items-center justify-center bg-paper px-4 text-center">
      <p className="font-display text-5xl font-semibold text-ink">404</p>
      <p className="mt-2 text-sm text-ink-soft">This page doesn't exist.</p>
      <Link
        to="/products"
        className="mt-6 inline-block rounded-md bg-signal px-4 py-2 text-sm font-semibold text-slate hover:bg-signal-strong"
      >
        Back to inventory
      </Link>
    </div>
  )
}
