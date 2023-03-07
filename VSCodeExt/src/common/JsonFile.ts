import * as fs from 'fs';
import * as deepdiff from 'deep-diff';

export class JsonFile<T> {
    public parent?: string;
    private readonly _fpath: string;

    constructor(fpath: string) {
        this._fpath = fpath;
    }

    get data(): T {
        let data = this.readfile();

        let pdata = this.parentData;
        if (pdata !== undefined) {
            return {...pdata, ...data};
        }

        return data;
    }

    get content(): T {
        return this.data;
    }

    private get parentData(): T | undefined {
        if (this.parent !== undefined && fs.existsSync(this.parent)) {
            return this.readfile(this.parent);
        }

        return undefined;
    }

    readfile(fpath?: string): T {
        return JSON.parse(fs.readFileSync((fpath === undefined) ? this._fpath : fpath, 'utf8')) as T;
    }

    update(callback: (content: T) => any): void {
        let dataPure = this.readfile();

        let pdataOrig = this.data;
        let pdataModified = JSON.parse(JSON.stringify(pdataOrig));

        let callbackResult = callback(pdataModified);
        if (callbackResult) {
            pdataModified = callbackResult;
        }

        let modified = false;
        let diffs = deepdiff.diff<T>(pdataOrig, pdataModified);
        if (diffs !== undefined) {
            for (let diff of diffs) {
                this.copyMissingDiffPath(dataPure, pdataModified, diff.path);
                deepdiff.applyChange(dataPure, pdataModified, diff);
                modified = true;
            }
        }

        if (modified) {
            fs.writeFileSync(this._fpath, JSON.stringify(dataPure, null, 4));
        }
    }

    private copyMissingDiffPath(dataPure: T, pdataModified: T, path?: any[] | undefined) {
        if (!path) {
            return;
        }

        let src = pdataModified as any;
        let dest = dataPure as any;

        for (let k of path) {
            if (src[k] === undefined) {
                break;
            }

            if (dest[k] === undefined) {
                dest[k] = JSON.parse(JSON.stringify(src[k]));
                break;
            }

            src = src[k];
            dest = dest[k];
        }
    }
}
