import http from 'http';

export interface DownloaderConfig {
    url: string
    directory?: string
    fileName?: string
    cloneFiles?: boolean
    skipExistingFileName?: boolean
    timeout?: number
    maxAttempts?: number
    headers?: object
    httpsAgent?: any
    proxy?: string
    shouldBufferResponse?: boolean
    useSynchronousMode?: boolean
    disableHttps?: boolean

    onProgress?(percentage: string, chunk: object, remaningSize: number): void

    onError?(e: Error): void

    onResponse?(r: http.IncomingMessage): boolean | void

    onBeforeSave?(finalName: string): string | void

    shouldStop?(e: Error): boolean | void
}

export interface DownloaderReport {
    downloadStatus: "COMPLETE" | "ABORTED"
    filePath: string | null
}


export class NodeJsFileDownloader {
    private _instance: any;

    constructor(config: DownloaderConfig) {
        let ctor = require('nodejs-file-downloader');

        if (config.disableHttps) {
            if (config.url.startsWith('https://')) {
                config.url = `http${config.url.substring(5)}`;
            }
        }

        config.disableHttps = undefined;
        this._instance = new ctor(config);
    }

    download(): Promise<DownloaderReport> {
        return this._instance.download();
    }

    cancel(): void {
        return this._instance.cancel();
    }
}
