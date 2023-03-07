import {WebInstallerOptions} from "./IWebInstaller";

export interface solc_build_info_t {
    path: string
    version: string
    build: string
    longVersion: string
    keccak256: string
    sha256: string
}

export interface solc_list_json_t {
    builds: solc_build_info_t[]
    releases: Map<string, string>
    latestRelease: string
}

export interface ISolcInstaller {
    supportedVersions: Set<string>
    defaultVersion: string

    initAsync(extensionPath: string): Promise<void>

    updateAsync(): Promise<boolean>

    solcJsPath(version: string): string

    isInstalled(version: string): boolean

    isInstalledAsync(version: string): Promise<boolean>

    installAsync(options?: WebInstallerOptions): Promise<void>
}