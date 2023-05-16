export type NodePlatformType =
    'aix'
    | 'android'
    | 'darwin'
    | 'freebsd'
    | 'haiku'
    | 'linux'
    | 'openbsd'
    | 'sunos'
    | 'win32'
    | 'cygwin'
    | 'netbsd';
export type NodeArchType = 'arm' | 'arm64' | 'ia32' | 'mips' | 'mipsel' | 'ppc' | 'ppc64' | 's390' | 's390x' | 'x64';
export type ProgressBarCallback = (operation: string, percent: number) => void;

export function sformat(str: string, ...args: string[]): string {
    return str.replace(/{(\d+)}/g, (match, index) => args[index] || '');
}

export function versionToNumber(version: string) {
    let result = 0;

    const shift = 8;
    const minValue = 0;
    const maxValue = (1 << shift) - 1;

    let parts = version.split('.');
    for (let i = 0; i < parts.length; i++) {
        let part = Number(parts[i]);
        if (part >= minValue && part <= maxValue) {
            result += part;
            if (i !== parts.length - 1) {
                result <<= shift;
            }
        } else {
            throw new Error('bad input');
        }
    }

    return result;
}
