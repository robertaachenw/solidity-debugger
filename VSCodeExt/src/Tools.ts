import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import * as util from 'util';
import * as properLockFile from 'proper-lockfile';
import {IEngineInstaller} from './common/IEngineInstaller';
import {ISolcInstaller} from './common/ISolcInstaller';
import {CacheFlag, DownloadCache} from './common/DownloadCache';
import {
    GITHUB_WEBINST_ENGINE_URL, GITHUB_WEBINST_SOLIDITY_URL, HOMEPAGE_JSON_URL, IReleaseJson, PORTABLE_DIR
} from './common/hardcodedURLs';
import {sformat, versionToNumber} from './common/common';
import {WebInstallerOptions} from "./common/IWebInstaller";
import {
    errInstallRequiresRestart,
    errInstallInProgress,
    updateIntervalMilliseconds,
    showSideBarCommandName, productName
} from "./consts";
import {APPHOME_NAME_INSTALL, APPHOME_NAME_UPDATE, apphomeMkdir} from "./common/apphome";

interface WebInstallers {
    engineInstaller: IEngineInstaller
    solcInstaller: ISolcInstaller
}

class ToolsBase {
    private _extension?: vscode.ExtensionContext;
    private _engineInstaller?: IEngineInstaller;
    private _solcInstaller?: ISolcInstaller;
    private _installDone: boolean;
    private _init: boolean;
    private _upgradeDeclined: boolean;

    constructor() {
        this._installDone = false;
        this._init = false;
        this._upgradeDeclined = false;
    }

    get engine(): IEngineInstaller {
        if (!this._engineInstaller) {
            throw new Error();
        }
        return this._engineInstaller;
    }

    get solidity(): ISolcInstaller {
        if (!this._solcInstaller) {
            throw new Error();
        }
        return this._solcInstaller;
    }

    private static async getWebInstallers(flags?: CacheFlag[]): Promise<WebInstallers> {
        let engineInstaller: IEngineInstaller | undefined = undefined;
        let solcInstaller: ISolcInstaller | undefined = undefined;

        await DownloadCache.getFileAsync(HOMEPAGE_JSON_URL, flags, undefined, async (url: string, localFile: string, inCache: boolean) => {
            if (flags && flags.indexOf('no-cache') >= 0) {
                let cacheFile = DownloadCache.getFileFromCache(url);
                if (cacheFile && fs.existsSync(cacheFile)) {
                    if (fs.readFileSync(localFile, 'utf8') === fs.readFileSync(cacheFile, 'utf8')) {
                        return false;
                    }
                }
            }

            let releaseJson = JSON.parse(fs.readFileSync(localFile, 'utf8')) as IReleaseJson;

            return await DownloadCache.getFileAsync(sformat(GITHUB_WEBINST_ENGINE_URL, releaseJson.latestRelease), flags, undefined, async (url: string, enginePath: string, inCache: boolean) => {
                engineInstaller = require(enginePath);

                await DownloadCache.getFileAsync(sformat(GITHUB_WEBINST_SOLIDITY_URL, releaseJson.latestRelease), flags, undefined, async (url: string, solcPath: string, inCache: boolean) => {
                    solcInstaller = require(solcPath);
                    return true;
                });

                return true;
            }) !== undefined;
        });

        if (!engineInstaller || !solcInstaller) {
            throw new Error('Web installer download failed');
        }

        return {
            engineInstaller: engineInstaller, solcInstaller: solcInstaller
        };
    }

    public isInstallerRunning(): boolean {
        try {
            let installDir = apphomeMkdir(APPHOME_NAME_INSTALL);

            if (properLockFile.checkSync(installDir)) {
                return true;
            }
        } catch (exception) {
        }

        return false;
    }

    isInstalled(verbose: boolean): boolean {
        if (this._installDone) {
            return true;
        }

        if (!verbose) {
            return false;
        }

        try {
            let installDir = apphomeMkdir(APPHOME_NAME_INSTALL);

            if (properLockFile.checkSync(installDir)) {
                vscode.window.showInformationMessage(errInstallInProgress);
            } else {
                vscode.window.showInformationMessage(errInstallRequiresRestart);
            }
        } catch (exception) {
            vscode.window.showErrorMessage(`${exception}`);
        }

        return false;
    }

    async init(context: vscode.ExtensionContext): Promise<void> {
        this._extension = context;

        DownloadCache.init(path.join(context.extension.extensionPath, PORTABLE_DIR), true);

        await this.updateWebInstallers(await ToolsBase.getWebInstallers(['cache-first']));

        this.checkForUpdates().then(r => {
        });

        this._init = true;
    }

    async installEngine(engineVersion?: string, overwrite?: boolean): Promise<void> {
        let suffix = '';
        try {
            if (engineVersion !== undefined) {
                suffix = `${engineVersion}`;
            } else if (this.engine.latestVersion !== undefined) {
                suffix = `${this.engine.latestVersion}`;
            }
        } catch {
        }

        try {
            await this.installWithProgressBar(
                `Installing ${productName} ${suffix}`,
                this.engine.installAsync.bind(this.engine),
                overwrite,
                undefined,
                undefined,
                engineVersion);
        } catch (e) {
            vscode.window.showErrorMessage(util.format('%s', e));
            console.log(e);
        }
    }

