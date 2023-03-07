import * as vscode from "vscode";
import {Uri, Webview} from "vscode";
import {dateFormat, openProjectCommandName} from "./consts";
import {ProjectHistory} from "./ProjectHistory";
import moment = require("moment");

const newProjectWebViewName = 'sdbg.openProject.webView';
const newProjectTabTitle = 'Open Project';

const resourcesFolder = 'webview-ui';
const resourceFileCSS = 'OpenProjectWindow.css';
const resourceFileJavascript = 'OpenProjectWindow.js';

const buttonOpenFolder = 'button-open-folder';
const inputOpenFolder = 'input-open-folder';
const inputOpenFolderDefaultValue = '/my/folder/path/';

interface GuiMessage {
    command: 'link' | 'openFolder'
    path?: string
}

class OpenProjectBase {
    private _extension?: vscode.ExtensionContext;
    private _window?: vscode.WebviewPanel;

    private static getUri(webview: Webview, extensionUri: Uri, pathList: string[]) {
        return webview.asWebviewUri(Uri.joinPath(extensionUri, ...pathList));
    }

    private static getHtml(webview: Webview, extensionUri: Uri): string {
        const toolkitUri = OpenProjectBase.getUri(webview, extensionUri, [
            "node_modules",
            "@vscode",
            "webview-ui-toolkit",
            "dist",
            "toolkit.js",
        ]);
        const styleUri = OpenProjectBase.getUri(webview, extensionUri, [resourcesFolder, resourceFileCSS]);
        const newProjectJsUri = OpenProjectBase.getUri(webview, extensionUri, [resourcesFolder, resourceFileJavascript]);

        let historyRows = '';
        try {
            let rows = ProjectHistory.read();

            for (let i = 0; i < rows.length; ++i) {
                let e = rows[i];

                historyRows += `
					<vscode-data-grid-row>
						<vscode-data-grid-cell grid-column="1">
							<vscode-link id="link-${i}" href="${e.basedir}">${e.name}</vscode-link>
							<br>
							${e.basedir}
						</vscode-data-grid-cell>

						<vscode-data-grid-cell grid-column="2">
							${moment(e.lastOpened, dateFormat).fromNow()}
						</vscode-data-grid-cell>
					</vscode-data-grid-row>`;
            }
        } catch {
            // ignored
        }


        return /*html*/ `
		<!DOCTYPE html>
		<html lang="en">
		<head>
		<meta charset="UTF-8">
		<meta name="viewport" content="width=device-width, initial-scale=1.0">
		<script type="module" src="${toolkitUri}"></script>
		<script type="module" src="${newProjectJsUri}"></script>
		<link rel="stylesheet" href="${styleUri}">
		<title>note_title</title>
		</head>
		<body id="webview-body">
	
		<h2>Open Existing Target</h2>
		<vscode-divider></vscode-divider>
		<br>

		<vscode-data-grid aria-label="Basic">
			<vscode-data-grid-row>

				<vscode-data-grid-cell grid-column="1">
					<vscode-text-field id="${inputOpenFolder}" size="160" placeholder="${inputOpenFolderDefaultValue}"></vscode-text-field>
				</vscode-data-grid-cell>

		  		<vscode-data-grid-cell grid-column="2">

				  <vscode-button id="${buttonOpenFolder}" appearance="primary">
				  	Open Existing Folder
			  	  </vscode-button>

				</vscode-data-grid-cell>
			</vscode-data-grid-row>


			<vscode-data-grid-row>
				<vscode-data-grid-cell grid-column="1">
				</vscode-data-grid-cell>
		  		<vscode-data-grid-cell grid-column="2">
				</vscode-data-grid-cell>
			</vscode-data-grid-row>

			${historyRows}

	  </vscode-data-grid>

		</body>
		</html>
		`;
    }

    init(context: vscode.ExtensionContext) {
        this._extension = context;

        vscode.commands.registerCommand(
            openProjectCommandName,
            () => {
                this._window = vscode.window.createWebviewPanel(
                    newProjectWebViewName,
                    newProjectTabTitle,
                    vscode.ViewColumn.One,
                    {
                        enableScripts: true,
                    }
                );

                this._window.title = newProjectTabTitle;
                this._window.webview.html = OpenProjectBase.getHtml(this._window.webview, context.extensionUri);

                this._window.webview.onDidReceiveMessage((message) => {
                    this.onMessage(message);
                });
            }
        );
    }

    openProject(projectDir: string) {
        try {
            ProjectHistory.update(projectDir);
        } catch {
            // ignored
        }

        vscode.commands.executeCommand('vscode.openFolder', vscode.Uri.file(projectDir));
    }

    private onMessage(message: GuiMessage) {
        switch (message.command) {
            case 'link':
                if (message.path) {
                    this.openProject(message.path);
                }
                break;

            case 'openFolder':
                if (message.path) {
                    this.openProject(message.path);
                } else {
                    vscode.commands.executeCommand('vscode.openFolder');
                }
                break;

            default:
                break;
        }
    }
}

export let CmdOpenProject = new OpenProjectBase();
