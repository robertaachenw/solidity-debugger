import * as path from 'path';
import * as os from 'os';
import * as fs from 'fs';

export let APPHOME_PATH = path.join(os.homedir(), '.solidity-ide');

export let APPHOME_NAME_DOTNET = 'dotnet';
export let APPHOME_NAME_ENGINE = 'engine';
export let APPHOME_NAME_SOLC = 'solc';
export let APPHOME_NAME_CONFIG = 'config';
export let APPHOME_NAME_DLCACHE = 'dlcache';

function apphomeInit() {
    if (!fs.existsSync(APPHOME_PATH)) {
        fs.mkdirSync(APPHOME_PATH);
    }
}


export function apphomeMkdir(dirname: string, innerDir?: string): string {
    apphomeInit();

    let dirpath = path.join(APPHOME_PATH, dirname);
    if (!fs.existsSync(dirpath)) {
        fs.mkdirSync(dirpath);
    }

    if (innerDir) {
        let innerPath = path.join(dirpath, innerDir);
        if (!fs.existsSync(innerPath)) {
            fs.mkdirSync(innerPath);
        }
        return innerPath;
    }

    return dirpath;
}

export function apphomeTempDir(): string {
    return apphomeMkdir('temp');
}
