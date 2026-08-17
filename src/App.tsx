import { BrowserRouter, HashRouter, Navigate, Route, Routes } from 'react-router-dom';

import MobileApp from '@/mobile/MobileApp';
import WebApp from '@/web/WebApp';
import WorkspaceChooser from '@/shared/WorkspaceChooser';
import ApiTestPage from '@/modules/console/pages/ApiTestPage';
import SetupPage from '@/modules/console/pages/SetupPage';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { DbStatusBanner } from '@/oas/components/DbStatusBanner';



// file:// (Electron desktop build) has no history server — use hash routing there.
const Router =
  typeof window !== 'undefined' && window.location.protocol === 'file:' ? HashRouter : BrowserRouter;

export default function App() {
  return (
    <ThemeProvider>
      <I18nProvider>
        <DbStatusBanner />
        <Router>
          <Routes>
            <Route path="/" element={<WorkspaceChooser />} />
            <Route path="/mobile/*" element={<MobileApp />} />
            <Route path="/web/*" element={<WebApp />} />
            {/* Standalone, outside both shells on purpose — auto-runs a full backend smoke test on load. */}
            <Route path="/test" element={<ApiTestPage />} />
            {/* Hidden ops utility, never linked from any nav — bootstrap the first admin, then create operators/supervisors without the normal login->console->Users navigation. */}
            <Route path="/setup" element={<SetupPage />} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </Router>
      </I18nProvider>
    </ThemeProvider>
  );
}

