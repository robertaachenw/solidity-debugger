import * as vscode from "vscode";
import {Uri, Webview} from "vscode";
import * as fs from 'fs';
import * as path from 'path';
import {newTestCommandName, ProjectJson, projectSettingsCommandName} from "./consts";
import {Project} from "./Project";
import {Tools} from "./Tools";

const projectSettingsWebViewName = 'sdbg.projectSettings.webView';
const projectSettingsTabTitle = 'Project Settings';

const resourcesFolder = 'webview-ui';
const resourceFileCSS = 'ProjectSettingsWindow.css';
const resourceFileJavascript = 'ProjectSettingsWindow.js';

type GuiCommand =
    'init'
    | 'getContractNames'
    | 'getProjectRoot'
    | 'getSolidityVersions'
    | 'getProjectJsons'
    | 'buttonAddTest'
    | 'buttonCancel'
    | 'buttonSave'
    | 'buttonApply'
    | 'buttonRemoveTest';

interface MsgFromGui {
    request: GuiCommand
    saveData?: JsonPack
    selectedContractName?: string
}

interface MsgToGui {
    response: GuiCommand
    value: any
}

interface JsonPack {
    projectJson?: ProjectJson
    selectedContractName?: string
    contractJsonPure: any
    contractJsonMerged: any
}

class ProjectSettingsBase {
    private _extension?: vscode.ExtensionContext;
    private _window?: vscode.WebviewPanel;
    private _lastSent?: JsonPack;

    private static getUri(webview: Webview, extensionUri: Uri, pathList: string[]) {
        return webview.asWebviewUri(Uri.joinPath(extensionUri, ...pathList));
    }

    init(context: vscode.ExtensionContext) {
        this._extension = context;

        vscode.commands.registerCommand(projectSettingsCommandName, () => {
            this._window = vscode.window.createWebviewPanel(projectSettingsWebViewName, projectSettingsTabTitle, vscode.ViewColumn.One, {
                enableScripts: true,
            });

            this._window.title = projectSettingsTabTitle;
            this._window.webview.html = this.getHtml(this._window.webview, context.extensionUri);

            this._window.webview.onDidReceiveMessage((message) => {
                this.onMessage(message);
            });
        });
    }

    private applyButtonClick(mods: JsonPack) {
        if (mods.projectJson) {
            Project.current?.projectJson.update((content) => {
                return mods.projectJson;
            });
        }

        if (mods.contractJsonMerged) {
            for (const contractName in mods.contractJsonMerged) {
                if (!Project.current?.contractExists(contractName)) {
                    continue;
                }
                Project.current?.getContractJson(contractName, true).update((content) => {
                    return mods.contractJsonMerged[contractName];
                });
            }
        }
    }

    private onMessage(msg: MsgFromGui) {
        switch (msg.request) {
            case 'init':
                this._window?.webview.postMessage({
                    'response': 'init',
                } as MsgToGui);
                break;

            case 'getProjectRoot':
                this._window?.webview.postMessage({
                    'response': 'getProjectRoot', 'value': Project.current?.path
                } as MsgToGui);
                break;

            case 'getContractNames':
                this._window?.webview.postMessage({
                    'response': 'getContractNames', 'value': Project.current?.getContractNames() ?? []
                } as MsgToGui);
                break;

            case 'getSolidityVersions':
                this._window?.webview.postMessage({
                    'response': 'getSolidityVersions', 'value': Array.from(Tools.solidity.supportedVersions.values())
                } as MsgToGui);
                break;

            case 'getProjectJsons':
                let jsonPack: JsonPack = {
                    'projectJson': Project.current?.projectJson?.data,
                    'selectedContractName': Project.current?.selectedContractName,
                    'contractJsonPure': {},
                    'contractJsonMerged': {}
                };

                for (let contractName of Project.current?.getContractNames() ?? []) {
                    jsonPack['contractJsonPure'][contractName] = Project.current?.getContractJson(contractName, false).data;

                    jsonPack['contractJsonMerged'][contractName] = Project.current?.getContractJson(contractName, true).data;
                }

                this._window?.webview.postMessage({
                    'response': 'getProjectJsons', 'value': jsonPack
                } as MsgToGui);
                this._lastSent = jsonPack;
                break;

            case 'buttonAddTest':
                let before = Project.current?.getContractNames().length;
                vscode.commands.executeCommand(newTestCommandName).then(() => {
                    setTimeout(() => {
                        let after = Project.current?.getContractNames().length;
                        if (before !== after) {
                            this._window?.dispose();
                            vscode.commands.executeCommand(projectSettingsCommandName);
                        }
                    }, 500);
                });
                break;

            case 'buttonCancel':
                this._window?.dispose();
                break;

            case 'buttonApply':
                if (msg.saveData) {
                    this.applyButtonClick(msg.saveData);
                }
                break;

            case 'buttonSave':
                if (msg.saveData) {
                    this.applyButtonClick(msg.saveData);
                }
                this._window?.dispose();
                break;

            case 'buttonRemoveTest':
                if (msg.selectedContractName && Project.current?.contractExists(msg.selectedContractName)) {
                    vscode.window.showInformationMessage(`You can remove ${msg.selectedContractName} by removing its directory:\n${Project.current?.getContractPath(msg.selectedContractName)}`, 'OK');
                }
                break;

            default:
                break;
        }
    }

    private getHtml(webview: Webview, extensionUri: Uri): string {
        if (!this._extension) {
            return '';
        }

        const toolkitUri = ProjectSettingsBase.getUri(webview, extensionUri, ["node_modules", "@vscode", "webview-ui-toolkit", "dist", "toolkit.js",]);
        const styleUri = ProjectSettingsBase.getUri(webview, extensionUri, [resourcesFolder, resourceFileCSS]);
        const newProjectJsUri = ProjectSettingsBase.getUri(webview, extensionUri, [resourcesFolder, resourceFileJavascript]);

        const htmlHead = `
		<script type="module" src="${toolkitUri}"></script>
		<script type="module" src="${newProjectJsUri}"></script>
		<link rel="stylesheet" href="${styleUri}">
		<title>note_title</title>
		`;

        let html = fs.readFileSync(path.join(this._extension.extensionPath, 'webview-ui/ProjectSettingsWindow.html')).toString();

        return html.replace('<!--HEAD-->', htmlHead);
    }
}

export let CmdProjectSettings = new ProjectSettingsBase();
