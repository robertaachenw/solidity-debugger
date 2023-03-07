import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';
import {
    ContractJson,
    contractJsonFileName,
    contractJsonTemplate,
    contractsDirName,
    entryPointContractName,
    entryPointContractTemplate,
    ProjectJson,
    projectJsonFileName,
    projectJsonTemplate
} from './consts';
import {JsonFile} from './common/JsonFile';
import {Tools} from './Tools';

export class SdbgProject {
    private readonly rootDir: string;
    private isOpen: boolean = false;
    private selectedContract?: string;
    private readonly solidityVersions: string[];

    constructor(rootDir: string) {
        this.rootDir = rootDir;
        this._projectJson = new JsonFile(path.join(this.rootDir, projectJsonFileName));
        this._open();
        this.selectDefaultContract();

        this.solidityVersions = SdbgProject.getCurrentWorkspaceSolidityVersions();
    }

    private _projectJson: JsonFile<ProjectJson>;

    get projectJson(): JsonFile<ProjectJson> {
        return this._projectJson;
    }

    /** path to project root directory */
    get path(): string {
        return this.rootDir;
    }

    get selectedContractName(): string | undefined {
        return this.selectedContract;
    }

    get selectedContractJson(): JsonFile<ContractJson> | undefined {
        if (this.selectedContractName) {
            return this.getContractJson(this.selectedContractName);
        }

        return undefined;
    }

    private get contractsDir(): string {
        return path.join(this.rootDir, this._projectJson.content.contractsDir as string);
    }

    /** Check if SDBG project exists at the given path */
    static exists(rootDir: string): boolean {
        return fs.existsSync(path.join(rootDir, projectJsonFileName));
    }

    static getCurrentWorkspaceSolidityVersions(): Array<string> {
        const versions = new Array<string>();

        const workspaceFolders = vscode.workspace.workspaceFolders
        if (!workspaceFolders) {
            return versions;
        }

        const projectPath = workspaceFolders[0].uri.fsPath;
        if (!projectPath || projectPath.length === 0 || !fs.existsSync(projectPath)) {
            return versions;
        }

        // Check if this is a hardhat project
        let configFile: string;
        if (fs.existsSync(path.join(projectPath, "hardhat.config.ts"))) {
            configFile = path.join(projectPath, "hardhat.config.ts");
        } else if (fs.existsSync(path.join(projectPath, "hardhat.config.js"))) {
            configFile = path.join(projectPath, "hardhat.config.js");
        } else {
            return versions;
        }

        const buildInfoFolder = path.join(projectPath, "artifacts", "build-info")
        if (!fs.existsSync(buildInfoFolder)) {
            return versions;
        }

        const solcVersionRegex = /^([0-9]+\.)+[0-9]+$/;
        const foundVersions = new Set<String>();
        for (const file of fs.readdirSync(buildInfoFolder)) {
            if (path.extname(file) !== ".json") {
                continue;
            }
            try {
                const buildInfo = JSON.parse(fs.readFileSync(path.join(buildInfoFolder, file)).toString("ascii"));
                const solcVersion = buildInfo.solcVersion;
                if (!solcVersionRegex.test(solcVersion)) {
                    continue;
                }

                if (!foundVersions.has(solcVersion)) {
                    versions.push(solcVersion);
                    foundVersions.add(solcVersion);
                }
            } catch (e: any) {
            }
        }

        return versions;
    }

    /** @returns List of contracts in SDBG project */
    getContractNames(): string[] {
        let result: string[] = [];

        if (!this.isOpen) {
            return result;
        }

        for (let fn of fs.readdirSync(this.contractsDir)) {
            let contractDir = path.join(this.contractsDir, fn);
            let contractJsonFile = path.join(contractDir, contractJsonFileName);

            if (!fs.existsSync(contractJsonFile)) {
                continue;
            }

            result.push(fn);
        }

        return result;
    }

    contractExists(contractName: string): boolean {
        for (let e of this.getContractNames()) {
            if (e.toLowerCase() === contractName.toLowerCase()) {
                return true;
            }
        }

        return false;
    }

    selectContract(contractName: string): void {
        this.contractMustExist(contractName);

        this._projectJson.update((content) => {
            content.selectedContract = contractName;
        });

        this.selectedContract = contractName;
    }

    getContractPath(contractName: string): string {
        this.contractMustExist(contractName);
        return path.join(this.contractsDir, contractName);
    }

    getContractMainSol(contractName: string): string | undefined {
        const fileExt = ['.t.sol', '.sol'];

        for (let ext of fileExt) {
            let fpath = path.join(this.getContractPath(contractName), `${contractName}${ext}`);
            if (fs.existsSync(fpath)) {
                return fpath;
            }
        }

        return undefined;
    }

