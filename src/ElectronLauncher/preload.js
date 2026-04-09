const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('cs16', {
  closeWindow:    () => ipcRenderer.send('win-close'),
  minimizeWindow: () => ipcRenderer.send('win-minimize'),
  apiRequest:     (opts) => ipcRenderer.invoke('api-request', opts),
  launchCS:       (opts) => ipcRenderer.invoke('launch-cs', opts),
  getConfig:      ()     => ipcRenderer.invoke('get-config'),
  saveConfig:     (cfg)  => ipcRenderer.invoke('save-config', cfg),
  browseCSPath:   ()     => ipcRenderer.invoke('browse-cs-path'),
  queryServer:    (opts) => ipcRenderer.invoke('query-server', opts),
});


