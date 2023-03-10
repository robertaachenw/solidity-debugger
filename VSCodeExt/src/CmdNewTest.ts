import * as vscode from 'vscode';
import {newTestCommandName} from './consts';
import {Project} from './Project';
import {Tools} from "./Tools";

async function AddNewTest(testName: string) {
    if (!Project.current) {
        return;
    }

    let solFilePath = Project.current.createNewContract(testName);
    Project.current.selectContract(testName);
    vscode.commands.executeCommand('vscode.open', vscode.Uri.file(solFilePath));
}

class CmdNewTestBase {
    private _extension?: vscode.ExtensionContext;

    /** Called when VSCode extension is activated */
    init(context: vscode.ExtensionContext) {
        this._extension = context;
        vscode.commands.registerCommand(newTestCommandName, async () => {
            Tools.isInstalled(true);

            if (!Project.current) {
                return;
            }

            let cname = await vscode.window.showInputBox(
                {
                    title: 'New Test Name',
                    value: Project.current.getAvailContractName()
                } as vscode.InputBoxOptions
            );

            if (!cname || cname.length === 0) {
                return;
            }

            await AddNewTest(cname);
            vscode.commands.executeCommand("workbench.files.action.focusFilesExplorer");
        });
    }

}


export let CmdNewTest = new CmdNewTestBase();
