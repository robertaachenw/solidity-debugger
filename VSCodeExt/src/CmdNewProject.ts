import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import * as util from 'util';
import * as vscode from "vscode";
import { Uri, Webview } from "vscode";
import { CmdOpenProject } from "./CmdOpenProject";
import { sformat } from "./common/common";
import { DownloadCache } from "./common/DownloadCache";
import { ZipExtract } from "./common/extractzip";
import {
    GITHUB_PROJECT_TEMPLATE_EMPTY_URL,
    GITHUB_PROJECT_TEMPLATE_ERC20_URL,
    GITHUB_PROJECT_TEMPLATE_HARDHAT_URL,
    HOMEPAGE_JSON_URL,
    IReleaseJson
} from "./common/hardcodedURLs";
import {
    ContractJson,
    contractJsonFileName,
    contractsDirName,
    entryPointContractName,
    entryPointContractTemplate, hardhatConfigJs, hardhatConfigTs,
    newProjectCommandName,
    ProjectJson,
    projectJsonFileName
} from "./consts";
import { Project, SdbgProject } from "./Project";
import { ProjectHistory } from "./ProjectHistory";
import { StatusBar } from "./StatusBar";
import { Tools } from "./Tools";

const newProjectWebViewName = 'sdbg.newProject.webView';
const newProjectTabTitle = 'New Project';

const resourcesFolder = 'webview-ui';
const resourceFileCSS = 'NewProjectWindow.css';
const resourceFileJavascript = 'NewProjectWindow.js';

const inputProjectName = "input-project-name";

const chkTemplateMinimal = "chk-template-minimal";
const chkTemplateERC20 = "chk-template-erc20";
const chkTemplateHardhat = "chk-template-hh";

const dropDownSolidityVersion = "dropdown-solidity-version";
const inputProjectPath = "input-project-path";
const buttonCreateProject = "button-create-project";
const buttonGoBack = "button-go-back";

const viewChooseOption = "view-choose-option";
const viewInitHere = "view-init-here";
const viewInitFromExisting = "view-init=from-existing";
const viewInitFromExample = "view-init-from-example";

const radioInitHere = "radio-init-here";
const radioFromExisting = "radio-from-existing";
const radioFromExample = "radio-from-example";

const buttonBrowse = "button-browse";
const inputBrowsePath = "input-browse-path";

const projectsHomeDir = path.join(os.homedir(), 'SolidityProjects');
const projectNamePrefix = 'MyContract';

enum CreateProjectLocation {
    CurrentFolder,
    ExistingFolder,
    FromExample
}

interface IGuiContent {
    sender?: string
    projectName?: string
    templateMinimal?: boolean
    templateERC20?: boolean
    templateHardhat?: boolean
    solidityVersion?: string
    projectPath?: string
    projectPathPlaceHolder?: string
    existingProjectPath?: string
    activePanel?: string
}


class NewProjectBase {
    private _extension?: vscode.ExtensionContext;
    private _window?: vscode.WebviewPanel;

    private static getUri(webview: Webview, extensionUri: Uri, pathList: string[]) {
        return webview.asWebviewUri(Uri.joinPath(extensionUri, ...pathList));
    }

    private static supportedSolidityVersionsHtml(): string {
        let result = '';

        for (let ver of Tools.solidity.supportedVersions.values()) {
            result += util.format('<vscode-option>%s</vscode-option>', ver);
        }

        return result;
    }

    private static isWorkspaceHardhatProject(): boolean {
        if (!vscode.workspace.workspaceFolders) {
            return false;
        }

        for (let folder of vscode.workspace.workspaceFolders) {
            if (fs.existsSync(path.join(folder.uri.fsPath, 'hardhat.config.ts'))) {
                return true;
            }

            if (fs.existsSync(path.join(folder.uri.fsPath, 'hardhat.config.js'))) {
                return true;
            }
        }

        return false;
    }

