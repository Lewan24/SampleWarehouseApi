import axios from 'axios'
import type { ProblemDetails } from '../api/types'

/** Turns an axios error (ProblemDetails / ValidationProblem shape) into one readable string. */
export function extractErrorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (!axios.isAxiosError(error)) return fallback

  if (error.response?.status === 401) {
    return 'Invalid email or password.'
  }

  const data = error.response?.data as ProblemDetails | { error?: string } | undefined
  if (!data) return fallback

  if ('errors' in data && data.errors) {
    const firstField = Object.values(data.errors)[0]
    if (firstField?.[0]) return firstField[0]
  }

  if ('error' in data && data.error) return data.error
  if ('detail' in data && data.detail) return data.detail
  if ('title' in data && data.title) return data.title

  return fallback
}
