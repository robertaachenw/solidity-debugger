let buttonAddTest = document.getElementById('buttonAddTest');
let buttonPreBuildAddStep = document.getElementById('buttonPreBuildAddStep');
let buttonPreDebugAddStep = document.getElementById('buttonPreDebugAddStep');
let buttonRemoveTest = document.getElementById('buttonRemoveTest');
let buttonCancel = document.getElementById('buttonCancel');
let buttonSave = document.getElementById('buttonSave');
let buttonApply = document.getElementById('buttonApply');
let chkBreakOnEntry = document.getElementById('chkBreakOnEntry');
let chkVerbose = document.getElementById('chkVerbose');
let chkExecutionTrace = document.getElementById('chkExecutionTrace');
let chkFork = document.getElementById('chkFork');
let chkHardhat = document.getElementById('chkHardhat');
let dropdownContractNames = document.getElementById('dropdownContractNames');
let dropdownSolVer = document.getElementById('dropdownSolVer');
let dropdownEvmVer = document.getElementById('dropdownEvmVer');
let gridPreBuildSteps = document.getElementById('gridPreBuildSteps');
let gridPreDebugSteps = document.getElementById('gridPreDebugSteps');
let inputForkBlock = document.getElementById('inputForkBlock');
let inputForkUrl = document.getElementById('inputForkUrl');
let inputHardhatPath = document.getElementById('inputHardhatPath');
let inputPreBuildCmdline = document.getElementById('inputPreBuildCmdline');
let inputPreBuildExpectedOutput = document.getElementById('inputPreBuildExpectedOutput');
let inputPreDebugCmdline = document.getElementById('inputPreDebugCmdline');
let inputPreDebugExpectedOutput = document.getElementById('inputPreDebugExpectedOutput');
let inputProjectRoot = document.getElementById('inputProjectRoot');
let inputSourceDirs = document.getElementById('inputSourceDirs');
let inputEntryPoint = document.getElementById('inputEntryPoint');
let tabCompiler = document.getElementById('tab-1');
let tabDebugger = document.getElementById('tab-2');

const vscode = acquireVsCodeApi();
let solidityVersions = [];
let contractNames = [];
let jsonPack = {};
let selectedContractJson = {};
let removeId = 0;
let soliditySupportedEvms = {};
const defaultEvm = '(default)';

function setGuiDisabledEx(disableGui) {
    buttonAddTest.disabled = disableGui;
    buttonPreBuildAddStep.disabled = disableGui;
    buttonPreDebugAddStep.disabled = disableGui;
    buttonRemoveTest.disabled = disableGui;
    buttonCancel.disabled = disableGui;
    buttonSave.disabled = disableGui;
    buttonApply.disabled = disableGui;
    chkBreakOnEntry.disabled = disableGui;
    chkVerbose.disabled = disableGui;
    chkExecutionTrace.disabled = disableGui;
    chkFork.disabled = disableGui;
    chkHardhat.disabled = disableGui;
    dropdownContractNames.disabled = disableGui;
    dropdownSolVer.disabled = disableGui;
    dropdownEvmVer.disabled = disableGui;
    gridPreBuildSteps.disabled = disableGui;
    gridPreDebugSteps.disabled = disableGui;
    inputForkBlock.disabled = disableGui;
    inputForkUrl.disabled = disableGui;
    inputHardhatPath.disabled = disableGui;
    inputPreBuildCmdline.disabled = disableGui;
    inputPreBuildExpectedOutput.disabled = disableGui;
    inputPreDebugCmdline.disabled = disableGui;
    inputPreDebugExpectedOutput.disabled = disableGui;
    inputProjectRoot.disabled = disableGui;
    inputSourceDirs.disabled = disableGui;
    inputEntryPoint.disabled = disableGui;
}

function guiDisable() {
    setGuiDisabledEx(true);
}

function guiEnable() {
    setGuiDisabledEx(false);
}

function onLoad() {
    setVSCodeMessageListener();
    vscode.postMessage({request: 'init'});
}


