import {IExtract} from "./extractif";
import * as fs from 'fs';
import * as targz from 'targz';
import {ProgressBarCallback} from "./common";

export class TarGzExtract implements IExtract {
    private readonly _fpath: string;

    constructor(fpath: string) {
        if (!fs.existsSync(fpath)) {
            throw new Error(`file not found ${fpath}`);
        }
        this._fpath = fpath;
    }

    async extract(
        toDir: string,
        progressBar?: ProgressBarCallback
    ): Promise<void> {
        if (progressBar) {
            progressBar(this._fpath, 0);
        }

        targz.decompress(
            {
                src: this._fpath,
                dest: toDir
            }
        );

        if (progressBar) {
            progressBar(this._fpath, 100);
        }
    }
}
