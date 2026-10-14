import * as Tooltip from '@radix-ui/react-tooltip';
import type { ReactNode } from 'react';
import AgentRuntimeStatusBar from '../app/AgentRuntimeStatusBar';
import type { SectionId } from '../shared/types/navigation';
import Sidebar from './Sidebar';

interface AppShellProps {
  activeSection: SectionId;
  children: ReactNode;
  developerMode: boolean;
  onSectionChange: (section: SectionId) => void;
  onReturnHome: () => void;
}

function AppShell({ activeSection, children, developerMode, onSectionChange, onReturnHome }: AppShellProps) {
  const isMac = navigator.platform.toLowerCase().includes('mac');

  return (
    <Tooltip.Provider delayDuration={120} skipDelayDuration={80}>
      <div className={`app-shell${isMac ? ' is-mac' : ''}`}>
        <Sidebar activeSection={activeSection} developerMode={developerMode} onSectionChange={onSectionChange} />

        <main className="main-area">
          <header className="engispace-module-header">
            <span>工作台 <span aria-hidden="true">/</span> <strong>勘察设计投标</strong></span>
            <button type="button" className="secondary-action" onClick={onReturnHome}>返回平台首页</button>
          </header>
          <AgentRuntimeStatusBar />
          <section className="content-shell" aria-label="主内容">
            {children}
          </section>
        </main>
      </div>
    </Tooltip.Provider>
  );
}

export default AppShell;
