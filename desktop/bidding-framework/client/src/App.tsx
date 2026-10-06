import { useEffect, useRef, useState } from 'react';
import AppRouter from './app/AppRouter';
import GpuHardwareAccelerationPrompt from './app/GpuHardwareAccelerationPrompt';
import LicenseStatusPrompt from './app/LicenseStatusPrompt';
import RequiredOnlineServicesPrompt from './app/RequiredOnlineServicesPrompt';
import UpdateNotifier from './app/UpdateNotifier';
import { useTextModelSetupRedirect } from './app/useTextModelSetupRedirect';
import AppShell from './components/AppShell';
import { trackAppOpen, trackConfigUsage, trackPageView } from './shared/analytics/analytics';
import type { SectionId } from './shared/types/navigation';

function isDeveloperSection(section: SectionId) {
  return section.startsWith('developer-');
}

function isManagedWorkbenchSection(section: SectionId) {
  return section === 'technical-plan' || section === 'feasibility-report';
}

function App() {
  const [activeSection, setActiveSection] = useState<SectionId>('technical-plan');
  const [developerMode, setDeveloperMode] = useState(false);
  const leaveGuardRef = useRef<((nextSection?: string) => Promise<boolean>) | null>(null);

  useEffect(() => {
    trackAppOpen();

    void window.yibiao?.config.load()
      .then((config) => {
        setDeveloperMode(Boolean(config?.developer_mode));
        trackConfigUsage({}, config);
      })
      .catch((error) => console.warn('读取开发者模式失败', error));
  }, []);

  useEffect(() => {
    trackPageView(activeSection);
    if (isManagedWorkbenchSection(activeSection)) return;
    void window.yibiao?.ui?.setCurrentView({ section: activeSection });
  }, [activeSection]);

  useEffect(() => {
    if (!developerMode && isDeveloperSection(activeSection)) {
      setActiveSection('bid-generation');
    }
  }, [activeSection, developerMode]);

  const requestSectionChange = async (section: SectionId) => {
    if (section === activeSection) {
      return true;
    }
    const allowed = await (leaveGuardRef.current?.(section) ?? Promise.resolve(true));
    if (allowed) {
      setActiveSection(section);
    }
    return allowed;
  };
  const { settingsRequest, clearSettingsRequest } = useTextModelSetupRedirect(requestSectionChange);
  const returnHome = async () => {
    const allowed = await (leaveGuardRef.current?.('engispace-home') ?? Promise.resolve(true));
    if (allowed) window.close();
  };

  return (
    <>
      <GpuHardwareAccelerationPrompt />
      <RequiredOnlineServicesPrompt />
      {/* 本移植版由EngiSpace维护版本，不自动安装上游发行包。 */}
      {/* 上游签名/商业授权提示不冒充本移植版授权；AGPL与作者信息在侧栏保留。 */}
      <AppShell
        activeSection={activeSection}
        developerMode={developerMode}
        onSectionChange={(section) => { void requestSectionChange(section); }}
        onReturnHome={() => { void returnHome(); }}
      >
        <AppRouter
          activeSection={activeSection}
          developerMode={developerMode}
          onDeveloperModeChange={setDeveloperMode}
          onSectionChange={(section) => { void requestSectionChange(section); }}
          registerLeaveGuard={(guard) => {
            leaveGuardRef.current = guard;
          }}
          settingsRequest={settingsRequest}
          onSettingsRequestHandled={clearSettingsRequest}
        />
      </AppShell>
    </>
  );
}

export default App;
