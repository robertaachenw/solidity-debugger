import * as fs from 'fs';
import * as path from 'path';
import * as process from 'process';
import * as util from 'util';
import {ZipExtract} from '../../../common/extractzip';
import {APPHOME_NAME_ENGINE, apphomeMkdir} from '../../../common/apphome';
import {ProgressBarCallback, sformat, versionToNumber} from '../../../common/common';
import {IEngineInstaller} from '../../../common/IEngineInstaller';
import {ENGINE_URLS, HOMEPAGE_JSON_URL, IReleaseJson, PORTABLE_DIR} from '../../../common/hardcodedURLs';
import {DownloadCache} from '../../../common/DownloadCache';
import {WebInstallerOptions} from '../../../common/IWebInstaller';
import {engineVersionFileName} from "../../../consts";

interface EngineVersionJson {
    installedVersion?: string
    previouslyInstalledVersions: string[]
}

export class EngineInstaller implements IEngineInstaller {
    private _basedir: string | undefined = undefined;

    get baseDir(): string {
        if (this._basedir === undefined) {
            this._basedir = apphomeMkdir(APPHOME_NAME_ENGINE);
        }

        return this._basedir;
    }

    get sdbgExePath(): string {
        if (process.platform === 'win32') {
            return path.join(this.baseDir, 'SolidityDebugger.exe');
        } else {
            return path.join(this.baseDir, 'SolidityDebugger');
        }
    }

    get sdbgDllPath(): string {
        return path.join(this.baseDir, 'SolidityDebugger.dll');
    }

    get dotNetExePath(): string {
        if (process.platform === 'win32') {
            return path.join(this.baseDir, 'dotnet.exe');
        } else {
            return path.join(this.baseDir, 'dotnet');
        }
    }

    get previouslyInstalledVersions(): string[] {
        return this.readEngineVersionJson().previouslyInstalledVersions;
    }

    get latestVersion(): string {
        let releaseJson = JSON.parse(DownloadCache.readFileFromCache(HOMEPAGE_JSON_URL)) as IReleaseJson;
        return releaseJson.latestRelease;
    }

    get installedVersion(): string | undefined {
        return this.readEngineVersionJson().installedVersion;
    }

    private static getDownloadUrls(releaseVersion: string): string[] {
        for (let e of ENGINE_URLS) {
            if (e.urls.length === 0) {
                continue;
            }
            if (e.platform !== process.platform) {
                continue;
            }
            if (e.arch !== process.arch) {
                continue;
            }

            let result = [];
            for (let url of e.urls) {
                result.push(sformat(url, releaseVersion));
            }

            return result;
        }

        throw new Error(util.format('No download URL for %s/%s', process.platform, process.arch));
    }

    async initAsync(extensionPath: string): Promise<void> {
        DownloadCache.init(path.join(extensionPath, PORTABLE_DIR));
    }

    async isInstalledAsync(): Promise<boolean> {
        return this.isInstalled();
    }

    async installAsync(options?: WebInstallerOptions): Promise<void> {
        if (options === undefined) {
            options = {};
        }

        let installedVersion = this.installedVersion;
        let latestVersion = this.latestVersion;

        if (options.overwrite !== true) {
            if (options.engineVersion !== undefined && options.engineVersion === installedVersion) {
                throw new Error(`engine ${options.engineVersion} is already installed`);
            } else if (options.engineVersion === undefined && installedVersion !== undefined && versionToNumber(installedVersion) >= versionToNumber(latestVersion)) {
                throw new Error(`latest engine is already installed (${installedVersion})`);
            }
        }

        let versionToInstall = this.latestVersion;
        if (options.engineVersion !== undefined) {
            versionToInstall = options.engineVersion;
        }

        let progressBar: ProgressBarCallback | undefined = options ? options.progressBar : undefined;
        let archiveFilePath: string | undefined = undefined;
        let downloadUrl = '';

        for (let url of EngineInstaller.getDownloadUrls(versionToInstall)) {
            downloadUrl = url;
            DownloadCache.deleteUrlFromCache(downloadUrl);
            DownloadCache.deleteFileFromCache(downloadUrl);
            archiveFilePath = await DownloadCache.getFileAsync(downloadUrl, undefined, options.progressBar);
            if (archiveFilePath) {
                break;
            }
        }

        if (!archiveFilePath) {
            throw new Error(`Download failed: ${downloadUrl}`);
        }

        let extract = new ZipExtract(archiveFilePath);
        let engineDir = apphomeMkdir(APPHOME_NAME_ENGINE);
        this.setInstalledVersion(undefined);
        await extract.extract(engineDir, progressBar);
        for (let filePath of [this.sdbgExePath, this.dotNetExePath]) {
            try {
                fs.chmodSync(filePath, 0o755);
            } catch {
            }
        }
        this.setInstalledVersion(versionToInstall);
    }

    private readEngineVersionJson(): EngineVersionJson {
        try {
            let jsonFilePath = path.join(this.baseDir, engineVersionFileName);
            return JSON.parse(fs.readFileSync(jsonFilePath, 'utf8')) as EngineVersionJson;
        } catch {
            let initial = {
                installedVersion: undefined, previouslyInstalledVersions: []
            } as EngineVersionJson;

            if (this.isInstalled()) {
                initial.installedVersion = '1.0.0';
                initial.previouslyInstalledVersions.push(initial.installedVersion);
            }

            return initial;
        }
    }

    private writeEngineVersionJson(content: EngineVersionJson) {
        let jsonFilePath = path.join(this.baseDir, engineVersionFileName);
        fs.writeFileSync(jsonFilePath, JSON.stringify(content));
    }

    private setInstalledVersion(version: string | undefined) {
        let jsonContent = this.readEngineVersionJson();
        let writeFile = false;

        if (jsonContent.installedVersion !== version) {
            jsonContent.installedVersion = version;
            writeFile = true;
        }

        if (version !== undefined) {
            if (jsonContent.previouslyInstalledVersions.indexOf(version) < 0) {
                jsonContent.previouslyInstalledVersions.push(version);
                writeFile = true;
            }
        }

        if (writeFile) {
            this.writeEngineVersionJson(jsonContent);
        }
    }

    private isInstalled(): boolean {
        let requiredFiles = [this.sdbgExePath, this.sdbgDllPath];
        for (let fpath of requiredFiles) {
            try {
                let st = fs.statSync(fpath);
                if (!st.isFile) {
                    throw new Error('not a file');
                }
            } catch {
                return false;
            }
        }

        return true;
    }
}

async function test(arg: string): Promise<void> {
    let inst = new EngineInstaller();
    console.log(`initAsync('${arg}')`);
    await inst.initAsync(arg);
    console.log(`installedVersion = '${inst.installedVersion}'`);
    console.log(`latestVersion = '${inst.latestVersion}'`);
    console.log(`previouslyInstalledVersions = ${inst.previouslyInstalledVersions}`);
    // await inst.installAsync({
    //     progressBar: (operation: string, percent: number) => {
    //         console.log(`operation=${operation} percent=${percent}`);
    //     },
    //     // engineVersion: '1.0.1'
    // });
}

let testEnv = process.env.SDBG_ENGINE_INSTALLER_TEST;
if (testEnv !== undefined) {
    test(testEnv);
}

module.exports = new EngineInstaller();
