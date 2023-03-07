import moment = require('moment');
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import {dateFormat, projectHistoryFile} from './consts';
import {APPHOME_NAME_CONFIG, apphomeMkdir} from './common/apphome';
import {SdbgProject} from './Project';

export interface IHistoryItem {
    name: string
    basedir: string
    lastOpened: string
}

class ProjectHistoryBase {
    private readonly _jsonPath: string;

    constructor() {
        this._jsonPath = path.join(apphomeMkdir(APPHOME_NAME_CONFIG), projectHistoryFile);
    }

    update(projectPath: string, projectName?: string): void {
        if (!projectName || projectName.length === 0) {
            projectName = path.basename(projectPath);
        }

        let history = this.readFile();

        let item = history.get(projectPath);
        if (undefined === item) {
            item = {
                name: projectName,
                basedir: projectPath,
                lastOpened: moment().format(dateFormat)
            } as IHistoryItem;
        } else {
            item.lastOpened = moment().format(dateFormat);
        }

        history.set(projectPath, item);

        this.writeFile(history);
    }

    updateByWorkspace(): boolean {
        let result = false;
        let self = this;
        vscode.workspace.workspaceFolders?.forEach(
            (workspace: vscode.WorkspaceFolder) => {
                try {
                    if (SdbgProject.exists(workspace.uri.fsPath)) {
                        self.update(workspace.uri.fsPath);
                        result = true;
                    }
                } catch {
                }
            }
        );
        return result;
    }

    read(): Array<IHistoryItem> {
        let history = this.readFile();

        let items = Array.from(history.values());
        items.sort(
            (a: IHistoryItem, b: IHistoryItem) => {
                let dateA = moment(a.lastOpened, dateFormat);
                let dateB = moment(b.lastOpened, dateFormat);
                return dateA.isSameOrAfter(dateB) ? -1 : 1;
            }
        );

        return items;
    }


    private readFile(): Map<string, IHistoryItem> {
        let result = new Map<string, IHistoryItem>();

        if (!fs.existsSync(this._jsonPath)) {
            return result;
        }

        let jsonData = JSON.parse(fs.readFileSync(this._jsonPath, 'utf8'));
        for (let k in jsonData) {
            let v = jsonData[k];
            result.set(k, v);
        }

        return result;
    }

    private writeFile(data: Map<string, IHistoryItem>): void {
        let jsonData = {};
        data.forEach((v, k) => {
            jsonData[k] = v;
        });

        fs.writeFileSync(
            this._jsonPath,
            JSON.stringify(jsonData, null, 4)
        );
    }
}

export let ProjectHistory = new ProjectHistoryBase();
