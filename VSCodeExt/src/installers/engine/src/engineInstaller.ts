import * as fs from 'fs';
import * as path from 'path';
import * as process from 'process';
import * as util from 'util';
import {ZipExtract} from '../../../common/extractzip';
import {APPHOME_NAME_ENGINE, apphomeMkdir} from '../../../common/apphome';
import {ProgressBarCallback, sformat} from '../../../common/common';
import {IEngineInstaller} from '../../../common/IEngineInstaller';
import {ENGINE_URLS, HOMEPAGE_JSON_URL, IReleaseJson, PORTABLE_DIR} from '../../../common/hardcodedURLs';
import {DownloadCache} from '../../../common/DownloadCache';
import {WebInstallerOptions} from '../../../common/IWebInstaller';

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
        DownloadCache.builtinPath = path.join(extensionPath, PORTABLE_DIR);
    }

    async isInstalledAsync(): Promise<boolean> {
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

    async installAsync(options?: WebInstallerOptions): Promise<void> {
        if (options === undefined) {
            options = {};
        }
        if ((await this.isInstalledAsync()) && options.overwrite !== true) {
            throw new Error('already installed');
        }

        let progressBar: ProgressBarCallback | undefined = options ? options.progressBar : undefined;

        let releaseJson = JSON.parse(DownloadCache.readFileFromCache(HOMEPAGE_JSON_URL)) as IReleaseJson;
        let archiveFilePath: string | undefined = undefined;
        let lastUrl = '';

        for (let url of EngineInstaller.getDownloadUrls(releaseJson.latestRelease)) {
            archiveFilePath = await DownloadCache.getFileAsync(url, undefined, options.progressBar);
            if (archiveFilePath) {
                break;
            }
        }

        if (!archiveFilePath) {
            throw new Error(`Download failed: ${lastUrl}`);
        }

        let extract = new ZipExtract(archiveFilePath);
        let engineDir = apphomeMkdir(APPHOME_NAME_ENGINE);
        await extract.extract(engineDir, progressBar);

        for (let filePath of [this.sdbgExePath, this.dotNetExePath]) {
            try {
                fs.chmodSync(filePath, 0o755);
            } catch {
            }
        }
    }
}

module.exports = new EngineInstaller();
