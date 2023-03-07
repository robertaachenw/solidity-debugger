import * as vscode from 'vscode';
import {newTestCommandName} from './consts';
import {Project} from './Project';

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

            AddNewTest(cname);
            vscode.commands.executeCommand("workbench.files.action.focusFilesExplorer");
        });
    }

}


export let CmdNewTest = new CmdNewTestBase();
