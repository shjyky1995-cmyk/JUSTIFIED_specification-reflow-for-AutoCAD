const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('workbench', {
  chooseSave: name => ipcRenderer.invoke('choose-save', name),
  work: request => ipcRenderer.invoke('work', request),
  openDocx: path => ipcRenderer.invoke('open-docx', path),
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
