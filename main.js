const { app, BrowserWindow, ipcMain } = require("electron");
const path = require("path");
const fs = require("fs");
const file = name => path.join(app.getPath("userData"), name);
const read = (name, fallback) => { try { return JSON.parse(fs.readFileSync(file(name), "utf8")); } catch { return fallback; } };
const write = (name, value) => { fs.mkdirSync(path.dirname(file(name)), {recursive:true}); fs.writeFileSync(file(name), JSON.stringify(value,null,2)); };
function createWindow(){const w=new BrowserWindow({width:1320,height:840,minWidth:980,minHeight:650,backgroundColor:"#08090d",title:"ALLINONE",webPreferences:{preload:path.join(__dirname,"preload.js"),contextIsolation:true,nodeIntegration:false,sandbox:true}});w.loadFile(path.join(__dirname,"src","index.html"))}
app.whenReady().then(()=>{ipcMain.handle("settings:get",()=>read("settings.json",null));ipcMain.handle("settings:save",(_,v)=>(write("settings.json",v),true));ipcMain.handle("history:get",()=>read("chat-history.json",[]));ipcMain.handle("history:save",(_,v)=>(write("chat-history.json",v),true));createWindow();app.on("activate",()=>{if(!BrowserWindow.getAllWindows().length)createWindow()})});
app.on("window-all-closed",()=>{if(process.platform!=="darwin")app.quit()});