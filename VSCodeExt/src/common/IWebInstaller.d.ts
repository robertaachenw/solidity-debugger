import {ProgressBarCallback} from "./common";

export interface WebInstallerOptions {
    solcVersion?: string

    overwrite?: boolean

    progressBar?: ProgressBarCallback
}
