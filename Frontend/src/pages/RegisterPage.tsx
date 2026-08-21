import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { extractErrorMessage } from '../lib/apiError'
import { Spinner } from '../components/Spinner'

export function RegisterPage() {
  const { register, login } = useAuth()
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)
    try {
      await register({ email, password, confirmPassword })
      // New accounts start as Viewer — log them straight in rather than making them
      // re-type credentials they just typed.
      await login({ email, password })
      navigate('/products', { replace: true })
    } catch (err) {
      setError(extractErrorMessage(err, 'Could not create the account. Check the details and try again.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-dvh items-center justify-center bg-slate px-4">
      <div className="w-full max-w-sm">
        <div className="mb-8 flex items-center justify-center gap-3">
          <span className="tag-shape flex h-9 items-center bg-signal pr-3.5 text-base font-bold text-slate">
            WH
          </span>
          <span className="font-display text-xl font-semibold text-white">Warehouse</span>
        </div>

        <div className="rounded-xl border border-slate-line bg-slate-soft p-7 shadow-2xl">
          <h1 className="font-display text-lg font-semibold text-white">Create an account</h1>
          <p className="mt-1 text-sm text-slate-ink-soft">
            New accounts start with view-only access.
          </p>

          <form onSubmit={handleSubmit} className="mt-6 space-y-4" noValidate>
            <div>
              <label htmlFor="email" className="block text-sm font-medium text-slate-ink">
                Email
              </label>
              <input
                id="email"
                type="email"
                autoComplete="username"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="mt-1.5 w-full rounded-md border border-slate-line bg-slate px-3 py-2 text-sm text-white placeholder:text-slate-ink-soft focus:border-signal"
                placeholder="you@example.com"
              />
            </div>

            <div>
              <label htmlFor="password" className="block text-sm font-medium text-slate-ink">
                Password
              </label>
              <input
                id="password"
                type="password"
                autoComplete="new-password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className="mt-1.5 w-full rounded-md border border-slate-line bg-slate px-3 py-2 text-sm text-white focus:border-signal"
              />
              <p className="mt-1 text-xs text-slate-ink-soft">
                At least 12 characters, with upper, lower, a digit, and a symbol.
              </p>
            </div>

            <div>
              <label htmlFor="confirmPassword" className="block text-sm font-medium text-slate-ink">
                Confirm password
              </label>
              <input
                id="confirmPassword"
                type="password"
                autoComplete="new-password"
                required
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                className="mt-1.5 w-full rounded-md border border-slate-line bg-slate px-3 py-2 text-sm text-white focus:border-signal"
              />
            </div>

            {error && (
              <p role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-sm text-danger-ink">
                {error}
              </p>
            )}

            <button
              type="submit"
              disabled={isSubmitting}
              className="flex w-full items-center justify-center gap-2 rounded-md bg-signal px-3 py-2.5 text-sm font-semibold text-slate transition-colors hover:bg-signal-strong disabled:opacity-60"
            >
              {isSubmitting && <Spinner className="h-4 w-4" />}
              Create account
            </button>
          </form>
        </div>

        <p className="mt-5 text-center text-sm text-slate-ink-soft">
          Already have an account?{' '}
          <Link to="/login" className="font-medium text-signal hover:text-signal-strong">
            Sign in
          </Link>
        </p>
      </div>
    </div>
  )
}
