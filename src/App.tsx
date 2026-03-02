import React, { useState } from 'react';
import { 
  Folder, 
  File, 
  Star, 
  ChevronRight, 
  ChevronDown, 
  Columns, 
  Layout,
  Search,
  Settings,
  MoreVertical,
  Plus
} from 'lucide-react';

// --- Types ---
interface FileItem {
  name: string;
  isDirectory: boolean;
  size: number;
  mtime: Date;
}

interface Favorite {
  id: string;
  name: string;
  path: string;
  type: 'folder' | 'file';
}

interface FavoriteGroup {
  id: string;
  name: string;
  items: Favorite[];
}

// --- Components ---

// サイドバーのお気に入り項目
const SidebarItem = ({ name, icon: Icon, active = false }: { name: string, icon: any, active?: boolean }) => (
  <div className={`flex items-center gap-2 px-3 py-1.5 rounded-md cursor-pointer transition-colors ${active ? 'bg-blue-600/20 text-blue-400' : 'hover:bg-slate-800 text-slate-400 hover:text-slate-200'}`}>
    <Icon size={16} />
    <span className="text-sm font-medium">{name}</span>
  </div>
);

// ファイルリストペイン
const FilePane = ({ id }: { id: number }) => {
  const [currentPath, setCurrentPath] = useState('C:/Users/User/Documents');
  
  // ダミーデータ（本来はipcRenderer経由で取得）
  const files: FileItem[] = [
    { name: 'Projects', isDirectory: true, size: 0, mtime: new Date() },
    { name: 'Photos', isDirectory: true, size: 0, mtime: new Date() },
    { name: 'todo.txt', isDirectory: false, size: 1024, mtime: new Date() },
    { name: 'budget.xlsx', isDirectory: false, size: 54200, mtime: new Date() },
  ];

  return (
    <div className="flex flex-col flex-1 min-w-0 border-r border-[#D3D0C3] last:border-r-0 bg-slate-900/50">
      {/* アドレスバー */}
      <div className="flex items-center gap-2 p-2 bg-slate-900 border-b border-[#D3D0C3]">
        <div className="flex-1 bg-slate-950 px-3 py-1 rounded border border-[#D3D0C3] text-xs text-slate-300 truncate">
          {currentPath}
        </div>
        <Search size={14} className="text-slate-500 cursor-pointer hover:text-slate-300" />
      </div>

      {/* ファイル一覧 */}
      <div className="flex-1 overflow-y-auto">
        <table className="w-full text-left border-collapse">
          <thead>
            <tr className="text-[11px] text-slate-500 uppercase tracking-wider border-b border-[#D3D0C3]">
              <th className="px-4 py-2 font-semibold">名前</th>
              <th className="px-4 py-2 font-semibold text-right">サイズ</th>
              <th className="px-4 py-2 font-semibold text-right">更新日時</th>
            </tr>
          </thead>
          <tbody>
            {files.map((file) => (
              <tr key={file.name} className="group hover:bg-blue-600/10 cursor-pointer text-sm">
                <td className="px-4 py-2 flex items-center gap-3 text-slate-200">
                  {file.isDirectory ? <Folder size={18} className="text-blue-400 fill-blue-400/20" /> : <File size={18} className="text-slate-400" />}
                  <span className="truncate">{file.name}</span>
                </td>
                <td className="px-4 py-2 text-right text-slate-500 text-xs">
                  {file.isDirectory ? '--' : `${(file.size / 1024).toFixed(1)} KB`}
                </td>
                <td className="px-4 py-2 text-right text-slate-500 text-xs">
                  {file.mtime.toLocaleDateString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
};

export default function App() {
  const [paneCount, setPaneCount] = useState(2);

  return (
    <div className="flex h-screen w-full bg-slate-950 text-slate-200 select-none">
      {/* サイドバー */}
      <div className="w-64 border-r border-[#D3D0C3] flex flex-col bg-slate-950">
        <div className="p-4 font-bold text-lg flex items-center justify-between">
          <span>Zenith Filer</span>
          <Plus size={18} className="text-slate-500 hover:text-slate-200 cursor-pointer" />
        </div>

        <div className="flex-1 overflow-y-auto px-2 space-y-4">
          {/* お気に入りフォルダセクション */}
          <div>
            <div className="space-y-0.5">
              <SidebarItem name="Desktop" icon={Layout} />
              <SidebarItem name="Documents" icon={Folder} active />
              <SidebarItem name="Downloads" icon={Folder} />
            </div>
          </div>

          {/* カスタムグループ */}
          <div>
            <div className="flex items-center gap-1 px-2 mb-1 text-xs font-bold text-slate-500 uppercase">
              <ChevronRight size={12} />
              <span>Work Projects</span>
            </div>
          </div>
        </div>

        <div className="p-3 border-t border-[#D3D0C3] flex items-center justify-between text-slate-500">
          <Settings size={18} className="hover:text-slate-200 cursor-pointer" />
          <div className="flex gap-2">
            <Columns 
              size={18} 
              className={`cursor-pointer ${paneCount === 2 ? 'text-blue-400' : 'hover:text-slate-200'}`} 
              onClick={() => setPaneCount(2)}
            />
            <Layout 
              size={18} 
              className={`cursor-pointer rotate-90 ${paneCount === 3 ? 'text-blue-400' : 'hover:text-slate-200'}`} 
              onClick={() => setPaneCount(3)}
            />
          </div>
        </div>
      </div>

      {/* メインエリア（分割ペイン） */}
      <div className="flex flex-1 overflow-hidden">
        {Array.from({ length: paneCount }).map((_, i) => (
          <FilePane key={i} id={i} />
        ))}
      </div>
    </div>
  );
}
