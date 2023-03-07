import * as vscode from 'vscode';
import {productName} from "./consts";

class OutputBase {
    private _extension?: vscode.ExtensionContext;
    private _channel?: vscode.OutputChannel;

    /** Called when VSCode extension is activated */
    init(context: vscode.ExtensionContext) {
        this._extension = context;
        this._channel = vscode.window.createOutputChannel(productName);
    }

    write(line: string, show?: boolean): void {
        this._channel?.appendLine(line);
        if (show) {
            this.show();
        }
    }

    show(): void {
        this._channel?.show(true);
    }

    clear(): void {
        this._channel?.clear();
    }
}

export let OutputWindow = new OutputBase();
