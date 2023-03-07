import {WebInstallerOptions} from "./IWebInstaller";

export interface IEngineInstaller {
    baseDir: string
    sdbgExePath: string
    sdbgDllPath: string
    dotNetExePath: string

    initAsync(extensionPath: string): Promise<void>

    isInstalledAsync(): Promise<boolean>

    installAsync(options?: WebInstallerOptions): Promise<void>
}