import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import * as productsApi from '../api/products'
import { useToast } from '../components/Toast'
import { Spinner } from '../components/Spinner'
import { extractErrorMessage } from '../lib/apiError'

interface FormState {
  name: string
  sku: string
  category: string
  quantity: string
  price: string
}

const emptyForm: FormState = { name: '', sku: '', category: '', quantity: '0', price: '0' }

export function ProductFormPage() {
  const { id } = useParams<{ id: string }>()
  const isEditing = !!id
  const navigate = useNavigate()
  const { showToast } = useToast()

  const [form, setForm] = useState<FormState>(emptyForm)
  const [isLoading, setIsLoading] = useState(isEditing)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) return
    productsApi
      .getProduct(id)
      .then((product) => {
        setForm({
          name: product.name,
          sku: product.sku,
          category: product.category,
          quantity: String(product.quantity),
          price: String(product.price),
        })
      })
      .catch((err) => setError(extractErrorMessage(err, 'Could not load this item.')))
      .finally(() => setIsLoading(false))
  }, [id])

  const update = (field: keyof FormState) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }))

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)
    setIsSubmitting(true)

    const payload = {
      name: form.name.trim(),
      category: form.category.trim(),
      quantity: Number(form.quantity),
      price: Number(form.price),
    }

    try {
      if (isEditing && id) {
        await productsApi.updateProduct(id, payload)
        showToast(`${payload.name} updated.`)
      } else {
        await productsApi.createProduct({ ...payload, sku: form.sku.trim() })
        showToast(`${payload.name} added to inventory.`)
      }
      navigate('/products')
    } catch (err) {
      setError(extractErrorMessage(err, 'Could not save this item. Check the details and try again.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  if (isLoading) {
    return (
      <div className="flex justify-center py-16">
        <Spinner className="h-5 w-5 text-signal" />
      </div>
    )
  }

  return (
    <div className="max-w-lg">
      <h1 className="font-display text-2xl font-semibold text-ink">
        {isEditing ? 'Edit item' : 'Add item'}
      </h1>
      <p className="mt-1 text-sm text-ink-soft">
        {isEditing ? 'Update the details for this stock item.' : 'Add a new item to the warehouse inventory.'}
      </p>

      <form onSubmit={handleSubmit} className="mt-6 space-y-4 rounded-lg border border-line bg-surface p-6" noValidate>
        <div>
          <label htmlFor="name" className="block text-sm font-medium text-ink">
            Name
          </label>
          <input
            id="name"
            required
            maxLength={150}
            value={form.name}
            onChange={update('name')}
            className="mt-1.5 w-full rounded-md border border-line px-3 py-2 text-sm text-ink focus:border-signal"
          />
        </div>

        <div>
          <label htmlFor="sku" className="block text-sm font-medium text-ink">
            SKU
          </label>
          <input
            id="sku"
            required
            disabled={isEditing}
            maxLength={50}
            pattern="[A-Za-z0-9\-_]+"
            title="Letters, numbers, hyphens and underscores only"
            value={form.sku}
            onChange={update('sku')}
            className="mt-1.5 w-full rounded-md border border-line px-3 py-2 font-data text-sm text-ink focus:border-signal disabled:bg-paper disabled:text-ink-soft"
          />
          {isEditing && <p className="mt-1 text-xs text-ink-soft">SKU can't be changed after creation.</p>}
        </div>

        <div>
          <label htmlFor="category" className="block text-sm font-medium text-ink">
            Category
          </label>
          <input
            id="category"
            required
            maxLength={80}
            value={form.category}
            onChange={update('category')}
            className="mt-1.5 w-full rounded-md border border-line px-3 py-2 text-sm text-ink focus:border-signal"
          />
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <label htmlFor="quantity" className="block text-sm font-medium text-ink">
              Quantity
            </label>
            <input
              id="quantity"
              type="number"
              min={0}
              step={1}
              required
              value={form.quantity}
              onChange={update('quantity')}
              className="mt-1.5 w-full rounded-md border border-line px-3 py-2 font-data text-sm text-ink focus:border-signal"
            />
          </div>
          <div>
            <label htmlFor="price" className="block text-sm font-medium text-ink">
              Price (USD)
            </label>
            <input
              id="price"
              type="number"
              min={0}
              step={0.01}
              required
              value={form.price}
              onChange={update('price')}
              className="mt-1.5 w-full rounded-md border border-line px-3 py-2 font-data text-sm text-ink focus:border-signal"
            />
          </div>
        </div>

        {error && (
          <p role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-sm text-danger-ink">
            {error}
          </p>
        )}

        <div className="flex justify-end gap-3 pt-2">
          <button
            type="button"
            onClick={() => navigate('/products')}
            className="rounded-md border border-line px-4 py-2 text-sm font-medium text-ink hover:bg-paper"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={isSubmitting}
            className="flex items-center gap-2 rounded-md bg-signal px-4 py-2 text-sm font-semibold text-slate hover:bg-signal-strong disabled:opacity-60"
          >
            {isSubmitting && <Spinner className="h-4 w-4" />}
            {isEditing ? 'Save changes' : 'Add item'}
          </button>
        </div>
      </form>
    </div>
  )
}
