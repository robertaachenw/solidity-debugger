const inputProjectName = "input-project-name";
const chkTemplateMinimal = "chk-template-minimal";
const chkTemplateERC20 = "chk-template-erc20";
const chkTemplateHardhat = "chk-template-hh";
const dropDownSolidityVersion = "dropdown-solidity-version";
const inputProjectPath = "input-project-path";
const buttonCreateProject = "button-create-project";
const buttonBrowse = "button-browse";
const buttonGoBack = "button-go-back";
const inputBrowsePath = "input-browse-path";

const nextBtn = "next-btn";
const viewChooseOption = "view-choose-option";
const viewCreateProj = "view-create-proj-section";
const viewInitHere = "view-init-here";
const viewInitFromExample = "view-init-from-example";
const viewInitFromExisting = "view-init-from-existing";

const radioChooseOption = "radio-choose-option";
const optionInitHere = "radio-init-here";
const optionFromExisting = "radio-from-existing";
const optionFromExample = "radio-from-example";

const inputProjectNameElement = document.getElementById(inputProjectName);
const chkTemplateMinimalElement = document.getElementById(chkTemplateMinimal);
const chkTemplateERC20Element = document.getElementById(chkTemplateERC20);
const chkTemplateHardhatElement = document.getElementById(chkTemplateHardhat);
const dropDownSolidityVersionElement = document.getElementById(dropDownSolidityVersion);
const inputProjectPathElement = document.getElementById(inputProjectPath);
const buttonCreateProjectElement = document.getElementById(buttonCreateProject);
const buttonBrowseElement = document.getElementById(buttonBrowse);
const buttonGoBackElement = document.getElementById(buttonGoBack);
const inputBrowsePathElement = document.getElementById(inputBrowsePath);
const nextBtnElement = document.getElementById(nextBtn);
const viewChooseOptionElement = document.getElementById(viewChooseOption);
const viewCreateProjElement = document.getElementById(viewCreateProj);
const viewInitHereElement = document.getElementById(viewInitHere);
const viewInitFromExampleElement = document.getElementById(viewInitFromExample);
const viewInitFromExistingElement = document.getElementById(viewInitFromExisting);
const radioChooseOptionElement = document.getElementById(radioChooseOption);
const optionInitHereElement = document.getElementById(optionInitHere);
const optionFromExistingElement = document.getElementById(optionFromExisting);
const optionFromExampleElement = document.getElementById(optionFromExample);

const vscode = acquireVsCodeApi();

let originalDropdownHtml = undefined;
let solidityVersionsAdded = false;
let additionalSolidityVersions = [];
let initMessage = false;
let optionInitHereValue = optionInitHereElement.disabled;

inputProjectNameElement.disabled = true;
radioChooseOptionElement.disabled = true;
nextBtnElement.disabled = true;

window.addEventListener("load", onLoad);

