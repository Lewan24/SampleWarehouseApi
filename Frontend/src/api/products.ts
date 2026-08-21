import { client } from './client'
import type { CreateProductRequest, PagedResult, Product, UpdateProductRequest } from './types'

export interface ListProductsParams {
  page?: number
  pageSize?: number
  search?: string
}

export async function listProducts(params: ListProductsParams = {}): Promise<PagedResult<Product>> {
  const { data } = await client.get<PagedResult<Product>>('/api/products', { params })
  return data
}

export async function getProduct(id: string): Promise<Product> {
  const { data } = await client.get<Product>(`/api/products/${id}`)
  return data
}

export async function createProduct(payload: CreateProductRequest): Promise<Product> {
  const { data } = await client.post<Product>('/api/products', payload)
  return data
}

export async function updateProduct(id: string, payload: UpdateProductRequest): Promise<Product> {
  const { data } = await client.put<Product>(`/api/products/${id}`, payload)
  return data
}

export async function deleteProduct(id: string): Promise<void> {
  await client.delete(`/api/products/${id}`)
}
