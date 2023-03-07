import * as vscode from 'vscode';
import {Tools} from "./Tools";
import {buildCommandName, errNoOpenProject, errNoSelectedContract, showSideBarCommandName} from "./consts";
import {OutputWindow} from "./OutputWindow";
import * as PromisifyChildProcess from 'promisify-child-process';
import {Project} from './Project';
import * as fs from 'fs';


class CmdBuildBase {
    private _extension?: vscode.ExtensionContext;

    /** Called when VSCode extension is activated */
    init(context: vscode.ExtensionContext) {
        this._extension = context;
        vscode.commands.registerCommand(buildCommandName, async () => {
            await this.vscBuild();
        });
    }

    private async vscBuild(): Promise<void> {
        if (!Project.current) {
            vscode.commands.executeCommand(showSideBarCommandName);
            OutputWindow.write(errNoOpenProject, true);
            return;
        }

        let contractJson = Project.current.selectedContractJson;
        if (!contractJson) {
            OutputWindow.write(errNoSelectedContract, true);
            return;
        }

        if (contractJson?.content.solc) {
            if (!Tools.solidity.isInstalled(contractJson.content.solc)) {
                await Tools.installSolc(contractJson.content.solc);
            }
        }

        OutputWindow.clear();
        OutputWindow.show();

        let processExe = Tools.engine.sdbgExePath;
        let processArgs = [
            '-P', Project.current.path,
            '-C', Project.current.selectedContractName as string,
            '-B'
        ];

        if (fs.existsSync(Tools.engine.dotNetExePath)) {
            processExe = Tools.engine.dotNetExePath;
            processArgs = [Tools.engine.sdbgDllPath].concat(processArgs);
        }

        OutputWindow.write(`------ Build started: Target: ${Project.current.selectedContractName} ------`);
        OutputWindow.write(`${processExe} ${processArgs.join(' ')}`);

        let process = PromisifyChildProcess.execFile(processExe, processArgs);

        process.stdout?.addListener('data', (chunk: any) => {
            OutputWindow.write(`${chunk}`);
        });

        process.stderr?.addListener('data', (chunk: any) => {
            OutputWindow.write(`${chunk}`);
        });

        process.on('exit', (statusCode) => {
            let successCount = (statusCode === 0) ? 1 : 0;
            let failCount = 1 - successCount;
            OutputWindow.write(`========== Build: ${successCount} succeeded, ${failCount} failed, 0 skipped ==========`);
        });
    }
}

export let CmdBuild = new CmdBuildBase();