function onLoad() {
    document.getElementById(buttonBrowse).addEventListener("click", () => {
        sendGuiContent(buttonBrowse);
    });
    document.getElementById(buttonCreateProject).addEventListener("click", () => {
        sendGuiContent(buttonCreateProject);
    });
    document.getElementById(inputProjectName).addEventListener("input", () => {
        sendGuiContent(inputProjectName);
    });
    document.getElementById(inputProjectPath).addEventListener("input", () => {
        sendGuiContent(inputProjectPath);
    });
    document.getElementById(dropDownSolidityVersion).addEventListener("click", () => {
        sendGuiContent(dropDownSolidityVersion);
    });

    document.getElementById(chkTemplateMinimal).addEventListener("change", () => {
        sendGuiContent(chkTemplateMinimal);
    });
    document.getElementById(chkTemplateERC20).addEventListener("change", () => {
        sendGuiContent(chkTemplateERC20);
    });
    document.getElementById(chkTemplateHardhat).addEventListener("change", () => {
        sendGuiContent(chkTemplateHardhat);
    });


    document.getElementById(nextBtn).addEventListener("click", () => {
        document.getElementById(viewChooseOption).hidden = true;
        const idOfSelected = document.getElementById(radioChooseOption).selectedRadio.id;
        switch (idOfSelected) {
            case optionInitHere:
                if (!solidityVersionsAdded && additionalSolidityVersions.length !== 0) {
                    originalDropdownHtml = document.getElementById(dropDownSolidityVersion).innerHTML;
                    let addedHtml = "";
                    for (const ver of additionalSolidityVersions) {
                        addedHtml += `<vscode-option>${ver}</vscode-option>`;
                    }
                    document.getElementById(dropDownSolidityVersion).innerHTML = addedHtml + originalDropdownHtml;
                    solidityVersionsAdded = true;
                }

                break;
            case optionFromExisting:
                document.getElementById(viewInitFromExisting).hidden = false;
                if (originalDropdownHtml !== undefined) {
                    document.getElementById(dropDownSolidityVersion).innerHTML = originalDropdownHtml;
                    solidityVersionsAdded = false;
                }
                document.getElementById(dropDownSolidityVersion).innerHTML;
                break;
            case optionFromExample:
                document.getElementById(viewInitFromExample).hidden = false;
                break;
        }

        document.getElementById(viewCreateProj).hidden = false;
    });

    document.getElementById(buttonGoBack).addEventListener("click", () => {
        const allViews = [viewInitFromExample, viewInitFromExisting, viewInitHere, viewCreateProj];
        for (const viewId of allViews) {
            document.getElementById(viewId).hidden = true;
        }

        document.getElementById(viewChooseOption).hidden = false;
    });

    setVSCodeMessageListener();

    vscode.postMessage({
        sender: 'init'
    });
}


function sendGuiContent(senderName) {
    vscode.postMessage({
        sender: senderName,
        projectName: document.getElementById(inputProjectName).value,
        templateMinimal: document.getElementById(chkTemplateMinimal).checked,
        templateERC20: document.getElementById(chkTemplateERC20).checked,
        templateHardhat: document.getElementById(chkTemplateHardhat).checked,
        solidityVersion: document.getElementById(dropDownSolidityVersion).value,
        projectPath: document.getElementById(inputProjectPath).value,
        projectPathPlaceHolder: document.getElementById(inputProjectPath).placeholder,
        existingProjectPath: document.getElementById(inputBrowsePath).value,
        activePanel: document.getElementById(radioChooseOption).selectedRadio.id,
    });
}

function setVSCodeMessageListener() {
    window.addEventListener("message", (event) => {
        if (!initMessage) {
            initMessage = true;
            inputProjectNameElement.disabled = false;
            radioChooseOptionElement.disabled = false;
            nextBtnElement.disabled = false;
            optionInitHereElement.disabled = optionInitHereValue;
        }

        if (event.data.currentWorkspaceSolidityVersions !== undefined) {
            if (additionalSolidityVersions.length === 0) {
                additionalSolidityVersions.push(...event.data.currentWorkspaceSolidityVersions);
            }
        }

        if (event.data.browseSelectedPath !== undefined) {
            document.getElementById(inputBrowsePath).value = event.data.browseSelectedPath;
        }
        if (event.data.projectName !== undefined) {
            document.getElementById(inputProjectName).value = event.data.projectName;
        }

        if (event.data.templateMinimal !== undefined) {
            document.getElementById(chkTemplateMinimal).checked = event.data.templateMinimal;
        }

        if (event.data.templateERC20 !== undefined) {
            document.getElementById(chkTemplateERC20).checked = event.data.templateERC20;
        }

        if (event.data.templateHardhat !== undefined) {
            document.getElementById(chkTemplateHardhat).checked = event.data.templateHardhat;
        }

        if (event.data.solidityVersion !== undefined) {
            document.getElementById(dropDownSolidityVersion).value = event.data.solidityVersion;
        }

        if (event.data.projectPath !== undefined) {
            document.getElementById(inputProjectPath).value = event.data.projectPath;
        }

        if (event.data.projectPathPlaceHolder !== undefined) {
            document.getElementById(inputProjectPath).placeholder = event.data.projectPathPlaceHolder;
        }
    });
}