    private static getHtml(webview: Webview, extensionUri: Uri): string {
        const toolkitUri = NewProjectBase.getUri(webview, extensionUri, [
            "node_modules",
            "@vscode",
            "webview-ui-toolkit",
            "dist",
            "toolkit.js",
        ]);
        const styleUri = NewProjectBase.getUri(webview, extensionUri, [resourcesFolder, resourceFileCSS]);
        const newProjectJsUri = NewProjectBase.getUri(webview, extensionUri, [resourcesFolder, resourceFileJavascript]);

        return `
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
	
		<header>
			<h2>Choose New Target</h2>
			<div id="tags-container"></div>
			<vscode-divider></vscode-divider>
			<br>
		</header>
	
		<section id="new-project-container">
            <vscode-text-field id="${inputProjectName}" size="80">Target name</vscode-text-field>
            <br>
            <br>

            <div id="${viewChooseOption}">
                <vscode-radio-group orientation="vertical" id="radio-choose-option">
                    <label slot="label">Choose an option</label>
                    <vscode-radio id="${radioFromExample}" appearance="primary" checked>Create from example</vscode-radio>
                    <vscode-radio id="${radioFromExisting}" appearance="primary">Create from existing Hardhat project</vscode-radio>
                    <vscode-radio id="${radioInitHere}" appearance="primary" ${this.isWorkspaceHardhatProject() ? '' : 'disabled'}>Create from currently-open Hardhat project</vscode-radio>
                </vscode-radio-group>
                <div style="margin-top: 2rem">
                    <vscode-button id="next-btn" appearance="primary">Next</vscode-button>
                </div>
            </div>
            <div id="${viewInitHere}" hidden>
            </div>
            <div id="view-init-from-existing" hidden>
                <label slot="label">New Target from existing Hardhat project: </label>
                <div style="margin-top: 1rem; display: flex">
                    <vscode-button id="${buttonBrowse}">Browse</vscode-button>
                    <vscode-text-field size=64 id="${inputBrowsePath}" disabled></vscode-text-field>      
                </div>
            </div>
            <div id="${viewInitFromExample}" hidden>
                <label slot="label">New Target from example contract</label>
                <div>
                    <vscode-radio-group orientation="vertical">
                        <vscode-radio id="${chkTemplateMinimal}" checked >
                            <b>Empty Contract</b><br>
                            Bare minimum to start debugging Solidity code
                        </vscode-radio>

                        <vscode-radio id="${chkTemplateERC20}" >
                            <b>Basic Token (ERC20)</b><br>
                            The most widespread token standard for fungible assets
                        </vscode-radio>

                        <vscode-radio id="${chkTemplateHardhat}">
                            <b>Hardhat Sample (Lock.sol)</b><br>
                            Simple digital lock, where users could only withdraw funds after a given period of time.
                        </vscode-radio>
                    </vscode-radio-group>
                </div>                    
                <vscode-text-field id="${inputProjectPath}" size="80">Save Location</vscode-text-field>            
            </div>


            </div>
            <div id="view-create-proj-section" hidden style="margin-top: 1rem">
                <p>Solidity version for test contracts:</p>
                <vscode-dropdown id="${dropDownSolidityVersion}" position="below">
                    ${NewProjectBase.supportedSolidityVersionsHtml()}
                </vscode-dropdown>
                <div style="margin-top: 2rem">
                    <vscode-button id="${buttonGoBack}">Back</vscode-button>
                    <vscode-button id="${buttonCreateProject}" appearance="primary">Create</vscode-button>
                </div>
            </div>
		</section>
	
	
		</body>
		</html>
		`;
    }

    private static getAvailableProjectName(): string {
        for (let i = 1; ; i++) {
            let projectName = util.format("%s%d", projectNamePrefix, i);
            let projectPath = path.join(projectsHomeDir, projectName);
            if (fs.existsSync(projectPath)) {
                continue;
            }
            return projectName;
        }
    }

    /** Called when VSCode extension is activated */
    init(context: vscode.ExtensionContext) {
        this._extension = context;

        vscode.commands.registerCommand(
            newProjectCommandName,
            () => {
                Tools.isInstalled(true);

                this._window = vscode.window.createWebviewPanel(
                    newProjectWebViewName,
                    newProjectTabTitle,
                    vscode.ViewColumn.One,
                    {
                        enableScripts: true,
                    }
                );

                this._window.title = newProjectTabTitle;
                this._window.webview.html = NewProjectBase.getHtml(this._window.webview, context.extensionUri);

                this._window.webview.onDidReceiveMessage(async (message) => {
                    await this.onMessage(message);
                });
            }
        );
    }

    private setInitialValues() {
        let defaultProjectName = NewProjectBase.getAvailableProjectName();
        let defaultProjectPath = path.join(projectsHomeDir, defaultProjectName);
        const currSolidityVersions = SdbgProject.getCurrentWorkspaceSolidityVersions();
        this.updateGui({
            projectName: defaultProjectName,
            projectPathPlaceHolder: defaultProjectPath,
            currentWorkspaceSolidityVersions: currSolidityVersions,
        } as IGuiContent);
    }

    /**
     * Instructs NewProjectWindow.js to update part of the GUI
     * @param content
     */
    private updateGui(content: IGuiContent) {
        this._window?.webview.postMessage(content);
    }