function updateEvmDropDown(selectedSolc, selectedEvm) {
    dropdownEvmVer.innerHTML = `<vscode-option>${defaultEvm}</vscode-option>`;

    let solcSupportedEvms = soliditySupportedEvms[selectedSolc] ?? [];

    let selectedEvmFinal = selectedEvm ?? defaultEvm;
    if (selectedEvmFinal !== defaultEvm && solcSupportedEvms.length > 0 && solcSupportedEvms.indexOf(selectedEvmFinal) < 0)
        {selectedEvmFinal = defaultEvm;}

    dropdownEvmVer.innerHTML = `<vscode-option>${selectedEvmFinal}</vscode-option>`;

    if (selectedEvmFinal !== defaultEvm) {
        dropdownEvmVer.innerHTML += `<vscode-option>${defaultEvm}</vscode-option>`;
    }

    for (let value of solcSupportedEvms) {
        if (value === selectedEvmFinal) {continue;}
        dropdownEvmVer.innerHTML += `<vscode-option>${value}</vscode-option>`;
    }
}


function loadContract(selectedContractName) {
    let contractJson = jsonPack['contractJsonMerged'][selectedContractName];
    if (!contractJson) {
        contractJson = jsonPack['projectJson'];
        if (!contractJson) {
            contractJson = {};
        }
    }

    selectedContractJson = contractJson;

    tabCompiler.innerHTML = `Compiling ${selectedContractName}`;
    tabDebugger.innerHTML = `Debugging ${selectedContractName}`;

    dropdownEvmVer.innerHTML = `<vscode-option>${defaultEvm}</vscode-option>`;

    let solcVer = contractJson['solc'];
    if (solcVer) {
        updateEvmDropDown(solcVer, contractJson['evm']);
        dropdownSolVer.innerHTML = `<vscode-option>${solcVer}</vscode-option>`;
    }

    for (let value of solidityVersions) {
        if (value === solcVer) {
            continue;
        }
        dropdownSolVer.innerHTML += `<vscode-option>${value}</vscode-option>`;
    }

    if (contractJson['sourceDirs']) {
        let sourceDirs = '';
        for (let sourceDir of contractJson['sourceDirs']) {
            sourceDirs += `${sourceDir}\n`;
        }
        if (sourceDirs.length > 0) {
            inputSourceDirs.value = sourceDirs.substring(0, sourceDirs.length - 1);
        }
    }

    chkFork.checked = (contractJson['fork'] && contractJson['fork']['enable']);
    inputForkUrl.value = (contractJson['fork'] && contractJson['fork']['url']) ?? '';
    inputForkBlock.value = (contractJson['fork'] && contractJson['fork']['blockNumber']) ? contractJson['fork']['blockNumber'] : '';
    chkBreakOnEntry.checked = (contractJson['breakOnEntry'] && true);
    chkVerbose.checked = (contractJson['verbose'] && true);
    chkExecutionTrace.checked = (contractJson['executionTrace'] && true);
    inputEntryPoint.value = contractJson['entryPoint'] ?? '';

    chkHardhat.checked = false;
    inputHardhatPath.value = '';

    if (contractJson['symbols'] && contractJson['symbols']['hardhat'] && contractJson['symbols']['hardhat']['projectPaths'] && contractJson['symbols']['hardhat']['projectPaths'].length > 0) {
        let hhPath = contractJson['symbols']['hardhat']['projectPaths'][0];
        chkHardhat.checked = true;
        inputHardhatPath.value = hhPath;
    }

    renderPreSteps(contractJson, 'preBuildSteps', gridPreBuildSteps);
    renderPreSteps(contractJson, 'preDebugSteps', gridPreDebugSteps);
}


function renderPreSteps(contractJson, preKind, vscodeDataGrid) {
    vscodeDataGrid.innerHTML = `
        <vscode-data-grid-row>
            <vscode-data-grid-cell grid-column="1"><b>Command Line</b></vscode-data-grid-cell>
            <vscode-data-grid-cell grid-column="2"><b>Expected Output</b></vscode-data-grid-cell>
            <vscode-data-grid-cell grid-column="3"></vscode-data-grid-cell>
        </vscode-data-grid-row>`;

    if (!contractJson[preKind]) {
        return;
    }

    for (let i = 0; i < contractJson[preKind].length; i++) {
        let step = contractJson[preKind][i];

        let elementId = `remove-${preKind}-${removeId++}`;

        vscodeDataGrid.innerHTML += `
                <vscode-data-grid-row>
                    <vscode-data-grid-cell grid-column="1"><pre>"${step['cmdline']}"</pre></vscode-data-grid-cell>
                    <vscode-data-grid-cell grid-column="2"><pre>${step['expectedOutput'] ? '"' + step['expectedOutput'] + '"' : ''}</pre></vscode-data-grid-cell>
                    <vscode-data-grid-cell grid-column="3"><pre><a id="${elementId}" href="#">Remove</a></pre></vscode-data-grid-cell>
                </vscode-data-grid-row>`;

        let element = document.getElementById(elementId);
        if (element) {
            element.addEventListener("click", () => {
                contractJson[preKind].splice(i, 1);
                renderPreSteps(contractJson, preKind, vscodeDataGrid);
            });
        }
    }
}


