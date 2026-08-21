import { Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { RequireAuth } from './auth/RequireAuth'
import { ToastProvider } from './components/Toast'
import { Layout } from './components/Layout'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { ProductsPage } from './pages/ProductsPage'
import { ProductFormPage } from './pages/ProductFormPage'
import { ForbiddenPage } from './pages/ForbiddenPage'
import { NotFoundPage } from './pages/NotFoundPage'

export default function App() {
  return (
    <AuthProvider>
      <ToastProvider>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/forbidden" element={<ForbiddenPage />} />

          <Route
            element={
              <RequireAuth>
                <Layout />
              </RequireAuth>
            }
          >
            <Route path="/products" element={<ProductsPage />} />
            <Route
              path="/products/new"
              element={
                <RequireAuth roles={['Manager', 'Admin']}>
                  <ProductFormPage />
                </RequireAuth>
              }
            />
            <Route
              path="/products/:id/edit"
              element={
                <RequireAuth roles={['Manager', 'Admin']}>
                  <ProductFormPage />
                </RequireAuth>
              }
            />
          </Route>

          <Route path="/" element={<Navigate to="/products" replace />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </ToastProvider>
    </AuthProvider>
  )
}
