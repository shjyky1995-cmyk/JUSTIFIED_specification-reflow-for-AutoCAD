const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('workbench', {
  openBidFramework: () => ipcRenderer.invoke('bid-framework-open'),
  onBidFrameworkState: callback => {
    const closed = () => callback({closed:true}); const failed = (_event,message) => callback({closed:true,error:message});
    ipcRenderer.on('bid-framework-closed',closed);ipcRenderer.on('bid-framework-failed',failed);
    return ()=>{ipcRenderer.removeListener('bid-framework-closed',closed);ipcRenderer.removeListener('bid-framework-failed',failed)};
  },
  chooseSave: name => ipcRenderer.invoke('choose-save', name),
  work: request => ipcRenderer.invoke('work', request),
  openDocx: path => ipcRenderer.invoke('open-docx', path),
  bids: {
    ai: {
      settings: () => ipcRenderer.invoke('bid-ai-settings'),
      configure: (model, key, remove) => ipcRenderer.invoke('bid-ai-configure', model, key, remove),
      prepare: selection => ipcRenderer.invoke('bid-ai-prepare', selection),
      start: (token, consent) => ipcRenderer.invoke('bid-ai-start', token, consent),
      discard: token => ipcRenderer.invoke('bid-ai-discard', token),
      list: id => ipcRenderer.invoke('bid-ai-list', id),
      cancel: (id, jobId) => ipcRenderer.invoke('bid-ai-cancel', id, jobId),
      apply: (id, jobId, indices) => ipcRenderer.invoke('bid-ai-apply', id, jobId, indices),
      remove: (id, jobId) => ipcRenderer.invoke('bid-ai-remove', id, jobId),
    },
    list: () => ipcRenderer.invoke('bid-list'),
    load: id => ipcRenderer.invoke('bid-load', id),
    save: bid => ipcRenderer.invoke('bid-save', bid),
    duplicate: id => ipcRenderer.invoke('bid-duplicate', id),
    importSource: () => ipcRenderer.invoke('bid-import-source'),
    openSource: (id, sourceId) => ipcRenderer.invoke('bid-open-source', id, sourceId),
    exportDocx: (id, mode) => ipcRenderer.invoke('bid-export', id, mode),
    backup: id => ipcRenderer.invoke('bid-backup', id),
    restore: () => ipcRenderer.invoke('bid-restore'),
  },
  notesList: () => ipcRenderer.invoke('notes-list'),
  noteLoad: id => ipcRenderer.invoke('note-load', id),
  noteSave: note => ipcRenderer.invoke('note-save', note),
  noteDelete: id => ipcRenderer.invoke('note-delete', id),
  projectsList: () => ipcRenderer.invoke('projects-list'),
  projectSave: project => ipcRenderer.invoke('project-save', project),
  catalogLoad: () => ipcRenderer.invoke('catalog-load'),
  catalogChoose: () => ipcRenderer.invoke('catalog-choose'),
  appInfo: () => ipcRenderer.invoke('app-info'),
  openDataFolder: () => ipcRenderer.invoke('open-data-folder'),
  duplicateNote: id => ipcRenderer.invoke('note-duplicate', id),
  exportBackup: () => ipcRenderer.invoke('backup-export'),
  importBackup: () => ipcRenderer.invoke('backup-import'),
  beforeClose: callback => {
    const listener = () => { Promise.resolve().then(callback).then(ok => ipcRenderer.send('close-ready', ok), () => ipcRenderer.send('close-ready', false)) }
    ipcRenderer.on('before-close', listener)
    return () => ipcRenderer.removeListener('before-close', listener)
  },
})
