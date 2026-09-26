const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('workbench', {
  chooseSave: () => ipcRenderer.invoke('choose-save'),
  chooseOpen: () => ipcRenderer.invoke('choose-open'),
  work: request => ipcRenderer.invoke('work', request),
  openDocx: path => ipcRenderer.invoke('open-docx', path),
})
