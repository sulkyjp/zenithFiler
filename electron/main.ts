import { app, BrowserWindow, ipcMain } from 'electron';
import * as path from 'path';
import * as fs from 'fs';

function createWindow() {
  const win = new BrowserWindow({
    width: 1200,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: true,
      contextIsolation: false, // 個人用アプリなので開発のしやすさを優先
    },
    title: "Zenith Filer",
  });

  // 開発時はViteのデバックサーバーを表示、ビルド後はHTMLファイルを表示
  if (process.env.NODE_ENV === 'development') {
    win.loadURL('http://localhost:5173');
  } else {
    win.loadFile(path.join(__dirname, '../dist/index.html'));
  }
}

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

// ファイルシステム操作のAPI（例：ファイル一覧の取得）
ipcMain.handle('read-dir', async (event, dirPath: string) => {
  try {
    const files = fs.readdirSync(dirPath, { withFileTypes: true });
    return files.map(file => ({
      name: file.name,
      isDirectory: file.isDirectory(),
      size: file.isDirectory() ? 0 : fs.statSync(path.join(dirPath, file.name)).size,
      mtime: fs.statSync(path.join(dirPath, file.name)).mtime,
    }));
  } catch (error) {
    console.error(error);
    return [];
  }
});