function addPreStep(preKind, cmdline, expectedOutput, vscodeDataGrid) {
    if (!preKind || !cmdline) {
        return;
    }

    let item = {'cmdline': cmdline};
    if (expectedOutput) {
        item['expectedOutput'] = expectedOutput;
    }

    if (!selectedContractJson[preKind]) {
        selectedContractJson[preKind] = [];
    }
    selectedContractJson[preKind].push(item);

    renderPreSteps(selectedContractJson, preKind, vscodeDataGrid);
}


function guiInit() {
    let selectedContractName = jsonPack['selectedContractName'];

    if (!jsonPack['projectJson']) {
        guiDisable();
    } else {
        guiEnable();
    }

    if (selectedContractName) {
        dropdownContractNames.innerHTML = `<vscode-option>${selectedContractName}</vscode-option>`;
    }

    for (let contractName of contractNames) {
        if (contractName === selectedContractName) {
            continue;
        }
        dropdownContractNames.innerHTML += `<vscode-option>${contractName}</vscode-option>`;
    }

    dropdownContractNames.innerHTML += `<vscode-option>(any)</vscode-option>`;

    if (selectedContractName) {
        loadContract(selectedContractName);
    }

    dropdownContractNames.addEventListener("change", () => {
        loadContract(dropdownContractNames.value);
    });

    buttonAddTest.addEventListener("click", () => {
        vscode.postMessage({request: 'buttonAddTest'});
    });
    buttonRemoveTest.addEventListener("click", () => {
        vscode.postMessage({request: 'buttonRemoveTest', selectedContractName: dropdownContractNames.value});
    });
    buttonCancel.addEventListener('click', () => {
        vscode.postMessage({request: 'buttonCancel'});
    });
    buttonSave.addEventListener('click', () => {
        vscode.postMessage({request: 'buttonSave', 'saveData': jsonPack});
    });
    buttonApply.addEventListener('click', () => {
        vscode.postMessage({request: 'buttonApply', 'saveData': jsonPack});
    });

    dropdownSolVer.addEventListener("change", () => {
        selectedContractJson['solc'] = dropdownSolVer.value;
        selectedContractJson['evm'] = undefined;
        updateEvmDropDown(selectedContractJson['solc'], selectedContractJson['evm']);
    });

    dropdownEvmVer.addEventListener("change", () => {
        if (dropdownEvmVer.value !== defaultEvm) {
            selectedContractJson['evm'] = dropdownEvmVer.value;
        } else {
            selectedContractJson['evm'] = undefined;
        }
    });

    inputSourceDirs.addEventListener("change", () => {
        selectedContractJson['sourceDirs'] = inputSourceDirs.value.split('\n');
    });

    buttonPreBuildAddStep.addEventListener("click", () => {
        addPreStep('preBuildSteps', inputPreBuildCmdline.value, inputPreBuildExpectedOutput.value, gridPreBuildSteps);
    });

    buttonPreDebugAddStep.addEventListener("click", () => {
        addPreStep('preDebugSteps', inputPreDebugCmdline.value, inputPreDebugExpectedOutput.value, gridPreDebugSteps);
    });

    chkBreakOnEntry.addEventListener('click', () => {
        selectedContractJson['breakOnEntry'] = chkBreakOnEntry.checked;
    });

    chkVerbose.addEventListener('click', () => {
        selectedContractJson['verbose'] = chkVerbose.checked;
    });

    chkExecutionTrace.addEventListener('click', () => {
        selectedContractJson['executionTrace'] = chkExecutionTrace.checked;
    });

    inputEntryPoint.addEventListener('change', () => {
        selectedContractJson['inputEntryPoint'] = inputEntryPoint.value;
    });

    chkFork.addEventListener('click', () => {
        if (!selectedContractJson['fork']) {
            selectedContractJson['fork'] = {};
        }
        selectedContractJson['fork']['enable'] = chkFork.checked;
    });

    inputForkUrl.addEventListener('change', () => {
        if (!selectedContractJson['fork']) {
            selectedContractJson['fork'] = {};
        }
        selectedContractJson['fork']['url'] = inputForkUrl.value;
    });

    inputForkBlock.addEventListener('change', () => {
        if (!selectedContractJson['fork']) {
            selectedContractJson['fork'] = {};
        }
        let blockNumber = parseInt(inputForkBlock.value);
        if (blockNumber) {
            selectedContractJson['fork']['blockNumber'] = blockNumber;
        } else {
            selectedContractJson['fork']['blockNumber'] = undefined;
        }
        inputForkBlock.value = selectedContractJson['fork']['blockNumber'] ?? '';
    });

    chkHardhat.addEventListener('click', () => {
        if (!selectedContractJson['symbols']) {
            selectedContractJson['symbols'] = {};
        }
        if (!selectedContractJson['symbols']['hardhat']) {
            selectedContractJson['symbols']['hardhat'] = {};
        }
        if (!selectedContractJson['symbols']['hardhat']['projectPaths']) {
            selectedContractJson['symbols']['hardhat']['projectPaths'] = [];
        }

        if (!chkHardhat.checked) {
            selectedContractJson['symbols']['hardhat']['projectPaths'] = [];
            inputHardhatPath.value = '';
            inputHardhatPath.readOnly = true;
        } else {
            inputHardhatPath.value = '.';
            inputHardhatPath.readOnly = false;
            selectedContractJson['symbols']['hardhat']['projectPaths'] = [inputHardhatPath.value,];
        }
    });

    inputHardhatPath.addEventListener('change', () => {
        if (!selectedContractJson['symbols']) {
            selectedContractJson['symbols'] = {};
        }
        if (!selectedContractJson['symbols']['hardhat']) {
            selectedContractJson['symbols']['hardhat'] = {};
        }
        if (!selectedContractJson['symbols']['hardhat']['projectPaths']) {
            selectedContractJson['symbols']['hardhat']['projectPaths'] = [];
        }

        if (chkHardhat.checked) {
            selectedContractJson['symbols']['hardhat']['projectPaths'] = [inputHardhatPath.value,];
        }
    });
}


