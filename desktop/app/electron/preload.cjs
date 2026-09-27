const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('workbench', {
  chooseSave: () => ipcRenderer.invoke('choose-save'),
  work: request => ipcRenderer.invoke('work', request),
  openDocx: path => ipcRenderer.invoke('open-docx'),
  notesList: () => ipcRenderer.invoke('notes-list'),
  noteLoad: id => ipcRenderer.invoke('note-load', id),
  noteSave: note => ipcRenderer.invoke('note-save', note),
  noteDelete: id => ipcRenderer.invoke('note-delete', id),
  projectsList: () => ipcRenderer.invoke('projects-list'),
  projectSave: project => ipcRenderer.invoke('project-save', project),
  catalogLoad: () => ipcRenderer.invoke('catalog-load'),
})
