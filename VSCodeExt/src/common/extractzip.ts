import * as util from 'util';
import * as yauzl_promise from 'yauzl-promise';
import * as fs from 'fs';
import * as bigjs from 'big.js';
import * as _stream from 'stream';
import * as path from 'path';
import {IExtract} from './extractif';
import {ProgressBarCallback} from './common';

type ZipCallback = (entry: yauzl_promise.Entry, isdir: boolean, readStream?: _stream.Readable) => Promise<void>;

export class ZipExtract implements IExtract {
    private readonly _fpath: string;

    constructor(fpath: string) {
        if (!fs.existsSync(fpath)) {
            throw new Error(`file not found ${fpath}`);
        }
        this._fpath = fpath;
    }

    private static async createDirectory(dirpath: string): Promise<void> {
        if (!fs.existsSync(dirpath)) {
            fs.mkdirSync(dirpath, {recursive: true});
        } else {
            let st = await fs.promises.stat(dirpath);
            if (!st.isDirectory()) {
                throw new Error(`not a directory ${dirpath}`);
            }
        }
    }

    async extract(toDir: string, progressBar?: ProgressBarCallback): Promise<void> {
        let toDirStat = await fs.promises.stat(toDir);
        if (!toDirStat.isDirectory()) {
            throw new Error(`not a directory ${toDir}`);
        }

        let uncompressedSize = await this.getUncompressedSize();
        let bytesExtracted = new bigjs.Big(0);
        let percent = -1;

        await this.iterateZipFile(this._fpath, async (entry: yauzl_promise.Entry, isdir: boolean, readStream?: _stream.Readable) => {
            let fullPath = path.join(toDir, entry.fileName);

            if (isdir) {
                await ZipExtract.createDirectory(fullPath);
            } else if (readStream) {
                await ZipExtract.createDirectory(path.dirname(fullPath));

                const pipeline = util.promisify(_stream.pipeline);
                await pipeline(readStream, fs.createWriteStream(fullPath));

                if (fullPath.indexOf('/.bin') >= 0) {
                    try {
                        fs.chmodSync(fullPath, 0o755);
                    } catch {
                    }
                }

                bytesExtracted = bytesExtracted.add(entry.uncompressedSize);

                if (!uncompressedSize.eq(0)) {
                    let newPercent = Math.floor(bytesExtracted.mul(100).div(uncompressedSize).toNumber());
                    if (percent !== newPercent) {
                        percent = newPercent;
                        if (progressBar) {
                            progressBar(this._fpath, percent);
                        }
                    }
                }
            }
        });
    }

    private async iterateZipFile(fpath: string, callback: ZipCallback) {
        let zipfile = await yauzl_promise.open(fpath);

        await zipfile.walkEntries(async (entry: yauzl_promise.Entry) => {
            if (entry.fileName.endsWith('/')) {
                await callback(entry, true);
            } else {
                let readStream = await zipfile.openReadStream(entry);
                await callback(entry, false, readStream);
            }
        });
    }

    private async getUncompressedSize(): Promise<bigjs.Big> {
        let totalSize = new bigjs.Big(0);

        await this.iterateZipFile(this._fpath, async (entry: yauzl_promise.Entry, isdir: boolean, readStream?: _stream.Readable) => {
            if (isdir) {
                return;
            }
            totalSize = totalSize.add(entry.uncompressedSize);
        });

        return totalSize;
    }
}
