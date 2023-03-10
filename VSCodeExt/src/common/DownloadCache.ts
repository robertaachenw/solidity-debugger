import {APPHOME_NAME_DLCACHE, apphomeMkdir} from "./apphome";
import {DownloaderConfig, DownloaderReport, NodeJsFileDownloader} from "./Downloader";
import * as path from 'path';
import * as fs from 'fs';
import * as process from 'process';
import {ProgressBarCallback} from "./common";

const LAST_MODIFIED_JSON = 'lastModified.json';
const TEMP_FILE_EXT = '.tmp';

export type CacheFlag = 'cache-only' | 'no-optimize' | 'cache-first' | 'no-cache';

interface ILocalFile {
    basename: string
    fpath: string
    exists: boolean
}

class DownloadCacheBase {
    public builtinPath?: string;
    private readonly _cacheDir: string;
    private readonly _lastModifiedJson: string;

    constructor() {
        this._cacheDir = apphomeMkdir(APPHOME_NAME_DLCACHE);
        this._lastModifiedJson = path.join(this._cacheDir, LAST_MODIFIED_JSON);

        if (!fs.existsSync(this._lastModifiedJson)) {
            fs.writeFileSync(this._lastModifiedJson, '{}');
        }

        this.deleteTempFiles();
    }

    private deleteTempFiles() {
        try {
            fs.readdirSync(this._cacheDir).forEach((filename) => {
                if (filename.endsWith(TEMP_FILE_EXT)) {
                    try {
                        fs.unlinkSync(path.join(this._cacheDir, filename));
                    } catch {
                    }
                }
            });

        } catch {
        }
    }

    getFileFromCache(url: string): string | undefined {
        let localFile = this.getLocalFile(url);

        if (localFile.exists) {
            return localFile.fpath;
        }

        return undefined;
    }

    readFileFromCache(url: string): string {
        let fpath = this.getFileFromCache(url);
        if (!fpath) {
            throw new Error(`Not cached: ${url}`);
        }

        return fs.readFileSync(fpath, 'utf8');
    }

    async getFileAsync(url: string, flagsList?: CacheFlag[], progressBar?: ProgressBarCallback, verifyCallback?: (url: string, localFile: string, inCache: boolean) => Promise<boolean>): Promise<string | undefined> {
        let flags = new Set<CacheFlag>(flagsList);

        let localFile = this.getLocalFile(url);

        if (flags.has('no-cache')) {
            localFile.exists = false;
        } else {
            if ((flags.has('cache-only') || flags.has('cache-first'))) {
                if (localFile.exists) {
                    if (verifyCallback !== undefined && !await verifyCallback(url, localFile.fpath, true)) {
                        throw new Error('verification failed');
                    }

                    return localFile.fpath;
                }

                if (flags.has('cache-only')) {
                    return undefined;
                }
            }
        }

        let disableHttps = (this.builtinPath !== undefined && fs.existsSync(path.join(this.builtinPath, 'disable.https')));

        let tempBasename = `${new Date().valueOf()}${process.hrtime()[1]}${TEMP_FILE_EXT}`;
        let tempFpath = path.join(this._cacheDir, tempBasename);

        let serverLastModified: string | undefined = undefined;
        let percent = -1;

        let dlConfig: DownloaderConfig = {
            url: url,
            directory: this._cacheDir,
            fileName: tempBasename,
            disableHttps: disableHttps,
            onResponse: (r) => {
                serverLastModified = r.headers["last-modified"];
            },
            onProgress: (percentFloat: string, chunk: object, remainingSize: number) => {
                let newPercent = parseInt(percentFloat, 10);
                if (percent !== newPercent) {
                    percent = newPercent;
                    if (progressBar) {
                        progressBar(url, percent);
                    }
                }
            },
            headers: {
                'User-Agent': 'VSCode', 'Cache-Control': 'no-cache',
            },
        };

        let knownLastModified = this.loadLastModified(url);
        if (dlConfig.headers && knownLastModified) {
            dlConfig.headers['If-Modified-Since'] = knownLastModified;
        }

        let err = '';

        try {
            let dl = new NodeJsFileDownloader(dlConfig);
            let dlResult: DownloaderReport;
            try {
                dlResult = await dl.download();
            } catch (exception) {
                let msg = `${exception}`;

                if (msg.includes('status code 304') && localFile.fpath) {
                    if (verifyCallback !== undefined && !await verifyCallback(url, localFile.fpath, true)) {
                        throw new Error('verification failed');
                    }

                    return localFile.fpath;
                }

                throw exception;
            }

            if (dlResult.downloadStatus !== 'COMPLETE') {
                throw new Error(dlResult.downloadStatus);
            }

            if (verifyCallback !== undefined && !await verifyCallback(url, tempFpath, false)) {
                throw new Error('verification failed');
            }

            let permanentPath = path.join(this._cacheDir, localFile.basename);
            if (fs.existsSync(permanentPath)) {
                fs.unlinkSync(permanentPath);
            }

            fs.renameSync(tempFpath, permanentPath);

            if (serverLastModified) {
                this.storeLastModified(url, serverLastModified);
            }

            return permanentPath;
        } catch (exception2) {
            err = `${exception2}`;
        }

        if (fs.existsSync(tempFpath) && err.indexOf('file is locked') < 0) {
            try {
                fs.unlinkSync(tempFpath);
            } catch {
            }
        }

        if (localFile.exists) {
            return localFile.fpath;
        }
        return undefined;
    }

    private storeLastModified(url: string, date: string): void {
        let content = JSON.parse(fs.readFileSync(this._lastModifiedJson, 'utf8'));
        content[url] = date;
        fs.writeFileSync(this._lastModifiedJson, JSON.stringify(content, null, 4));
    }

    private loadLastModified(url: string): string | undefined {
        let content = JSON.parse(fs.readFileSync(this._lastModifiedJson, 'utf8'));
        return content[url];
    }

    private getLocalFile(url: string): ILocalFile {
        let basename = url.split('/').pop();
        if (!basename) {
            throw new Error('invalid url');
        }

        let fpath = path.join(this._cacheDir, basename);
        if (fs.existsSync(fpath)) {
            return {
                basename: basename, fpath: fpath, exists: true
            };
        }

        if (this.builtinPath) {
            fpath = path.join(this.builtinPath, basename);
            if (fs.existsSync(fpath)) {
                return {
                    basename: basename, fpath: fpath, exists: true
                };
            }
        }

        return {
            basename: basename, fpath: path.join(this._cacheDir, basename), exists: false
        };
    }
}

export let DownloadCache = new DownloadCacheBase();
