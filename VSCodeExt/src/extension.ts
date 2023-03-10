import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import {ProviderResult} from 'vscode';
import {CmdBuild} from './CmdBuild';
import {initDebugger} from './CmdDebug';
import {CmdNewProject} from './CmdNewProject';
import {CmdNewTest} from './CmdNewTest';
import {CmdOpenProject} from './CmdOpenProject';
import {CmdProjectSettings} from './CmdProjectSettings';
import {apphomeMkdir, APPHOME_NAME_CONFIG} from "./common/apphome";
import {DOCS_URL, TUTORIAL_URL} from "./common/hardcodedURLs";
import {
    checkForUpdatesCommandName,
    docsCommandName,
    errInstallInProgress,
    errInstallRequiresRestart,
    nopCommandName,
    productName,
    showSideBarCommandName
} from './consts';
import {OutputWindow} from './OutputWindow';
import {Project} from './Project';
import {ProjectHistory} from './ProjectHistory';
import {StatusBar} from './StatusBar';
import {Tools} from './Tools';
const open = require('open');

const delayedActivateInterval = 250;
let delayedActivatePopUp = false;

export async function activate(context: vscode.ExtensionContext) {
    try {
        await Tools.init(context);

        OutputWindow.init(context);
        CmdNewProject.init(context);
        CmdOpenProject.init(context);
        CmdBuild.init(context);
        CmdNewTest.init(context);
        CmdProjectSettings.init(context);
        Project.init(context);
        StatusBar.init(context);

        ProjectHistory.updateByWorkspace();

        vscode.commands.registerCommand(checkForUpdatesCommandName, async () => {
            await vscCheckForUpdates(context);
        });
        vscode.commands.registerCommand(docsCommandName, async () => {
            await vscShowDocs(context);
        });
        vscode.commands.registerCommand(nopCommandName, async () => {
            vscode.commands.executeCommand(showSideBarCommandName);
        });

        if (await Tools.install()) {
            activatePostInstall(context);
        } else {
            // installer could be running in another VSCode process
            setTimeout(async () => {
                await delayedActivate(context);
            }, delayedActivateInterval);
        }
    } catch (e) {
        vscode.window.showErrorMessage(`${e}`);
    }
}


function showNewInstallWelcome() {
    vscode.commands.executeCommand(showSideBarCommandName);
    vscode.window.showInformationMessage(`Welcome! Open ${productName} tutorial?`, 'Yes', 'No').then(response => {
        if (response !== 'Yes') {
            return;
        }
        open(TUTORIAL_URL);
    });
}


function activatePostInstall(context: vscode.ExtensionContext) {
    initDebugger(context, new DebugAdapterExecutableFactory());

    if (isNewInstall()) {
        showNewInstallWelcome();
    }
}


async function delayedActivate(context: vscode.ExtensionContext) {
    if (Tools.isInstallerRunning()) {
        // installer is still running in another VSCode process
        setTimeout(async () => {
            await delayedActivate(context);
        }, delayedActivateInterval);

        if (!delayedActivatePopUp) {
            vscode.window.showInformationMessage(errInstallInProgress);
            delayedActivatePopUp = true;
        }

        return;
    }

    // try one last time
    if (await Tools.install()) {
        activatePostInstall(context);

        if (delayedActivatePopUp) {
            showNewInstallWelcome();
        }

    } else {
        vscode.window.showInformationMessage(errInstallRequiresRestart);
    }
}


function isNewInstall(): boolean {
    let result = false;

    try {
        let configDir = apphomeMkdir(APPHOME_NAME_CONFIG);

        // write test
        let tempBasename = `${new Date().valueOf()}.tmp`;
        let tempFpath = path.join(configDir, tempBasename);
        fs.writeFileSync(tempFpath, 'test');
        fs.unlinkSync(tempFpath);

        // check flag
        let flagFilePath = path.join(configDir, 'new.txt');
        let flagFileExists = fs.existsSync(flagFilePath);
        fs.writeFileSync(flagFilePath, 'isNewInstall');
        result = !flagFileExists;
    } catch {
    }

    return result;
}


async function vscCheckForUpdates(context: vscode.ExtensionContext) {
    if (!Tools.isInstalled(true)) {
        return;
    }
    await Tools.install();
}


async function vscShowDocs(context: vscode.ExtensionContext) {
    vscode.window.showInformationMessage(DOCS_URL);
    await open(DOCS_URL);
}


export function deactivate() {
    // nothing to do
}


class DebugAdapterExecutableFactory implements vscode.DebugAdapterDescriptorFactory {
    createDebugAdapterDescriptor(_session: vscode.DebugSession, executable: vscode.DebugAdapterExecutable | undefined): ProviderResult<vscode.DebugAdapterDescriptor> {
        if (!Project.current) {
            throw new Error(`${productName}: No project is open`);
        }

        if (!Project.current.selectedContractJson) {
            throw new Error(`${productName}: No contract selected`);
        }

        let processExe = Tools.engine.sdbgExePath;
        let processArgs = ['-P', Project.current.path, '-C', Project.current.selectedContractName as string];

        if (fs.existsSync(Tools.engine.dotNetExePath)) {
            processExe = Tools.engine.dotNetExePath;
            processArgs = [Tools.engine.sdbgDllPath].concat(processArgs);
        }

        OutputWindow.write(processArgs.join(' '));
        OutputWindow.write('---');

        let options: vscode.DebugAdapterExecutableOptions = {
            cwd: Tools.engine.baseDir
        };

        executable = new vscode.DebugAdapterExecutable(processExe, processArgs, options);

        return executable;
    }
}
