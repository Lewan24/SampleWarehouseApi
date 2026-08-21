import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import * as productsApi from '../api/products'
import type { Product } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { useToast } from '../components/Toast'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { Spinner } from '../components/Spinner'
import { extractErrorMessage } from '../lib/apiError'

const PAGE_SIZE = 10

const currency = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function ProductsPage() {
  const { hasRole } = useAuth()
  const { showToast } = useToast()

  const [items, setItems] = useState<Product[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [pendingDelete, setPendingDelete] = useState<Product | null>(null)
  const [isDeleting, setIsDeleting] = useState(false)

  const canManage = hasRole('Manager', 'Admin')
  const canDelete = hasRole('Admin')

  const load = useCallback(async () => {
    setIsLoading(true)
    setError(null)
    try {
      const result = await productsApi.listProducts({ page, pageSize: PAGE_SIZE, search: search || undefined })
      setItems(result.items)
      setTotalCount(result.totalCount)
    } catch (err) {
      setError(extractErrorMessage(err, 'Could not load products.'))
    } finally {
      setIsLoading(false)
    }
  }, [page, search])

  useEffect(() => {
    load()
  }, [load])

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setPage(1)
    load()
  }

  const confirmDelete = async () => {
    if (!pendingDelete) return
    setIsDeleting(true)
    try {
      await productsApi.deleteProduct(pendingDelete.id)
      showToast(`${pendingDelete.name} removed from inventory.`)
      setPendingDelete(null)
      await load()
    } catch (err) {
      showToast(extractErrorMessage(err, 'Could not delete this item.'), 'error')
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <div>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-semibold text-ink">Inventory</h1>
          <p className="mt-1 text-sm text-ink-soft">
            {totalCount} item{totalCount === 1 ? '' : 's'} in stock
          </p>
        </div>

        <div className="flex items-center gap-3">
          <form onSubmit={handleSearchSubmit} className="flex items-center gap-2">
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Search name or SKU…"
              className="w-56 rounded-md border border-line bg-surface px-3 py-1.5 text-sm text-ink placeholder:text-ink-soft focus:border-signal"
            />
          </form>

          {canManage && (
            <Link
              to="/products/new"
              className="rounded-md bg-signal px-3.5 py-1.5 text-sm font-semibold text-slate hover:bg-signal-strong"
            >
              Add item
            </Link>
          )}
        </div>
      </div>

      <div className="mt-6 overflow-hidden rounded-lg border border-line bg-surface">
        {isLoading ? (
          <div className="flex items-center justify-center py-16">
            <Spinner className="h-5 w-5 text-signal" />
          </div>
        ) : error ? (
          <p role="alert" className="px-6 py-10 text-center text-sm text-danger-ink">
            {error}
          </p>
        ) : items.length === 0 ? (
          <div className="px-6 py-16 text-center">
            <p className="font-display text-base font-semibold text-ink">No items match this view</p>
            <p className="mt-1 text-sm text-ink-soft">
              {search ? 'Try a different search term.' : 'Add the first item to get started.'}
            </p>
          </div>
        ) : (
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="border-b border-line bg-paper text-xs font-semibold uppercase tracking-wide text-ink-soft">
                <th className="px-4 py-3">Name</th>
                <th className="px-4 py-3">SKU</th>
                <th className="px-4 py-3">Category</th>
                <th className="px-4 py-3 text-right">Quantity</th>
                <th className="px-4 py-3 text-right">Price</th>
                {canManage && <th className="px-4 py-3 text-right">Actions</th>}
              </tr>
            </thead>
            <tbody>
              {items.map((product) => (
                <tr key={product.id} className="border-b border-line last:border-0 hover:bg-paper/60">
                  <td className="px-4 py-3 font-medium text-ink">{product.name}</td>
                  <td className="px-4 py-3">
                    <span className="rounded bg-paper px-1.5 py-0.5 font-data text-xs text-ink-soft">
                      {product.sku}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-ink-soft">{product.category}</td>
                  <td className="px-4 py-3 text-right font-data text-ink">{product.quantity}</td>
                  <td className="px-4 py-3 text-right font-data text-ink">{currency.format(product.price)}</td>
                  {canManage && (
                    <td className="px-4 py-3 text-right">
                      <div className="flex justify-end gap-3">
                        <Link
                          to={`/products/${product.id}/edit`}
                          className="text-sm font-medium text-signal-ink hover:text-signal-strong"
                        >
                          Edit
                        </Link>
                        {canDelete && (
                          <button
                            type="button"
                            onClick={() => setPendingDelete(product)}
                            className="text-sm font-medium text-danger hover:text-danger-ink"
                          >
                            Delete
                          </button>
                        )}
                      </div>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {!isLoading && !error && items.length > 0 && (
        <div className="mt-4 flex items-center justify-between text-sm text-ink-soft">
          <span>
            Page {page} of {totalPages}
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage((p) => p - 1)}
              className="rounded-md border border-line px-3 py-1.5 font-medium text-ink hover:bg-surface disabled:opacity-40"
            >
              Previous
            </button>
            <button
              type="button"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => p + 1)}
              className="rounded-md border border-line px-3 py-1.5 font-medium text-ink hover:bg-surface disabled:opacity-40"
            >
              Next
            </button>
          </div>
        </div>
      )}

      <ConfirmDialog
        open={!!pendingDelete}
        title="Remove this item?"
        description={`"${pendingDelete?.name}" will be permanently removed from inventory. This can't be undone.`}
        confirmLabel="Delete"
        danger
        busy={isDeleting}
        onConfirm={confirmDelete}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  )
}
