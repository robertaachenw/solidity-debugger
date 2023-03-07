import * as path from 'path';
import * as fs from 'fs';
import {sformat} from '../../../common/common';
import {ISolcInstaller, solc_list_json_t} from '../../../common/ISolcInstaller';
import {PORTABLE_DIR, SOLC_JS_URL, SOLC_LIST_JSON_URL} from '../../../common/hardcodedURLs';
import {CacheFlag, DownloadCache} from '../../../common/DownloadCache';
import {APPHOME_NAME_SOLC, apphomeMkdir} from '../../../common/apphome';
import {WebInstallerOptions} from '../../../common/IWebInstaller';


function objectToMap(obj: any): Map<string, string> {
    let result = new Map<string, string>();

    if (!obj) {
        return result;
    }

    for (let k in obj) {
        let v = obj[k] as string;
        result.set(k, v);
    }

    return result;
}


export class SolcInstaller implements ISolcInstaller {
    supportedVersions: Set<string>;
    defaultVersion: string;

    private _listjson: solc_list_json_t;

    constructor() {

        this._listjson = {
            builds: [],
            releases: new Map<string, string>(),
            latestRelease: '0.8.15'
        };

        this.defaultVersion = this._listjson.latestRelease;
        this.supportedVersions = new Set<string>([this.defaultVersion,]);
    }

    async initAsync(extensionPath: string): Promise<void> {
        DownloadCache.builtinPath = path.join(extensionPath, PORTABLE_DIR);
        await this.getListJson(['cache-first'], true);
    }

    async updateAsync(): Promise<boolean> {
        return (await this.getListJson(['no-cache'], true) !== undefined);
    }

    solcJsPath(version: string): string {
        if (!this._listjson.releases.has(version)) {
            throw new Error(`Unknown solidity version '${version}'`);
        }

        let fpath = path.join(apphomeMkdir(APPHOME_NAME_SOLC), version, 'solc.js');
        if (fs.existsSync(fpath)) {
            return fpath;
        }

        throw new Error(`Solidity compiler ${version} is not installed`);
    }

    isInstalled(version: string): boolean {
        try {
            this.solcJsPath(version);
            return true;
        } catch {
            return false;
        }
    }

    async isInstalledAsync(version: string): Promise<boolean> {
        return this.isInstalled(version);
    }

    async installAsync(options?: WebInstallerOptions): Promise<void> {
        if (!options) {
            throw new Error();
        }

        let version = options.solcVersion;
        if (!version) {
            throw new Error('WebInstallerOptions.solcVersion is required');
        }

        if (this.isInstalled(version) && options.overwrite !== true) {
            throw new Error('already installed');
        }

        let filename = this._listjson.releases.get(version);
        if (!filename) {
            await this.getListJson(undefined, true);
            filename = this._listjson.releases.get(version);
            if (!filename) {
                throw new Error(`Unknown solidity version '${version}'`);
            }
        }

        let url = sformat(SOLC_JS_URL, filename);

        let dlPath = await DownloadCache.getFileAsync(
            url,
            options.overwrite ? [] : ['cache-first'],
            options.progressBar
        );

        if (!dlPath) {
            throw new Error(`Download failed: ${url}`);
        }

        let finalPath = path.join(apphomeMkdir(APPHOME_NAME_SOLC, version), 'solc.js');

        try {
            fs.unlinkSync(finalPath);
        } catch {
            // ignored
        }

        fs.copyFileSync(dlPath, finalPath);
    }

    private async getListJson(flags?: CacheFlag[], reload?: boolean): Promise<solc_list_json_t> {
        let fpath = await DownloadCache.getFileAsync(SOLC_LIST_JSON_URL, flags);
        if (!fpath) {
            throw new Error(`Download failed: ${SOLC_LIST_JSON_URL}`);
        }

        let listjson = JSON.parse(fs.readFileSync(fpath, 'utf8')) as solc_list_json_t;
        listjson.releases = objectToMap(listjson.releases as any);

        if (reload) {
            this._listjson = listjson;
            this.defaultVersion = this._listjson.latestRelease;
            this.supportedVersions = new Set<string>(this._listjson.releases.keys());
        }

        return listjson;
    }
}

module.exports = new SolcInstaller();