    private async createProjectInPath(projectPath: string | undefined, solidityVersion: string | undefined): Promise<boolean> {
        // Sanity check the existing hardhat project
        if (!projectPath || projectPath.length === 0 || !fs.existsSync(projectPath)) {
            vscode.window.showErrorMessage("Invalid hardhat project path");
            return false;
        }

        if (!fs.existsSync(path.join(projectPath, hardhatConfigTs)) && !fs.existsSync(path.join(projectPath, hardhatConfigJs))) {
            vscode.window.showErrorMessage("Not a hardhat project path. could not find hardhat.config.[ts|js]");
            return false;
        }

        // check if global json file already exists
        if (fs.existsSync(path.join(projectPath, projectJsonFileName))) {
            vscode.window.showErrorMessage("Debug target already initialized in this project. use Open Existing Target instead.");
            this._window!.dispose();
            this._window = undefined;
            return false;
        }

        // Create global dbg settings
        const globalSettings: ProjectJson = {
            contractsDir: contractsDirName,
            selectedContract: "Test1",
            autoOpen: true,
            breakOnEntry: false,
            symbols: {hardhat: {projectPaths: [`${projectPath}`]}}
        };
        fs.writeFileSync(path.join(projectPath, projectJsonFileName), JSON.stringify(globalSettings, null, 4));

        // create dbg Folder
        const dbgFolder = path.join(projectPath, globalSettings.contractsDir as string);
        fs.mkdirSync(dbgFolder);

        // Create Test.t.sol with boilerplate
        const test1Folder = path.join(dbgFolder, "Test1");
        fs.mkdirSync(test1Folder);

        // Create test settings json file
        const testSettings: ContractJson = {
            entryPoint: `${entryPointContractName}`,
            solc: Tools.solidity.defaultVersion,
            sourceDirs: ["."],
            breakOnEntry: false,
            fork: {enable: false, url: "", blockNumber: 0}
        };
        fs.writeFileSync(path.join(test1Folder, contractJsonFileName), JSON.stringify(testSettings, null, 4));

        // Write test boilerplate
        fs.writeFileSync(path.join(test1Folder, "Test1.t.sol"), entryPointContractTemplate);

        // update solidity version in project json
        let project = new SdbgProject(projectPath);
        if (project.selectedContractJson) {
            project.selectedContractJson.update(
                (content) => {
                    content.solc = solidityVersion;
                }
            );
        }

        return true;
    }