function setVSCodeMessageListener() {
    window.addEventListener("message", (event) => {
        switch (event.data.response) {
            case 'init':
                vscode.postMessage({request: 'getContractNames'});
                break;

            case 'getContractNames':
                contractNames = event.data.value ?? [];
                vscode.postMessage({request: 'getProjectRoot'});
                break;

            case 'getProjectRoot':
                inputProjectRoot.value = event.data.value;
                vscode.postMessage({request: 'getSolidityVersions'});
                break;

            case 'getSolidityVersions':
                solidityVersions = event.data.value ?? [];
                vscode.postMessage({request: 'getProjectJsons'});
                break;

            case 'getProjectJsons':
                jsonPack = event.data.value;
                guiInit();
                break;

            default:
                break;
        }
    });
}

window.addEventListener("load", onLoad);

soliditySupportedEvms = {
    "0.4.21": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.4.22": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.4.23": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.4.24": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.4.25": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.4.26": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.0": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.1": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.2": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.3": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.4": [
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.5": [
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.6": [
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.7": [
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.8": [
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.9": [
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.10": [
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.11": [
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.12": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.13": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.14": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.15": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.16": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.5.17": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.0": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.1": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.2": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.3": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.4": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.5": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.6": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.7": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.8": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.9": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.10": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.11": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.6.12": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.7.0": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.7.1": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.7.2": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.7.3": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.7.4": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.7.5": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.7.6": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.0": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.1": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.2": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.3": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.4": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.5": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.6": [
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.7": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.8": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.9": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.10": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.11": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.12": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.13": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.14": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.15": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.16": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.17": [
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.18": [
        "paris",
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.19": [
        "paris",
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ],
    "0.8.20": [
        "shanghai",
        "paris",
        "london",
        "berlin",
        "istanbul",
        "petersburg",
        "constantinople",
        "byzantium",
        "spuriousDragon",
        "tangerineWhistle",
        "homestead"
    ]
};