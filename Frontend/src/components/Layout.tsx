import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { RoleBadge } from './RoleBadge'

const navItems = [
  { to: '/products', label: 'Inventory' },
]

export function Layout() {
  const { user, logout, hasRole } = useAuth()
  const navigate = useNavigate()

  const handleLogout = async () => {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="flex min-h-dvh">
      <aside className="flex w-60 shrink-0 flex-col bg-slate text-slate-ink">
        <div className="flex items-center gap-2.5 px-5 py-6">
          <span className="tag-shape flex h-7 items-center bg-signal pr-3 text-sm font-bold text-slate">
            WH
          </span>
          <span className="font-display text-base font-semibold text-white">Warehouse</span>
        </div>

        <nav className="mt-2 flex flex-1 flex-col gap-1 px-3">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-slate-soft text-white'
                    : 'text-slate-ink-soft hover:bg-slate-soft hover:text-white'
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
          {hasRole('Manager', 'Admin') && (
            <NavLink
              to="/products/new"
              className={({ isActive }) =>
                `rounded-md px-3 py-2 text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-slate-soft text-white'
                    : 'text-slate-ink-soft hover:bg-slate-soft hover:text-white'
                }`
              }
            >
              Add stock item
            </NavLink>
          )}
        </nav>

        <div className="border-t border-slate-line px-3 py-4">
          <button
            type="button"
            onClick={handleLogout}
            className="w-full rounded-md px-3 py-2 text-left text-sm font-medium text-slate-ink-soft transition-colors hover:bg-slate-soft hover:text-white"
          >
            Sign out
          </button>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between border-b border-line bg-surface px-6 py-3.5">
          <div className="text-sm text-ink-soft">
            Signed in as <span className="font-medium text-ink">{user?.email}</span>
          </div>
          {user && user.roles[0] && <RoleBadge role={user.roles[0]} />}
        </header>

        <main className="flex-1 overflow-y-auto px-6 py-8">
          <div className="mx-auto max-w-5xl">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
