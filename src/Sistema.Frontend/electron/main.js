const { app, BrowserWindow, Menu, shell } = require('electron')
const path = require('path')
const isDev = process.env.NODE_ENV === 'development'

let win

function createWindow() {
  win = new BrowserWindow({
    width: 1400,
    height: 900,
    minWidth: 1024,
    minHeight: 700,
    icon: path.join(__dirname, '../public/icon.ico'),
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
    },
    titleBarStyle: 'hidden',
    titleBarOverlay: {
      color: '#ffffff',
      symbolColor: '#2E7D32',
      height: 40,
    },
    show: false,
  })

  if (isDev) {
    win.loadURL('http://localhost:5173')
    win.webContents.openDevTools()
  } else {
    win.loadFile(path.join(__dirname, '../dist/index.html'))
  }

  win.once('ready-to-show', () => win.show())

  // Abrir links externos no browser do SO
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (url.startsWith('http')) shell.openExternal(url)
    return { action: 'deny' }
  })
}

// Menu simplificado
const template = [
  {
    label: 'Sistema',
    submenu: [
      { label: 'PDV', accelerator: 'CmdOrCtrl+P', click: () => win.webContents.send('navigate', '/pdv') },
      { type: 'separator' },
      { label: 'Sair', role: 'quit' }
    ]
  },
  {
    label: 'Visualizar',
    submenu: [
      { label: 'Recarregar', role: 'reload' },
      { label: 'Ferramentas do Desenvolvedor', role: 'toggleDevTools' },
      { type: 'separator' },
      { label: 'Zoom +', role: 'zoomIn' },
      { label: 'Zoom -', role: 'zoomOut' },
      { label: 'Tamanho real', role: 'resetZoom' },
      { type: 'separator' },
      { label: 'Tela cheia', role: 'togglefullscreen' },
    ]
  }
]

app.whenReady().then(() => {
  Menu.setApplicationMenu(Menu.buildFromTemplate(template))
  createWindow()

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') app.quit()
})
