export const solcVersionDefault = '0.8.20';

export const engineVersionFileName = 'engine-version.json';

export const dateFormat = 'YYYY-MM-DD HH:mm:ss';
export const projectHistoryFile = 'user-project-history.json';
export const projectJsonFileName = 'dbg.project.json';

export const contractsDirName = 'contracts-dbg';
export const contractJsonFileName = 'dbg.contract.json';

export const hardhatConfigTs = 'hardhat.config.ts';
export const hardhatConfigJs = 'hardhat.config.js';

export const entryPointContractName = 'DbgEntry';
export const entryPointFullName = `${entryPointContractName}`;
export const entryPointContractTemplate = `// SPDX-License-Identifier: MIT
pragma solidity >= 0.4.21 < 0.9.0;

contract ${entryPointContractName} {
    event EvmPrint(string);

    constructor() {
        emit EvmPrint("${entryPointContractName}.constructor");

        // Here you can either deploy your contracts via \`new\`, eg:
        //  Counter counter = new Counter();
        //  counter.increment();

        // or interact with an existing deployment by specifying a \`fork\` url in \`${projectJsonFileName}\`
        // eg:
        //  ICounter counter = ICounter(0x12345678.....)
        //  counter.increment(); 
        //
        // If you have correct symbols (\`artifacts\`) for the deployed contract, you can step-into calls.

        uint256 abc = 123;
        uint256 def = abc + 5;

        emit EvmPrint("${entryPointContractName} return");
    }
}`;
export const productName = 'Solidity Debugger';

export const updateIntervalMilliseconds = 2 * 3600 * 1000;

// vscode command names
export const buildCommandName = 'sdbg.build';
export const newTestCommandName = 'sdbg.newTest';
export const newProjectCommandName = 'sdbg.newProject';
export const openProjectCommandName = 'sdbg.openProject';
export const projectSettingsCommandName = 'sdbg.projectSettings';
export const debugCommandName = 'sdbg.debug';
export const checkForUpdatesCommandName = 'sdbg.update';
export const docsCommandName = 'sdbg.docs';
export const selectContractCommandName = 'sdbg.selectContract';
export const nopCommandName = 'sdbg.nop';
export const showSideBarCommandName = 'sdbg.sdsMainMenu.focus';

export const errNoOpenProject = `${productName}: No open project. Please open a project first.`;
export const errNoSelectedContract = `${productName}: No contract selected. Please select an entry-point contract for the debugger. See button at the bottom of the window.`;
export const errInstallInProgress = `${productName} is being installed...`;
export const errInstallRequiresRestart = `${productName}: Please restart VS Code to complete installation`;
export interface ContractJsonFork {
    enable: boolean
    url: string
    blockNumber: number
}


export interface ContractJsonSymbolsEtherscan {
    url: string
    apiKey: string
    contractAddrs: string[]
}


export interface ContractJsonSymbols {
    hardhat?: { projectPaths: string[] }
    etherscan?: ContractJsonSymbolsEtherscan
}

export interface SetupStep {
    cmdline: string
    expectedOutput?: string
}

export interface ContractJson {
    solc?: string
    evm?: string
    entryPoint?: string
    sourceDirs?: string[]
    fork?: ContractJsonFork
    symbols?: ContractJsonSymbols
    preDebugSteps?: SetupStep[]
    preBuildSteps?: SetupStep[]
    breakOnEntry?: boolean
    verbose?: boolean
}

export interface ProjectJson extends ContractJson {
    contractsDir?: string
    selectedContract?: string
    autoOpen?: boolean
}

export const projectJsonTemplate: ProjectJson = {
    contractsDir: contractsDirName
};

export const projectJsonTemplateStr: string = JSON.stringify(projectJsonTemplate, null, 4);

export const contractJsonTemplate: ContractJson = {
    solc: solcVersionDefault, sourceDirs: ['.'], entryPoint: entryPointFullName
};

