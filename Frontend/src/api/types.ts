export type Role = 'Viewer' | 'Manager' | 'Admin'

export interface AuthResponse {
  accessToken: string
  expiresAtUtc: string
}

export interface RegisteredUser {
  id: string
  email: string
}

export interface Product {
  id: string
  name: string
  sku: string
  category: string
  quantity: number
  price: number
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface CreateProductRequest {
  name: string
  sku: string
  category: string
  quantity: number
  price: number
}

export type UpdateProductRequest = Omit<CreateProductRequest, 'sku'>

/** Shape the API's ProblemDetails / ValidationProblem responses take. */
export interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
  errors?: Record<string, string[]>
}
