// 今回は開発の簡略化のため contextIsolation: false にしていますが、
// 将来的にセキュアにする場合はここでAPIを露出させます。
import { ipcRenderer } from 'electron';

// contextIsolation: true の場合は以下のように記述します
// contextBridge.exposeInMainWorld('electronAPI', {
//   readDir: (path: string) => ipcRenderer.invoke('read-dir', path)
// })

console.log('Preload script loaded');