    async installSolc(solcVersion: string, overwrite?: boolean): Promise<void> {
        await this.installWithProgressBar(`Installing Solidity Compiler v${solcVersion}`, this.solidity.installAsync.bind(this.solidity), overwrite, undefined, solcVersion);
    }

    askToUpgrade(runCommandIfDeclined?: string): boolean {
        if (!this.engine) {return false;}
        if (this._upgradeDeclined) {return false;}
        let latestVersionStr = this.engine.latestVersion;
        let currentVersionStr = this.engine.installedVersion;
        if (!latestVersionStr || !currentVersionStr) {return false;}

        let latestVersion = versionToNumber(latestVersionStr);
        let currentVersion = versionToNumber(currentVersionStr);
        
        if (currentVersion >= latestVersion) {return false;}

        this._upgradeDeclined = true;

        vscode.window.showInformationMessage(`Upgrade ${productName} to version ${latestVersionStr}?`, 'Yes', 'No').then(
            response => {
                switch (response) {
                    case 'Yes':
                        Tools.installEngine(undefined, true);
                        break;

                    case 'No':
                        if (runCommandIfDeclined) {
                            vscode.commands.executeCommand(runCommandIfDeclined);
                        }
                        break;

                    default:
                        break;
                }
            }
        );

        return true;
    }

    private static async lock(file: string): Promise<() => Promise<void>>
    {
        return properLockFile.lock(file, {stale: 45000});
    }

    async install(): Promise<boolean> {
        let result = false;

        try {
            let installDir = apphomeMkdir(APPHOME_NAME_INSTALL);
            let releaseLock = await ToolsBase.lock(installDir);

            result = await this.innerInstall();

            await releaseLock();
        } catch (e) {
            console.log(e);
        }

        return result;
    }

    private async innerInstall(): Promise<boolean> {
        try {
            if (!(await this.engine.isInstalledAsync())) {
                await this.installEngine();
            }

            if (!this.solidity.isInstalled(this.solidity.defaultVersion)) {
                await this.installSolc(this.solidity.defaultVersion);
            }

        } catch (e) {
            vscode.window.showErrorMessage(util.format('%s', e));
            console.log(e);
            return false;
        }

        this._installDone = true;
        return true;
    }

    private async checkForUpdates(): Promise<void> {
        let releaseLock: any = undefined;

        try {
            let updateDir = apphomeMkdir(APPHOME_NAME_UPDATE);
            releaseLock = await ToolsBase.lock(updateDir);
        } catch {
            releaseLock = undefined;
        }

        if (releaseLock === undefined) { // locked
            setTimeout(this.checkForUpdates.bind(this), 500);
            return;
        }

        setTimeout(this.checkForUpdates.bind(this), updateIntervalMilliseconds);
        try {
            await this.updateWebInstallers(await ToolsBase.getWebInstallers(['no-cache']));
        } catch (e) {
        }

        try {
            await releaseLock();
        } catch {
        }
    }

    private async updateWebInstallers(inst: WebInstallers, ignoreErrors?: boolean): Promise<void> {
        if (!this._extension) {
            throw new Error();
        }
        let extensionPath = this._extension.extension.extensionPath;

        try {
            await inst.engineInstaller.initAsync(extensionPath);
            this._engineInstaller = inst.engineInstaller;
        } catch (e) {
            if (!ignoreErrors) {
                throw new Error(`engineInstaller init failed: ${e}`);
            }
        }

        try {
            await inst.solcInstaller.initAsync(extensionPath);
            this._solcInstaller = inst.solcInstaller;
        } catch (e) {
            if (!ignoreErrors) {
                throw new Error(`solcInstaller init failed: ${e}`);
            }
        }
    }

    private async installWithProgressBar(
        title: string,
        installAsync: (options?: WebInstallerOptions) => Promise<void>,
        overwrite?: boolean,
        validateAsync?: () => Promise<boolean>,
        solcVersion?: string,
        engineVersion?: string,
        ): Promise<void> {
        await vscode.window.withProgress({
            location: vscode.ProgressLocation.Notification, cancellable: false, title: title
        }, async (progress) => {
            let prevOp = '';
            let prevPercent = 0;
            await installAsync({
                overwrite: overwrite, solcVersion: solcVersion, engineVersion: engineVersion, progressBar: (operation, percent) => {
                    if (operation !== prevOp) {
                        // started new operation
                        prevOp = operation;
                        prevPercent = percent;
                        progress.report({increment: percent});
                    } else if (percent !== prevPercent) {
                        progress.report({increment: percent - prevPercent});
                        prevPercent = percent;
                    }
                }
            });

            if (validateAsync) {
                progress.report({message: 'Validating'});
                if (!(await validateAsync())) {
                    throw new Error('Failed to execute .NET runtime');
                }
            }
        });
    }
}

export let Tools: ToolsBase = new ToolsBase();