    private async onCreateProjectClick(message: IGuiContent, where: CreateProjectLocation): Promise<void> {
        // if (this._window === undefined) {
        //     return;
        // }

        // sanity checks
        let projectName = message.projectName;
        if (!projectName || projectName.length === 0) {
            vscode.window.showErrorMessage("Project name cannot be empty");
            return;
        }

        if (where === CreateProjectLocation.ExistingFolder) {
            if (!message.existingProjectPath) {
                vscode.window.showErrorMessage("Empty path");
                return;
            }

            const success = await this.createProjectInPath(message.existingProjectPath, message.solidityVersion);
            if (success) {
                CmdOpenProject.openProject(message.existingProjectPath);
            }

            return;
        } else if (where === CreateProjectLocation.CurrentFolder) {
            const workspaceFolders = vscode.workspace.workspaceFolders;
            if (!workspaceFolders) {
                vscode.window.showErrorMessage("ERROR: Need to be opened in a workspace");
                return;
            }

            const projectPath = workspaceFolders[0].uri.fsPath;
            const success = await this.createProjectInPath(projectPath, message.solidityVersion);

            // Navigate to the newly created test.t.sol
            if (success && this._extension) {
                const testFile = path.join(projectPath, contractsDirName, "Test1", "Test1.t.sol");
                const testFileUri = vscode.Uri.file(testFile);
                const document = await vscode.workspace.openTextDocument(testFileUri);

                // Focus on file explorer
                vscode.commands.executeCommand("workbench.files.action.focusFilesExplorer");
                await vscode.window.showTextDocument(document);

                Project.init(this._extension);
                StatusBar.init(this._extension);
                ProjectHistory.updateByWorkspace();
            }
            return;
        }

        // Create From Example:

        let projectPath = message.projectPath ? message.projectPath : message.projectPathPlaceHolder;
        if (!projectPath || projectPath.length === 0) {
            vscode.window.showErrorMessage("Project path cannot be empty");
            return;
        }

        if (fs.existsSync(projectPath)) {
            vscode.window.showErrorMessage(`Directory exists: ${projectPath}`);
            return;
        }

        // make sure selected solidity compiler is installed
        if (!message.solidityVersion || message.solidityVersion.length === 0) {
            return;
        }

        if (!Tools.solidity.isInstalled(message.solidityVersion)) {
            await Tools.installSolc(message.solidityVersion);
        }

        // get project template url
        let templateZipUrl = '';

        try {
            let releaseJson = JSON.parse(DownloadCache.readFileFromCache(HOMEPAGE_JSON_URL)) as IReleaseJson;

            if (message.templateMinimal) {
                templateZipUrl = sformat(GITHUB_PROJECT_TEMPLATE_EMPTY_URL, releaseJson.latestRelease);
            } else if (message.templateERC20) {
                templateZipUrl = sformat(GITHUB_PROJECT_TEMPLATE_ERC20_URL, releaseJson.latestRelease);
            } else if (message.templateHardhat) {
                templateZipUrl = sformat(GITHUB_PROJECT_TEMPLATE_HARDHAT_URL, releaseJson.latestRelease);
            } else {
                return;
            }
        } catch (e) {
            vscode.window.showErrorMessage(`${e}`);
            return;
        }

        // close project creation tab
        this._window?.dispose();
        this._window = undefined;

        // download project template
        let templateZipPath: string | undefined = undefined;

        await vscode.window.withProgress(
            {
                location: vscode.ProgressLocation.Notification,
                cancellable: false,
                title: 'Downloading solidity debugger example'
            },
            async (progress) => {
                let percent = 0;
                templateZipPath = await DownloadCache.getFileAsync(
                    templateZipUrl,
                    ['cache-first'],
                    (op, newPercent) => {
                        if (percent !== newPercent) {
                            progress.report({increment: newPercent - percent});
                            percent = newPercent;
                        }
                    }
                );
            }
        );

        if (!templateZipPath) {
            vscode.window.showErrorMessage('Project template download failed');
            return;
        }

        // extract project template
        fs.mkdirSync(projectPath, {recursive: true});

        await vscode.window.withProgress(
            {
                location: vscode.ProgressLocation.Notification,
                cancellable: false,
                title: 'Extracting example and installing node modules'
            },
            async (progress) => {
                let zipExtract = new ZipExtract(templateZipPath as string);
                let percent = 0;
                await zipExtract.extract(
                    projectPath as string,
                    (op, newPercent) => {
                        if (percent !== newPercent) {
                            progress.report({increment: newPercent - percent});
                            percent = newPercent;
                        }
                    }
                );
            }
        );

        // update solidity version in project json
        let project = new SdbgProject(projectPath);
        if (project.selectedContractJson) {
            project.selectedContractJson.update(
                (content) => {
                    content.solc = message.solidityVersion;
                }
            );
        }

        // open newly created project
        CmdOpenProject.openProject(projectPath);
    }

    async createFromCurrentHardhatProject(projectName: string)
    {
        await this.onMessage({
            sender: buttonCreateProject,
            activePanel: radioInitHere,
            projectName: projectName,
            solidityVersion: Tools.solidity.defaultVersion
        });
    }

    /**
     * Called whenever NewProjectWindow.js sends us a message using vscode.postMessage()
     * @param message Message content
     * @returns
     */
    private async onMessage(message: IGuiContent): Promise<void> {
        if (message.sender === 'init') {
            this.setInitialValues();
            return;
        }

        if (message.sender === buttonCreateProject) {
            try {
                // console.log(JSON.stringify(message))
                let location = CreateProjectLocation.CurrentFolder;
                switch (message.activePanel) {
                    case radioInitHere:
                        location = CreateProjectLocation.CurrentFolder;
                        break;
                    case radioFromExisting:
                        location = CreateProjectLocation.ExistingFolder;
                        break;
                    case radioFromExample:
                        location = CreateProjectLocation.FromExample;
                        break;
                }
                await this.onCreateProjectClick(message, location);
            } catch (e) {
                vscode.window.showErrorMessage(`${e}`);
            }

        } else if (message.sender === inputProjectName) {
            this.updateGui(
                {
                    projectPathPlaceHolder: path.join(projectsHomeDir, message.projectName as string)
                } as IGuiContent
            );
        } else if (message.sender === buttonBrowse) {
            const chosenPath = await vscode.window.showOpenDialog({
                canSelectFolders: true,
                canSelectFiles: false,
                title: "Select Existing Hardhat Project"
            });
            if (chosenPath && chosenPath.length > 0) {
                this.updateGui(
                    {
                        browseSelectedPath: chosenPath[0].fsPath
                    } as IGuiContent
                );
            }
        }

    }
}

export let CmdNewProject = new NewProjectBase();
