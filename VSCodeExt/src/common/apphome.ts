import * as path from 'path';
import * as os from 'os';
import * as fs from 'fs';

export let APPHOME_PATH = path.join(os.homedir(), '.solidity-ide');

export let APPHOME_NAME_DOTNET = 'dotnet';
export let APPHOME_NAME_ENGINE = 'engine';
export let APPHOME_NAME_SOLC = 'solc';
export let APPHOME_NAME_CONFIG = 'config';
export let APPHOME_NAME_DLCACHE = 'dlcache';
export let APPHOME_NAME_INSTALL = 'install';
export let APPHOME_NAME_UPDATE = 'update';


function mkdirIfMissing(dirpath: string) {
    if (fs.existsSync(dirpath)) {
        return;
    }

    try {
        fs.mkdirSync(dirpath);
    } catch {
    }

    if (!fs.existsSync(dirpath)) {
        throw new Error(`cannot mkdir ${dirpath}`);
    }
}

export function apphomeMkdir(dirname: string, innerDir?: string): string {
    mkdirIfMissing(APPHOME_PATH);

    let dirpath = path.join(APPHOME_PATH, dirname);
    mkdirIfMissing(dirpath);

    if (innerDir) {
        let innerPath = path.join(dirpath, innerDir);
        mkdirIfMissing(innerPath);
        return innerPath;
    }

    return dirpath;
}

export function apphomeTempDir(): string {
    return apphomeMkdir('temp');
}
