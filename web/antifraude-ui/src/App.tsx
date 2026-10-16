import { Navigate, Route, Routes } from 'react-router-dom';
import { RequireAuth } from './auth/AuthContext';
import { Layout } from './components/Layout';
import { AnalysesPage } from './pages/AnalysesPage';
import { AnalysisDetailPage } from './pages/AnalysisDetailPage';
import { LoginPage } from './pages/LoginPage';
import { SimulatePage } from './pages/SimulatePage';

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route
        element={
          <RequireAuth>
            <Layout />
          </RequireAuth>
        }
      >
        <Route index element={<AnalysesPage />} />
        <Route path="analises/:id" element={<AnalysisDetailPage />} />
        <Route path="simular" element={<SimulatePage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
