import * as vscode from 'vscode';
import {
    buildCommandName,
    debugCommandName,
    newTestCommandName,
    productName,
    projectSettingsCommandName,
    selectContractCommandName, showSideBarCommandName
} from './consts';
import {Project} from './Project';

const statusBarAlignment = vscode.StatusBarAlignment.Left;
const statusBarPriority = 0x7fffffff;

type IconName =
    'chevron-left'
    | 'chevron-right'
    | 'flame'
    | 'plus'
    | 'versions'
    | 'bug'
    | 'gear'
    | 'layers'
    | 'debug-alt'
    | 'book'
    | 'settings-gear'
    | 'list-selection';


class StatusBarItemEx {
    private static _cnt = 0;
    text: string = '';
    private readonly _id: string;
    private _obj: vscode.StatusBarItem;
    private readonly _icon: string = '';
    private readonly _prefix: string = '';
    private readonly _command?: string;

    constructor(icon?: IconName, prefix?: string, text?: string, command?: string, id?: string) {
        this._id = id ? id : `statusBarItem${StatusBarItemEx._cnt++}`;
        if (icon) {
            this._icon = '$(' + icon + ')';
        }
        if (prefix) {
            this._prefix = prefix;
        }
        if (command) {
            this._command = command;
        }
        if (text) {
            this.text = text;
        }

        this._obj = vscode.window.createStatusBarItem(statusBarAlignment, statusBarPriority - StatusBarItemEx._cnt);
        this._obj.command = this._command;
    }

    get id(): string {
        return this._id;
    }

    show(): void {
        let fulltext = '';

        if (this._icon.length > 0) {
            fulltext += this._icon + ' ';
        }

        if (this._prefix.length > 0) {
            fulltext += this._prefix;
        }

        fulltext += this.text;

        this._obj.text = fulltext;
        this._obj.show();
    }

    hide(): void {
        this._obj.hide();
    }
}


class StatusBarBase {
    sbarSelContract?: StatusBarItemEx;
    sbarBuildBtn?: StatusBarItemEx;
    sbarDebugBtn?: StatusBarItemEx;
    private _extension?: vscode.ExtensionContext;
    private _items: StatusBarItemEx[] = [];

    init(context: vscode.ExtensionContext) {
        this._extension = context;

        if (!Project.current) {
            return;
        }

        this._items.push(new StatusBarItemEx('chevron-left', productName));

        this._items.push(new StatusBarItemEx('plus', 'Create New Test', undefined, newTestCommandName));
        this.sbarSelContract = new StatusBarItemEx('list-selection', 'Selected: ', '-', selectContractCommandName);
        this._items.push(this.sbarSelContract);

        this._items.push(new StatusBarItemEx(undefined, '|'));

        this.sbarDebugBtn = new StatusBarItemEx('debug-alt', 'Debug ', undefined, debugCommandName);
        this._items.push(this.sbarDebugBtn);
        this.sbarBuildBtn = new StatusBarItemEx('layers', 'Build ', undefined, buildCommandName);
        this._items.push(this.sbarBuildBtn);

        this._items.push(new StatusBarItemEx(undefined, '|'));
        this._items.push(new StatusBarItemEx('settings-gear', 'Settings', undefined, projectSettingsCommandName));
        this._items.push(new StatusBarItemEx('chevron-right'));

        try {
            if (Project.current.selectedContractName) {
                this.selectContract(Project.current.selectedContractName);
            }
        } catch {
        }

        vscode.commands.registerCommand(selectContractCommandName, async () => {
            await this.vscSelectContract(context);
        });

        this._show();
    }

    async vscSelectContract(
        context: vscode.ExtensionContext,
        onSelect?: (selectedItem: string) => Promise<void>
    ) {
        if (!Project.current) {
            vscode.commands.executeCommand(showSideBarCommandName);
            return;
        }

        let qp = vscode.window.createQuickPick();

        let items: vscode.QuickPickItem[] = [];
        for (let contractName of Project.current.getContractNames()) {
            let item: vscode.QuickPickItem = {
                label: contractName,
                picked: (contractName === Project.current.selectedContractName)
            };

            items.push(item);
        }

        qp.items = items;
        qp.title = 'Select Debugger Entry-Point Contract';
        qp.canSelectMany = false;

        qp.onDidChangeSelection((sel) => {
            this.selectContract(sel[0].label);
            qp.hide();

            if (onSelect) {
                onSelect(sel[0].label);
            }
        });

        qp.onDidHide(() => qp.dispose());
        qp.show();
    }

    private selectContract(cname: string) {
        if (!Project.current) {
            return;
        }

        try {
            Project.current.selectContract(cname);

            if (this.sbarSelContract) {
                this.sbarSelContract.text = `${Project.current.selectedContractName}`;
                this.sbarSelContract.show();
            }

            if (this.sbarBuildBtn) {
                this.sbarBuildBtn.text = `${Project.current.selectedContractName}`;
                this.sbarBuildBtn.show();
            }

            if (this.sbarDebugBtn) {
                this.sbarDebugBtn.text = `${Project.current.selectedContractName}`;
                this.sbarDebugBtn.show();
            }

            let fpath = Project.current.getContractMainSol(cname);
            if (fpath) {
                vscode.commands.executeCommand('vscode.open', vscode.Uri.file(fpath));
            }
        } catch (e) {
            vscode.window.showErrorMessage(`${e}`);
        }
    }

    private _show() {
        for (let e of this._items) {
            e.show();
        }
    }

    private _hide() {
        for (let e of this._items) {
            e.hide();
        }
    }
}

export let StatusBar = new StatusBarBase();
