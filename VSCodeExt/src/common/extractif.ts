import {ProgressBarCallback} from "./common";

export interface IExtract {
    extract(toDir: string, progressBar?: ProgressBarCallback): Promise<void>
}
