import {ProgressBarCallback} from "./common";

export interface WebInstallerOptions {
    solcVersion?: string

    engineVersion?: string

    overwrite?: boolean

    progressBar?: ProgressBarCallback
}