    /**
     * @param contractName Name of contract that is part of this project
     * @param mergeWithProjectJson
     * @returns Contract's JsonFile object
     */
    getContractJson(contractName: string, mergeWithProjectJson: boolean = false): JsonFile<ContractJson> {
        this.contractMustExist(contractName);
        let jsonFile = path.join(this.contractsDir, contractName, contractJsonFileName);
        let result = new JsonFile<ContractJson>(jsonFile);
        if (mergeWithProjectJson) {
            result.parent = path.join(this.rootDir, projectJsonFileName);
        }
        result.readfile();
        return result;
    }

    getAvailContractName(): string {
        for (let i = 1; ; i++) {
            let name = `${entryPointContractName}${i}`;

            if (!this.contractExists(name)) {
                return name;
            }
        }
    }

    createNewContract(contractName: string): string {
        this.contractMustNotExist(contractName);

        // create contract directory
        let contractDir = path.join(this.contractsDir, contractName);
        if (!fs.existsSync(contractDir)) {
            fs.mkdirSync(contractDir);
        }

        // create contract json
        let contractJsonFile = path.join(contractDir, contractJsonFileName);
        let template = JSON.parse(JSON.stringify(contractJsonTemplate)) as ContractJson;
        if (this.solidityVersions.length > 0) {
            template.solc = this.solidityVersions[0];
        } else {
            template.solc = Tools.solidity.defaultVersion;
        }

        fs.writeFileSync(contractJsonFile, JSON.stringify(template, null, 4));

        // create solidity file for debugger entry point
        let solFilePath = path.join(contractDir, `${contractName}.t.sol`);
        fs.writeFileSync(solFilePath, entryPointContractTemplate);

        // if this is the first contract, select it
        this.selectDefaultContract();

        return solFilePath;
    }

    /** Open SDBG project */
    private _open() {
        if (this.isOpen) {
            throw new Error('already open');
        }

        // create root directory
        if (!fs.existsSync(this.rootDir)) {
            fs.mkdirSync(this.rootDir);
        }

        // open or create project json
        let projectJsonPath = path.join(this.rootDir, projectJsonFileName);
        if (!fs.existsSync(projectJsonPath)) {
            fs.writeFileSync(projectJsonPath, JSON.stringify(projectJsonTemplate, null, 4));
        }

        this._projectJson = new JsonFile(projectJsonPath);
        if (!this._projectJson.content.contractsDir) {
            this._projectJson.update((content) => {
                content.contractsDir = contractsDirName;
            });
        }

        // create contracts dir
        let contractsDirPath = path.join(this.rootDir, this._projectJson.content.contractsDir as string);
        if (!fs.existsSync(contractsDirPath)) {
            fs.mkdirSync(contractsDirPath);
        }

        // mark as open
        this.isOpen = true;
    }

    private contractMustExist(contractName: string): void {
        if (!this.contractExists(contractName)) {
            throw new Error(`contract doesn't exist: ${contractName}`);
        }
    }

    private contractMustNotExist(contractName: string): void {
        if (this.contractExists(contractName)) {
            throw new Error(`contract exists: ${contractName}`);
        }
    }

    private selectDefaultContract(): void {
        if (this._projectJson.content.selectedContract &&
            this.contractExists(this._projectJson.content.selectedContract)) {
            this.selectContract(this._projectJson.content.selectedContract);
        } else {
            let allContractsNames = this.getContractNames();
            if (allContractsNames.length > 0) {
                let first = allContractsNames.values().next().value;
                this.selectContract(first);
            }
        }
    }
}

class ProjectBase {
    private _extension?: vscode.ExtensionContext;
    private _project?: SdbgProject;

    get current(): SdbgProject | undefined {
        return this._project;
    }

    /** Called when VSCode extension is activated */
    init(context: vscode.ExtensionContext) {
        this._extension = context;
        this.openWorkspaceProject();

        if (this.current?.projectJson.content.autoOpen) {
            this.openSelectedContractInEditor();
        }
    }

    private openWorkspaceProject(): boolean {
        let result = false;
        vscode.workspace.workspaceFolders?.forEach(
            (workspace: vscode.WorkspaceFolder) => {
                let rootDir = workspace.uri.fsPath;
                if (SdbgProject.exists(rootDir)) {
                    this._project = new SdbgProject(rootDir);
                    result = true;
                }
            }
        );
        return result;
    }

    private openSelectedContractInEditor() {
        if (this.current && this.current.selectedContractName) {
            let fpath = this.current.getContractMainSol(this.current.selectedContractName);
            if (fpath && fs.existsSync(fpath)) {
                vscode.workspace.openTextDocument(vscode.Uri.file(fpath)).then(document => {
                    vscode.window.showTextDocument(document);
                });
            }
        }
    }
}

export let Project = new ProjectBase();
