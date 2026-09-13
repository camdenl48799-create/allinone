const { contextBridge } = require('electron');

contextBridge.exposeInMainWorld('allinoneDesktop', {
  platform: process.platform,
  version: process.versions.electron
});
