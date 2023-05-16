import {WebInstallerOptions} from "./IWebInstaller";

export interface engine_list_json_t {
    releases: string[]
}

export interface IEngineInstaller {
    baseDir: string
    sdbgExePath: string
    sdbgDllPath: string
    dotNetExePath: string
    installedVersion?: string
    latestVersion?: string
    previouslyInstalledVersions?: string[]

    initAsync(extensionPath: string): Promise<void>

    isInstalledAsync(): Promise<boolean>

    installAsync(options?: WebInstallerOptions): Promise<void>
}