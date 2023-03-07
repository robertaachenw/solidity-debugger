import * as vscode from 'vscode';
import {CancellationToken, DebugConfiguration, ProviderResult, WorkspaceFolder} from 'vscode';
import {debugCommandName, errNoOpenProject, showSideBarCommandName} from './consts';
import {MockDebugSession} from './mockDebug';
import {FileAccessor} from './mockRuntime';
import {OutputWindow} from './OutputWindow';
import {Project} from './Project';
import {ProjectHistory} from './ProjectHistory';
import {Tools} from './Tools';
import {StatusBar} from "./StatusBar";

/** vscode command 'sdbg.debug' */
async function vscDebug(context: vscode.ExtensionContext, resource: vscode.Uri) {
    if (!ProjectHistory.updateByWorkspace()) {
        // no open project
        vscode.commands.executeCommand(showSideBarCommandName);
        OutputWindow.write(errNoOpenProject, true);
        return;
    }

    await StatusBar.vscSelectContract(context, async (selContractName) => {
        if (Project.current && Project.current.selectedContractJson && Project.current.selectedContractJson.content.solc) {
            if (!Tools.solidity.isInstalled(Project.current.selectedContractJson.content.solc)) {
                await Tools.installSolc(Project.current.selectedContractJson.content.solc);
            }
        }

        vscode.commands.executeCommand('workbench.view.debug');
        vscode.commands.executeCommand('workbench.debug.action.toggleRepl');

        let targetResource = resource;
        if (!targetResource && vscode.window.activeTextEditor) {
            targetResource = vscode.window.activeTextEditor.document.uri;
        }

        if (targetResource) {
            vscode.debug.startDebugging(undefined, {
                type: 'mock', name: 'Debug File', request: 'launch', program: targetResource.fsPath, stopOnEntry: true
            });
            // ...continues in DebugAdapterExecutableFactory
        }
    });
}


/** called when debug session terminates */
async function onDebugSessionTerminated(context: vscode.ExtensionContext, debugSession: vscode.DebugSession) {
}


export function initDebugger(context: vscode.ExtensionContext, factory?: vscode.DebugAdapterDescriptorFactory) {
    context.subscriptions.push(vscode.commands.registerCommand(debugCommandName, async (resource: vscode.Uri) => {
        await vscDebug(context, resource);
    }),); // context.subscriptions.push

    vscode.debug.onDidTerminateDebugSession(async (debugSession: vscode.DebugSession) => {
        await onDebugSessionTerminated(context, debugSession);
    });

    // register a configuration provider for 'mock' debug type
    const provider = new MockConfigurationProvider();
    context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider('mock', provider));

    // register a dynamic configuration provider for 'mock' debug type
    context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider('mock', {
        provideDebugConfigurations(folder: WorkspaceFolder | undefined): ProviderResult<DebugConfiguration[]> {
            return [{
                name: "Dynamic Launch", request: "launch", type: "mock", program: "${file}"
            }];
        }
    }, vscode.DebugConfigurationProviderTriggerKind.Dynamic));

    if (!factory) {
        factory = new InlineDebugAdapterFactory();
    }

    context.subscriptions.push(vscode.debug.registerDebugAdapterDescriptorFactory('mock', factory));
    if ('dispose' in factory) {
        context.subscriptions.push(factory);
    }

    // override VS Code's default implementation of the debug hover
    // here we match only Mock "variables", that are words starting with an '$'
    context.subscriptions.push(vscode.languages.registerEvaluatableExpressionProvider('markdown', {
        provideEvaluatableExpression(document: vscode.TextDocument, position: vscode.Position): vscode.ProviderResult<vscode.EvaluatableExpression> {

            const VARIABLE_REGEXP = /\$[a-z][a-z0-9]*/ig;
            const line = document.lineAt(position.line).text;

            let m: RegExpExecArray | null;
            while (m = VARIABLE_REGEXP.exec(line)) {
                const varRange = new vscode.Range(position.line, m.index, position.line, m.index + m[0].length);

                if (varRange.contains(position)) {
                    return new vscode.EvaluatableExpression(varRange);
                }
            }
            return undefined;
        }
    }));

    // override VS Code's default implementation of the "inline values" feature"
    context.subscriptions.push(vscode.languages.registerInlineValuesProvider('markdown', {

        provideInlineValues(document: vscode.TextDocument, viewport: vscode.Range, context: vscode.InlineValueContext): vscode.ProviderResult<vscode.InlineValue[]> {

            const allValues: vscode.InlineValue[] = [];

            for (let l = viewport.start.line; l <= context.stoppedLocation.end.line; l++) {
                const line = document.lineAt(l);
                var regExp = /\$([a-z][a-z0-9]*)/ig;	// variables are words starting with '$'
                do {
                    var m = regExp.exec(line.text);
                    if (m) {
                        const varName = m[1];
                        const varRange = new vscode.Range(l, m.index, l, m.index + varName.length);

                        // some literal text
                        //allValues.push(new vscode.InlineValueText(varRange, `${varName}: ${viewport.start.line}`));

                        // value found via variable lookup
                        allValues.push(new vscode.InlineValueVariableLookup(varRange, varName, false));

                        // value determined via expression evaluation
                        //allValues.push(new vscode.InlineValueEvaluatableExpression(varRange, varName));
                    }
                } while (m);
            }

            return allValues;
        }
    }));
}


class MockConfigurationProvider implements vscode.DebugConfigurationProvider {
    /**
     * Massage a debug configuration just before a debug session is being launched,
     * e.g. add all missing attributes to the debug configuration.
     */
    resolveDebugConfiguration(folder: WorkspaceFolder | undefined, config: DebugConfiguration, token?: CancellationToken): ProviderResult<DebugConfiguration> {

        const editor = vscode.window.activeTextEditor;
        if (!editor?.document.fileName.includes(".t.sol")) {
            vscode.window.showErrorMessage("Can only debug Test files (.t.sol)")
            return undefined;
        }

        // if launch.json is missing or empty
        if (!config.type && !config.request && !config.name) {
            if (editor && editor.document.languageId === 'solidity') {
                config.type = 'mock';
                config.name = 'Launch';
                config.request = 'launch';
                config.program = '${file}';
                config.stopOnEntry = true;
            }
        }

        if (!config.program) {
            return vscode.window.showInformationMessage("Cannot find a program to debug").then(_ => {
                return undefined;	// abort launch
            });
        }

        return config;
    }
}

export const workspaceFileAccessor: FileAccessor = {
    async readFile(path: string): Promise<Uint8Array> {
        let uri: vscode.Uri;
        try {
            uri = pathToUri(path);
        } catch (e) {
            return new TextEncoder().encode(`cannot read '${path}'`);
        }

        return await vscode.workspace.fs.readFile(uri);
    }, async writeFile(path: string, contents: Uint8Array) {
        await vscode.workspace.fs.writeFile(pathToUri(path), contents);
    }
};

function pathToUri(path: string) {
    try {
        return vscode.Uri.file(path);
    } catch (e) {
        return vscode.Uri.parse(path);
    }
}

class InlineDebugAdapterFactory implements vscode.DebugAdapterDescriptorFactory {
    createDebugAdapterDescriptor(_session: vscode.DebugSession): ProviderResult<vscode.DebugAdapterDescriptor> {
        return new vscode.DebugAdapterInlineImplementation(new MockDebugSession(workspaceFileAccessor));
    }
}
